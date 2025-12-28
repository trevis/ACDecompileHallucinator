#!/usr/bin/env python3
"""
LLM Processing Pipeline
=======================
Processes decompiled C++ classes through LLM in dependency order.

Workflow:
1. Load processing order from database/JSON
2. For each class (in dependency order):
   - Generate header via LLM
   - Process each method via LLM (one at a time)
   - Assemble final .cpp from processed methods (no LLM)
3. Output debug files showing prompts, responses, and types

Usage:
    python llm_process.py --db mcp-sources/types.db --output ./output
    python llm_process.py --db mcp-sources/types.db --class PlayerModule --debug
    python llm_process.py --db mcp-sources/types.db --dry-run
"""
import argparse
import json
import logging
import re
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Dict, Any, Optional, Callable
from tqdm import tqdm

from code_parser import (
    DatabaseHandler, DependencyAnalyzer,
    ClassHeaderGenerator, FunctionProcessor, ClassAssembler,
    LLMCache, ContextBuilder
)
from code_parser.class_assembler import ProcessedMethod

# Engine abstraction layer
from engines import get_engine, list_engines, EngineConfig, LLMEngine


# ────────────────────────────────────────────────────────────────────────────────
# Configuration
# ────────────────────────────────────────────────────────────────────────────────
DEFAULT_ENGINE = "lm-studio"
LM_STUDIO_URL = "http://localhost:1234/v1"
MAX_LLM_TOKENS = 131072

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s │ %(levelname)-7s │ %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
logger = logging.getLogger("llm-processor")


# NOTE: The legacy LLMClient class has been replaced by the engine abstraction.
# Use engines.get_engine("lm-studio") or engines.get_engine("claude-code") instead.
# See engines/ module for implementation details.


# ────────────────────────────────────────────────────────────────────────────────
# Output Cleaning
# ────────────────────────────────────────────────────────────────────────────────
def clean_llm_output(text: str) -> str:
    """Remove common LLM output artifacts"""
    if not text:
        return ""
    
    # Remove markdown code blocks
    text = re.sub(r'^```(?:cpp|c\+\+|c|\w+)?\s*\n?', '', text, flags=re.M | re.I)
    text = re.sub(r'\n?```$', '', text, flags=re.M)
    
    # Remove markdown formatting
    text = re.sub(r'\*\*([^*]+)\*\*', r'\1', text)
    text = re.sub(r'__([^_]+)__', r'\1', text)
    
    return text.strip()


def split_namespace(full_name: str) -> tuple[Optional[str], str]:
    """
    Split a fully qualified name into (namespace, simple_name).
    Follows mcp-sources convention:
    - Namespace is the top-level namespace only (e.g. 'Turbine::Debug' -> 'Turbine')
    - Simple name is the last part (e.g. 'Turbine::Debug::Assert' -> 'Assert')
    """
    if '::' in full_name:
        parts = full_name.split('::')
        return parts[0], parts[-1]
    return None, full_name


# ────────────────────────────────────────────────────────────────────────────────
# Processing Result
# ────────────────────────────────────────────────────────────────────────────────
@dataclass
class ProcessingResult:
    """Result of processing a single type (enum or struct).

    Used for tracking results in parallel processing.
    """
    type_name: str
    kind: str  # "enum" or "struct"
    success: bool
    header_path: Optional[Path] = None
    source_path: Optional[Path] = None
    method_count: int = 0
    error: Optional[str] = None


# ────────────────────────────────────────────────────────────────────────────────
# Main Processor
# ────────────────────────────────────────────────────────────────────────────────
class LLMProcessor:
    """
    Main orchestrator for LLM-based code processing.
    
    Processes classes in dependency order, generating headers and methods
    one at a time, with optional debug output.
    """
    
    def __init__(self, db_path: Path, output_dir: Path,
                 debug_dir: Optional[Path] = None,
                 dry_run: bool = False,
                 force: bool = False,
                 engine: Optional[LLMEngine] = None,
                 engine_name: str = DEFAULT_ENGINE,
                 engine_config: Optional[EngineConfig] = None,
                 use_skills: bool = True,
                 max_workers: int = 1):
        """
        Initialize the processor.

        Args:
            db_path: Path to types.db database
            output_dir: Output directory for generated files
            debug_dir: Optional directory for debug output
            dry_run: If True, don't call LLM or write files
            force: If True, reprocess even if already done
            engine: Pre-configured LLM engine instance (optional)
            engine_name: Name of engine to use if no engine provided
            engine_config: Configuration for engine if no engine provided
            use_skills: Whether to use skill-based prompt enhancement (default True)
            max_workers: Number of parallel workers (1=sequential, >1=parallel)
        """
        self.db = DatabaseHandler(str(db_path))
        self.output_dir = Path(output_dir)
        self.debug_dir = Path(debug_dir) if debug_dir else None
        self.dry_run = dry_run
        self.force = force
        self.use_skills = use_skills
        self.max_workers = max_workers
        self.processed_owners = set()

        # Initialize cache (separate database)
        cache_path = self.db.db_path.parent / "llm_cache.db"
        self.cache = LLMCache(cache_path)
        logger.info(f"LLM Cache: {cache_path}")
        logger.info(f"Skill enhancement: {'enabled' if use_skills else 'disabled'}")

        # Initialize components
        self.analyzer = DependencyAnalyzer(self.db)
        self.assembler = ClassAssembler(self.output_dir)

        # Engine configuration
        self._engine = engine
        self._engine_name = engine_name
        self._engine_config = engine_config

        # Headers and methods processors (lazy-loaded with debug support)
        self._header_gen = None
        self._func_processor = None

        # Context builder (shared between components for unified context)
        self._context_builder = None
    
    def get_file_owner(self, type_name: str) -> str:
        """Find the top-level struct/class that owns this type's file."""
        if '::' not in type_name:
            return type_name
            
        parts = type_name.split('::')
        # Crawl from top to bottom
        for i in range(1, len(parts)):
            prefix = '::'.join(parts[:i])
            # Check if prefix is a struct
            res = self.db.get_type_by_name(prefix, 'struct')
            if res:
                return prefix
        return type_name

    def get_header_path(self, type_name: str) -> Path:
        """Get the expected header path for a type"""
        # If it's a template instantiation, we use its base name's owner
        effective_name = type_name
        if self.header_generator.is_template_instantiation(type_name):
            effective_name = self.header_generator.get_template_base_name(type_name)
            
        owner = self.get_file_owner(effective_name)

        if self.header_generator.is_template_instantiation(type_name):
            namespace, simple_name = split_namespace(owner)
            if namespace:
                return self.output_dir / "include" / "Templates" / namespace.replace('::', '/') / f"{simple_name}.h"
            return self.output_dir / "include" / "Templates" / f"{simple_name}.h"

        namespace, simple_name = split_namespace(owner)
        if namespace:
            return self.output_dir / "include" / namespace.replace('::', '/') / f"{simple_name}.h"
        return self.output_dir / "include" / f"{simple_name}.h"

    def get_source_path(self, type_name: str) -> Path:
        """Get the expected source path for a type"""
        effective_name = type_name
        if self.header_generator.is_template_instantiation(type_name):
            effective_name = self.header_generator.get_template_base_name(type_name)
            
        owner = self.get_file_owner(effective_name)
        namespace, simple_name = split_namespace(owner)
        if namespace:
            return self.output_dir / "src" / namespace.replace('::', '/') / f"{simple_name}.cpp"
        return self.output_dir / "src" / f"{simple_name}.cpp"

    @property
    def engine(self) -> LLMEngine:
        """Lazy-load LLM engine.

        Uses the engine abstraction layer for all LLM operations.
        The engine is configured via constructor parameters.
        """
        if self._engine is None:
            # Build engine config if not provided
            config = self._engine_config
            if config is None:
                config = EngineConfig(
                    temperature=0.2,
                    max_tokens=MAX_LLM_TOKENS,
                    extra={"base_url": LM_STUDIO_URL}
                )

            self._engine = get_engine(self._engine_name, config=config, cache=self.cache)
            logger.info(f"Using engine: {self._engine.name}")

        return self._engine

    @property
    def llm_client(self) -> LLMEngine:
        """Backward-compatible alias for engine property."""
        return self.engine

    @property
    def context_builder(self) -> ContextBuilder:
        """Get or create the shared ContextBuilder instance.

        The ContextBuilder provides unified context gathering for both
        the FunctionProcessor and ClaudeCodeEngine, including:
        - Type reference extraction
        - Enum value mapping
        - Parent class context
        - Constant annotation
        """
        if self._context_builder is None:
            self._context_builder = ContextBuilder(
                db=self.db,
                output_dir=self.output_dir,
            )
            logger.debug("ContextBuilder initialized")

            # If engine is Claude Code, attach the context builder
            if hasattr(self._engine, 'set_context_builder'):
                self._engine.set_context_builder(self._context_builder)

        return self._context_builder

    @property
    def header_generator(self) -> ClassHeaderGenerator:
        """Lazy-load header generator with debug support"""
        if self._header_gen is None:
            self._header_gen = ClassHeaderGenerator(
                self.db, 
                llm_client=self.llm_client if not self.dry_run else None,
                debug_dir=self.debug_dir
            )
            self._header_gen.dependency_analyzer = self.analyzer
        return self._header_gen
    
    @property
    def function_processor(self) -> FunctionProcessor:
        """Lazy-load function processor with debug support and context builder"""
        if self._func_processor is None:
            self._func_processor = FunctionProcessor(
                self.db,
                llm_client=self.llm_client if not self.dry_run else None,
                debug_dir=self.debug_dir,
                project_root=Path.cwd(),
                use_skills=self.use_skills,
                context_builder=self.context_builder
            )
        return self._func_processor
    
    def get_processing_order(self) -> List[Dict[str, str]]:
        """Get types to process in dependency order"""
        # Check for existing processing_order.json
        order_file = self.output_dir.parent / "mcp-sources" / "processing_order.json"
        if order_file.exists():
            with open(order_file) as f:
                return json.load(f)
        
        # Build fresh from analyzer
        self.analyzer.build_dependency_graph()
        order = self.analyzer.get_processing_order()
        return [{"name": name, "kind": kind} for name, kind in order]
    
    def process_enum(self, enum_name: str, pbar: Optional[tqdm] = None) -> Optional[Path]:
        """Process an enum (copy to header, no LLM needed)"""
        # Get enum from database
        enums = self.db.get_type_by_name(enum_name, 'enum')
        if not enums:
            logger.warning(f"Enum not found: {enum_name}")
            return None
        
        enum_row = enums[0]
        enum_code = enum_row[5] if len(enum_row) > 5 else ""
        
        if self.dry_run:
            logger.info(f"[DRY-RUN] Would copy enum: {enum_name}")
            return None
        
        # Check if already exists
        header_path = self.get_header_path(enum_name)
        if header_path.exists() and not self.force:
            logger.info(f"✓ Enum: {enum_name} (already exists, skipping)")
            return header_path

        # Write enum header
        namespace, simple_name = split_namespace(enum_name)
        path = self.assembler.write_enum_header(
            simple_name, 
            enum_code,
            namespace=namespace
        )
        if path:
            logger.info(f"✓ Enum: {enum_name} → {path.name}")
            if pbar:
                pbar.update(1)
        
        return path

    def group_templates(self, order: List[Dict[str, str]]) -> List[Dict[str, str]]:
        """Group template instantiations by their base name"""
        grouped_order = []
        processed_templates = set()

        for item in order:
            name = item['name']
            if self.header_generator.is_template_instantiation(name):
                base_name = self.header_generator.get_template_base_name(name)
                if base_name not in processed_templates:
                    item['name'] = name  # Keep original as representative
                    item['is_representative'] = True
                    grouped_order.append(item)
                    processed_templates.add(base_name)
                else:
                    # Skip other instantiations for header generation
                    continue
            else:
                grouped_order.append(item)
        
        return grouped_order
    
    def process_class(self, class_name: str, pbar: Optional[tqdm] = None) -> Dict[str, Any]:
        """Process a single class by name (determines if enum or struct)"""
        # Determine owner
        owner = self.get_file_owner(class_name)
        if owner != class_name:
            if owner in self.processed_owners:
                return {"header_path": None, "source_path": None, "method_count": 0}
            logger.info(f"Redirecting {class_name} to owner {owner}")
            return self.process_class(owner, pbar=pbar)
            
        # Check if it's an enum
        enums = self.db.get_type_by_name(class_name, 'enum')
        if enums:
            path = self.process_enum(class_name, pbar=pbar)
            self.processed_owners.add(class_name)
            return {"header_path": path, "source_path": None, "method_count": 0}
        
        # Otherwise treat as struct
        result = self.process_struct(class_name, pbar=pbar)
        self.processed_owners.add(class_name)
        return result

    def process_struct(self, class_name: str, pbar: Optional[tqdm] = None) -> Dict[str, Any]:
        """
        Process a struct/class: generate header + process methods.
        
        Returns dict with:
        - header_path: Path to generated header
        - source_path: Path to generated source
        - method_count: Number of methods processed
        """
        result = {"header_path": None, "source_path": None, "method_count": 0}
        
        if self.dry_run:
            methods = self.db.get_methods_by_parent(class_name)
            logger.info(f"[DRY-RUN] Would process: {class_name} ({len(methods)} methods)")
            return result
        
        logger.info(f"┌─ Processing: {class_name}")

        if self.force:
            logger.info(f"│  └─ Force enabled: Clearing previous results...")
            self.db.clear_processed_class(class_name)

        # Skip if header already exists
        header_path = self.get_header_path(class_name)
        source_path = self.get_source_path(class_name)
        is_template = self.header_generator.is_template_instantiation(class_name)
        
        # Check if output files are missing and force regeneration if so
        if not self.force:
            header_missing = not header_path.exists()
            source_missing = not is_template and not source_path.exists()
            
            if header_missing or source_missing:
                reason = "Header missing" if header_missing else "Source missing"
                if header_missing and source_missing: reason = "Header and Source missing"
                logger.info(f"│  └─ {reason}: Triggering regeneration...")
                self.db.clear_processed_class(class_name)
        
        analysis = None
        extracted_types = []
        
        if header_path.exists() and not self.force:
            logger.info(f"│  └─ Header already exists: {header_path.relative_to(self.output_dir.parent)}")
            result["header_path"] = header_path
        else:
            # Step 0: Analysis
            logger.info(f"│  └─ Analyzing class...")
            try:
                analysis_result = self.header_generator.analyze_class(class_name)
                if analysis_result:
                    logger.info(f"│  └─ ✓ Analysis complete")
                    # Parse the analysis result to extract referenced types
                    import json
                    try:
                        analysis_data = json.loads(analysis_result)
                        analysis = analysis_data.get("analysis", "")
                        extracted_types = analysis_data.get("referenced_types", [])
                        logger.info(f"│  └─ Found {len(extracted_types)} referenced types from analysis")
                    except json.JSONDecodeError:
                        # If JSON parsing fails, use the original analysis
                        analysis = analysis_result
                        extracted_types = []
                else:
                    logger.warning(f"│  └─ ✗ Analysis failed (empty)")
            except Exception as e:
                logger.error(f"│  └─ ✗ Analysis error: {e}")

            # Step 1: Generate header
            logger.info(f"│  └─ Generating header...")
            
            try:
                header_code = self.header_generator.generate_header(class_name, save_to_db=True, analysis=analysis)
                if header_code:
                    header_code = clean_llm_output(header_code)
                    namespace, simple_name = split_namespace(class_name)
                    result["header_path"] = self.assembler.write_header_file(
                        simple_name,
                        header_code,
                        namespace=namespace,
                        path=header_path
                    )
                    logger.info(f"│  └─ ✓ Header saved")
                    if pbar:
                        pbar.update(1)
                else:
                    logger.warning(f"│  └─ ✗ Header generation failed")
            except Exception as e:
                logger.error(f"│  └─ ✗ Header error: {e}")
        
        # Step 2: Process methods one at a time (SKIP for templates)
        if self.header_generator.is_template_instantiation(class_name):
            logger.info(f"│  └─ Template instantiation: skipping separate method processing and source assembly")
            logger.info(f"└─ Done: {class_name}")
            return result

        all_methods = []
        unprocessed_methods = []
        
        # Gather all types that belong to this file
        nested_types = self.db.get_nested_types(class_name)
        file_types = [class_name] + [nt[2] for nt in nested_types]
        
        for t in file_types:
            all_methods.extend(self.db.get_methods_by_parent(t))
            unprocessed_methods.extend(self.db.get_unprocessed_methods(parent_class=t))
            
        source_path = self.get_source_path(class_name)
        
        # Check if we should skip method processing
        if not unprocessed_methods and source_path.exists() and not self.force:
            logger.info(f"│  └─ Source already exists and all methods processed: {source_path.relative_to(self.output_dir.parent)}")
            result["source_path"] = source_path
            result["method_count"] = len(all_methods)
            logger.info(f"└─ Done: {class_name} (skipped)")
            return result

        if unprocessed_methods:
            logger.info(f"│  └─ Processing {len(unprocessed_methods)} methods for {len(file_types)} classes...")
        
        for method in unprocessed_methods:
            method_name = method[1]
            
            try:
                logger.info(f"│     └─ {method_name}...")
                self.function_processor.process_function(method, save_to_db=True, analysis=analysis)
                result["method_count"] += 1
                if pbar:
                    pbar.update(1)
            except Exception as e:
                logger.error(f"│     └─ ✗ {method_name}: {e}")
        
        # Step 3: Assemble source file (no LLM) - Gather ALL processed methods
        # This ensures that even if we resumed, we include methods from previous runs
        processed_db_methods = []
        for t in file_types:
            processed_db_methods.extend(self.db.get_processed_methods_by_parent(t))

        if processed_db_methods:
            logger.info(f"│  └─ Assembling source with {len(processed_db_methods)} methods...")
            
            processed_methods = []
            for pm in processed_db_methods:
                processed_methods.append(ProcessedMethod(
                    name=pm['name'],
                    full_name=pm['full_name'],
                    parent_class=pm['parent_class'],
                    processed_code=clean_llm_output(pm['processed_code']),
                    dependencies=pm.get('dependencies', []),
                    offset=pm.get('offset', '0')
                ))

            namespace, simple_name = split_namespace(class_name)
            result["source_path"] = self.assembler.write_source_file(
                simple_name, 
                processed_methods,
                namespace=namespace
            )
            if result["source_path"]:
                logger.info(f"│  └─ ✓ Source saved")
        
        logger.info(f"└─ Done: {class_name}")
        return result
    
        # Otherwise treat as struct
        return self.process_struct(class_name, pbar=pbar)

    def calculate_work_units(self, order: List[Dict[str, str]]) -> Dict[str, Any]:
        """Calculate number of headers and methods that actually need processing."""
        total_headers = 0
        total_methods = 0
        processed_owners = set()
        
        logger.info("Calculating total work units...")
        
        for type_info in order:
            name = type_info["name"]
            owner = self.get_file_owner(name)
            
            if owner in processed_owners:
                continue
            processed_owners.add(owner)
            
            kind = type_info["kind"] # Note: This kind might be for the nested type, but we care about the owner's kind
            # Let's get the owner's kind
            owner_rows = self.db.get_type_by_name(owner)
            owner_kind = owner_rows[0][1] if owner_rows else kind
            
            if owner_kind == "enum":
                header_path = self.get_header_path(owner)
                if not header_path.exists() or self.force:
                    total_headers += 1
            else:
                # Struct/Class
                header_path = self.get_header_path(owner)
                source_path = self.get_source_path(owner)
                is_template = self.header_generator.is_template_instantiation(owner)
                
                header_missing = not header_path.exists()
                source_missing = not is_template and not source_path.exists()
                
                needs_regeneration = self.force or header_missing or source_missing
                
                if needs_regeneration:
                    total_headers += 1
                
                if is_template:
                    continue
                
                # Gather methods for owner and all nested types
                nested_types = self.db.get_nested_types(owner)
                file_types = [owner] + [nt[2] for nt in nested_types]
                
                for t in file_types:
                    if needs_regeneration:
                        all_methods = self.db.get_methods_by_parent(t)
                        total_methods += len(all_methods)
                    else:
                        unprocessed_methods = self.db.get_unprocessed_methods(parent_class=t)
                        total_methods += len(unprocessed_methods)
                    
        return {
            "headers": total_headers,
            "methods": total_methods,
            "total": total_headers + total_methods
        }

    def process_all_internal(self, filter_classes: Optional[List[str]] = None) -> Dict[str, Any]:
        """
        Process all types in dependency order.
        
        Args:
            filter_classes: Optional list of class names to process (skip others)
            
        Returns:
            Summary statistics
        """
        order = self.get_processing_order()
        
        # Group templates before processing
        order = self.group_templates(order)
        
        stats = {
            "total": len(order),
            "enums_processed": 0,
            "structs_processed": 0,
            "methods_processed": 0,
            "errors": []
        }
        
        # Filter if requested
        if filter_classes:
            order = [t for t in order if t["name"] in filter_classes]
            logger.info(f"Filtered to {len(order)} types: {[t['name'] for t in order]}")
        
        # Calculate work units for better progress estimation
        work_plan = self.calculate_work_units(order)
        logger.info(f"Plan: {work_plan['headers']} headers, {work_plan['methods']} methods to process.")
        
        if work_plan["total"] == 0:
            logger.info("Nothing to process.")
            return stats

        logger.info(f"Processing {len(order)} types in dependency order...")
        
        with tqdm(total=work_plan["total"], desc="Processing", unit="task") as pbar:
            for type_info in order:
                name = type_info["name"]
                
                # Skip if owner already processed
                owner = self.get_file_owner(name)
                if owner in self.processed_owners:
                    continue
                    
                kind = type_info["kind"]
                try:
                    if kind == "enum":
                        self.process_enum(name, pbar=pbar)
                        stats["enums_processed"] += 1
                    else:
                        result = self.process_struct(name, pbar=pbar)
                        if result.get("header_path"):
                            stats["structs_processed"] += 1
                        stats["methods_processed"] += result.get("method_count", 0)
                except Exception as e:
                    logger.error(f"Failed to process {name}: {e}")
                    stats["errors"].append({"name": name, "error": str(e)})
        
        return stats
    
    def process_all(self, filter_classes: Optional[List[str]] = None) -> Dict[str, Any]:
        """Process all types in dependency order.

        Routes to parallel or sequential processing based on max_workers setting.
        """
        # Note: We don't support force for process_all to prevent accidental mass deletion
        # unless filter_classes is provided
        if self.force and not filter_classes:
            logger.warning("Force flag ignored for bulk processing (safety check)")
            self.force = False

        # Choose processing mode based on max_workers
        if self.max_workers > 1:
            logger.info(f"Using parallel processing with {self.max_workers} workers")
            return self.process_all_parallel(filter_classes)
        else:
            return self.process_all_internal(filter_classes)
    
    def show_plan(self):
        """Show what would be processed (dry-run mode)"""
        order = self.get_processing_order()
        
        print(f"\n{'='*60}")
        print(f"Processing Plan - {len(order)} types")
        print('='*60)
        
        enum_count = sum(1 for t in order if t["kind"] == "enum")
        struct_count = len(order) - enum_count
        
        print(f"Enums:   {enum_count}")
        print(f"Structs: {struct_count}")
        print('-'*60)
        
        for i, type_info in enumerate(order[:20], 1):
            name = type_info["name"]
            kind = type_info["kind"]
            
            if kind == "enum":
                print(f"{i:3}. [ENUM] {name}")
            else:
                methods = self.db.get_methods_by_parent(name)
                print(f"{i:3}. [STRUCT] {name} ({len(methods)} methods)")
        
        if len(order) > 20:
            print(f"... and {len(order) - 20} more types")

        print('='*60 + "\n")

    def process_type(self, type_name: str, kind: str) -> ProcessingResult:
        """Process a single type (enum or struct) and return a result.

        Thread-safe wrapper for process_enum/process_struct for use in
        parallel processing. Does not update progress bar (handled by caller).

        Args:
            type_name: Name of the type to process
            kind: "enum" or "struct"

        Returns:
            ProcessingResult with success status and output paths
        """
        try:
            if kind == "enum":
                path = self.process_enum(type_name, pbar=None)
                return ProcessingResult(
                    type_name=type_name,
                    kind="enum",
                    success=path is not None,
                    header_path=path
                )
            else:
                result = self.process_struct(type_name, pbar=None)
                return ProcessingResult(
                    type_name=type_name,
                    kind="struct",
                    success=result.get("header_path") is not None,
                    header_path=result.get("header_path"),
                    source_path=result.get("source_path"),
                    method_count=result.get("method_count", 0)
                )
        except Exception as e:
            logger.error(f"Error processing {type_name}: {e}")
            return ProcessingResult(
                type_name=type_name,
                kind=kind,
                success=False,
                error=str(e)
            )

    def process_all_parallel(self, filter_classes: Optional[List[str]] = None) -> Dict[str, Any]:
        """Process all types using parallel execution by dependency level.

        Types at the same dependency level have no inter-dependencies and can
        be processed concurrently. Level N is only started after level N-1
        completes, ensuring all dependencies are satisfied.

        Args:
            filter_classes: Optional list of class names to process (skip others)

        Returns:
            Summary statistics
        """
        # Build dependency graph and get levels
        self.analyzer.build_dependency_graph()
        levels = self.analyzer.get_dependency_levels()

        if not levels:
            logger.info("No types to process.")
            return {"total": 0, "enums_processed": 0, "structs_processed": 0,
                    "methods_processed": 0, "errors": []}

        # Calculate totals
        all_types = []
        for level_types in levels.values():
            all_types.extend(level_types)

        # Apply filter if requested
        if filter_classes:
            filter_set = set(filter_classes)
            levels = {
                lvl: [(name, kind) for name, kind in types if name in filter_set]
                for lvl, types in levels.items()
            }
            # Remove empty levels
            levels = {lvl: types for lvl, types in levels.items() if types}

        total_types = sum(len(types) for types in levels.values())

        stats = {
            "total": total_types,
            "enums_processed": 0,
            "structs_processed": 0,
            "methods_processed": 0,
            "errors": []
        }

        if total_types == 0:
            logger.info("Nothing to process.")
            return stats

        logger.info(f"Processing {total_types} types across {len(levels)} levels "
                    f"with {self.max_workers} workers...")

        # Process each level in order
        with tqdm(total=total_types, desc="Processing", unit="type") as pbar:
            for level in sorted(levels.keys()):
                level_types = levels[level]
                if not level_types:
                    continue

                logger.info(f"Level {level}: {len(level_types)} types")

                # Process all types in this level in parallel
                with ThreadPoolExecutor(max_workers=self.max_workers) as executor:
                    # Submit all tasks for this level
                    futures = {
                        executor.submit(self.process_type, name, kind): (name, kind)
                        for name, kind in level_types
                    }

                    # Collect results as they complete
                    for future in as_completed(futures):
                        name, kind = futures[future]
                        result = future.result()

                        if result.success:
                            if result.kind == "enum":
                                stats["enums_processed"] += 1
                            else:
                                stats["structs_processed"] += 1
                                stats["methods_processed"] += result.method_count
                        else:
                            stats["errors"].append({
                                "name": result.type_name,
                                "error": result.error or "Unknown error"
                            })

                        pbar.update(1)

        return stats


# ────────────────────────────────────────────────────────────────────────────────
# CLI Entry Point
# ────────────────────────────────────────────────────────────────────────────────
def main():
    parser = argparse.ArgumentParser(
        description="Process decompiled C++ through LLM in dependency order",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=f"""
Examples:
    # Process all types (default engine: {DEFAULT_ENGINE})
    python llm_process.py --db mcp-sources/types.db --output ./output

    # Process with Claude Code engine (uses skills from .claude/skills/)
    python llm_process.py --engine claude-code --db mcp-sources/types.db

    # Process with LM Studio (local LLM via OpenAI-compatible API)
    python llm_process.py --engine lm-studio --lm-studio-url http://localhost:1234/v1

    # Process single class with debug output
    python llm_process.py --class AllegianceProfile --debug

    # Parallel processing with 4 workers (types at same dependency level run concurrently)
    python llm_process.py --engine claude-code --parallel 4

    # Show processing plan (dry-run)
    python llm_process.py --dry-run

Available engines: {', '.join(list_engines())}

Engine details:
  lm-studio:   Local LLM via OpenAI-compatible API (requires LM Studio running)
  claude-code: Claude Code CLI with skills integration (requires 'claude' CLI)
        """
    )

    parser.add_argument("--db", type=Path, default=Path("mcp-sources/types.db"),
        help="Path to types.db database")
    parser.add_argument("--output", type=Path, default=Path("./output"),
        help="Output directory for generated files")
    parser.add_argument("--engine", type=str, choices=list_engines(), default=DEFAULT_ENGINE,
        help=f"LLM engine to use (default: {DEFAULT_ENGINE})")
    parser.add_argument("--lm-studio-url", type=str, default=LM_STUDIO_URL,
        help=f"LM Studio API URL (default: {LM_STUDIO_URL})")
    parser.add_argument("--temperature", type=float, default=0.2,
        help="LLM temperature for generation (default: 0.2)")
    parser.add_argument("--debug", action="store_true",
        help="Enable debug output (prompts, responses, types)")
    parser.add_argument("--debug-dir", type=Path, default=None,
        help="Debug output directory (default: <output>/debug)")
    parser.add_argument("--class", dest="single_class", type=str, default=None,
        help="Process only this class")
    parser.add_argument("--dry-run", action="store_true",
        help="Show what would be processed without calling LLM")
    parser.add_argument("--force", action="store_true",
        help="Force re-processing (clears previous results for this class)")
    parser.add_argument("--verbose", "-v", action="store_true",
        help="Enable verbose logging")
    parser.add_argument("--no-skills", action="store_true",
        help="Disable skill-based prompt enhancement (use raw few-shot prompts)")
    parser.add_argument("--parallel", type=int, default=1, metavar="N",
        help="Number of parallel workers (default: 1=sequential, >1=parallel by dependency level)")

    args = parser.parse_args()
    
    # Configure logging
    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)
    
    # Validate database
    if not args.db.exists():
        logger.error(f"Database not found: {args.db}")
        logger.error("Run 'python process.py parse' first to create it.")
        return 1
    
    # Set up debug directory
    debug_dir = None
    if args.debug:
        debug_dir = args.debug_dir or (args.output / "debug")
        debug_dir.mkdir(parents=True, exist_ok=True)
        logger.info(f"Debug output: {debug_dir}")

    # Build engine configuration based on selected engine
    extra_config = {}
    if args.engine == "lm-studio":
        extra_config = {
            "base_url": args.lm_studio_url,
            "model": "local-model",
        }
    elif args.engine == "claude-code":
        extra_config = {
            "project_root": str(args.output.parent),  # Use parent of output as project root
            "cli_timeout": 600,  # 10 minutes for complex operations
        }

    engine_config = EngineConfig(
        temperature=args.temperature,
        max_tokens=MAX_LLM_TOKENS,
        extra=extra_config
    )

    # Create processor with engine configuration
    processor = LLMProcessor(
        db_path=args.db,
        output_dir=args.output,
        debug_dir=debug_dir,
        dry_run=args.dry_run,
        force=args.force,
        engine_name=args.engine,
        engine_config=engine_config,
        use_skills=not args.no_skills,
        max_workers=args.parallel
    )
    
    # Handle modes
    if args.dry_run:
        processor.show_plan()
        return 0
    
    if args.single_class:
        result = processor.process_class(args.single_class)
        print(f"\nProcessed: {args.single_class}")
        print(f"  Header: {result.get('header_path', 'None')}")
        print(f"  Source: {result.get('source_path', 'None')}")
        print(f"  Methods: {result.get('method_count', 0)}")
    else:
        stats = processor.process_all()
        print(f"\n{'='*60}")
        print("Processing Complete")
        print('='*60)
        print(f"Enums processed:   {stats['enums_processed']}")
        print(f"Structs processed: {stats['structs_processed']}")
        print(f"Methods processed: {stats['methods_processed']}")
        if stats['errors']:
            print(f"Errors: {len(stats['errors'])}")
        print('='*60 + "\n")
    
    return 0


if __name__ == "__main__":
    exit(main())
