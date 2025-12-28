"""Unified context builder for C++ code modernization.

This module provides a centralized context-gathering system used by both
the legacy LM Studio pipeline and the Claude Code engine. It aggregates
TypeResolver, ConstantReplacer, and regex-based extraction to produce
structured context for LLM prompts.

The ContextBuilder consolidates functionality previously duplicated in:
- FunctionProcessor (find_type_references, get_reference_context, etc.)
- ClassHeaderGenerator (find_type_references, get_reference_context, etc.)

Key features:
- Unified type reference extraction with configurable patterns
- Programmatic enum value mapping for magic number replacement
- Constant annotation and pre-processing
- Parent class context retrieval
- Structured output via ContextResult dataclass
"""

from __future__ import annotations

import logging
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple, TYPE_CHECKING

if TYPE_CHECKING:
    from .db_handler import DBHandler
    from .type_resolver import TypeResolver, EnumValueInfo, ConstantInfo
    from .constant_replacer import ConstantReplacer

logger = logging.getLogger(__name__)


# Primitive types to exclude from type references
PRIMITIVE_TYPES: Set[str] = {
    'int', 'unsigned', 'char', 'short', 'long', 'float', 'double',
    'void', 'bool', 'size_t', 'uint8_t', 'uint16_t', 'uint32_t',
    'uint64_t', 'int8_t', 'int16_t', 'int32_t', 'int64_t',
    'BYTE', 'WORD', 'DWORD', 'QWORD', 'BOOL', 'HANDLE', 'HRESULT',
    'TRUE', 'FALSE', 'NULL', 'nullptr', 'String', 'Vector',
    'const', 'static', 'virtual', 'override', 'final',
}


@dataclass
class EnumValueMapping:
    """Maps a numeric value to a possible enum constant."""
    value: int
    enum_name: str
    constant_name: str
    hex_value: str  # Formatted hex representation

    @property
    def full_qualified(self) -> str:
        """Get fully qualified enum reference (EnumName::ConstantName)."""
        return f"{self.enum_name}::{self.constant_name}"


@dataclass
class ContextResult:
    """Aggregated context for code modernization.

    This structured result allows engines to selectively use context
    components and format them appropriately for their prompts.
    """
    # Type references found in the code
    type_references: Set[str] = field(default_factory=set)

    # Type definitions keyed by name -> (code, is_processed)
    type_definitions: Dict[str, Tuple[str, bool]] = field(default_factory=dict)

    # Formatted type context string (ready for prompt)
    type_context_str: str = ""

    # Parent class information
    parent_class: Optional[str] = None
    parent_header: Optional[str] = None  # Processed header if available
    parent_is_processed: bool = False

    # Enum value mappings for magic number replacement
    enum_mappings: Dict[int, List[EnumValueMapping]] = field(default_factory=dict)
    enum_context_str: str = ""  # Formatted enum reference

    # Pre-processed code (with constants annotated)
    preprocessed_code: Optional[str] = None

    # Numeric literals found in code
    numeric_literals: Set[int] = field(default_factory=set)

    def to_prompt_sections(self) -> Dict[str, str]:
        """Format context as sections for inclusion in LLM prompt.

        Returns dictionary with keys:
        - 'parent_context': Parent class definition
        - 'type_context': Referenced type definitions
        - 'enum_context': Enum value reference table
        """
        sections = {}

        if self.parent_header:
            status = "modernized" if self.parent_is_processed else "raw"
            sections['parent_context'] = (
                f"// Parent class definition ({status}):\n{self.parent_header}"
            )

        if self.type_context_str:
            sections['type_context'] = self.type_context_str

        if self.enum_context_str:
            sections['enum_context'] = self.enum_context_str

        return sections


class ContextBuilder:
    """Unified context gathering for C++ code modernization.

    Aggregates TypeResolver, ConstantReplacer, and regex-based extraction
    to provide a single interface for both the FunctionProcessor and
    ClaudeCodeEngine to gather context for LLM prompts.

    Example usage:
        builder = ContextBuilder(db_handler, output_dir)

        # For method modernization
        ctx = builder.gather_method_context(
            code=method_definition,
            parent_class="PlayerModule",
            namespace="Turbine"
        )

        # Use context in prompt
        prompt = f'''
        {ctx.to_prompt_sections()['parent_context']}
        {ctx.to_prompt_sections()['type_context']}
        {ctx.to_prompt_sections()['enum_context']}

        Modernize this code:
        {method_definition}
        '''
    """

    # Regex patterns for type reference extraction (from FunctionProcessor)
    TYPE_PATTERNS = [
        # Pointer/Reference types: SomeType*, SomeType&
        r'\b([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s*[*&]',

        # Parameter types: (const Type param), (Type param)
        r'\(\s*(?:[^()]*,\s*)?(const\s+)?([A-Za-z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s+\w+',

        # Variable declarations: Type varname;
        r'^\s*([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s+\w+\s*[;=]',

        # Return types: ReturnType ClassName::Method
        r'^([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s*[*&]?\s+\w+::',

        # Cast expressions: (SomeType*), (Type)
        r'\(\s*([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s*\*?\s*\)',

        # Scoped member access: ClassName::member
        r'([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)::(?:\w+)',

        # Lowercase enum parameters: eCombatMode param
        r'\b(const\s+)?([a-z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s+\w+(?=\s*[),])',
    ]

    def __init__(
        self,
        db: 'DBHandler',
        output_dir: Optional[Path] = None,
        type_resolver: Optional['TypeResolver'] = None,
        constant_replacer: Optional['ConstantReplacer'] = None,
    ):
        """Initialize the context builder.

        Args:
            db: Database handler for type lookups.
            output_dir: Output directory for header paths (default: ./output).
            type_resolver: Optional TypeResolver instance. Created if not provided.
            constant_replacer: Optional ConstantReplacer. Created if not provided.
        """
        self.db = db
        self.output_dir = output_dir or Path("output")

        # Lazy-load components if not provided
        self._type_resolver = type_resolver
        self._constant_replacer = constant_replacer
        self._code_preprocessor = None

        # Compile regex patterns
        self._compiled_patterns = [
            re.compile(p, re.MULTILINE) for p in self.TYPE_PATTERNS
        ]

        logger.debug("ContextBuilder initialized")

    @property
    def type_resolver(self) -> 'TypeResolver':
        """Get or create TypeResolver instance."""
        if self._type_resolver is None:
            from .type_resolver import TypeResolver
            self._type_resolver = TypeResolver(self.db)
        return self._type_resolver

    @property
    def constant_replacer(self) -> 'ConstantReplacer':
        """Get or create ConstantReplacer instance."""
        if self._constant_replacer is None:
            from .constant_replacer import ConstantReplacer
            self._constant_replacer = ConstantReplacer(self.db)
        return self._constant_replacer

    @property
    def code_preprocessor(self) -> 'CodePreprocessor':
        """Get or create CodePreprocessor instance.

        The CodePreprocessor handles:
        - Replacing decompiler types (BOOL -> bool, etc.)
        - Removing calling conventions (__thiscall, etc.)
        - Annotating magic numbers with enum values
        - Annotating known constants
        """
        if self._code_preprocessor is None:
            from .code_preprocessor import CodePreprocessor
            self._code_preprocessor = CodePreprocessor(
                db_handler=self.db,
                type_resolver=self._type_resolver,
                constant_replacer=self._constant_replacer,
            )
        return self._code_preprocessor

    # ═══════════════════════════════════════════════════════════════════════════
    # High-Level Context Gathering
    # ═══════════════════════════════════════════════════════════════════════════

    def gather_method_context(
        self,
        code: str,
        parent_class: Optional[str] = None,
        namespace: Optional[str] = None,
        include_enums: bool = True,
        include_constants: bool = True,
        preprocess_code: bool = True,
        max_types: int = 10,
    ) -> ContextResult:
        """Gather complete context for method modernization.

        This is the main entry point for the FunctionProcessor and
        ClaudeCodeEngine to gather all context needed for modernizing
        a single method.

        Args:
            code: The raw decompiled C++ method code.
            parent_class: Name of the parent class (None for global functions).
            namespace: Optional namespace for the parent class.
            include_enums: Whether to extract enum value mappings.
            include_constants: Whether to annotate constants in code.
            preprocess_code: Whether to run full preprocessing (type replacement,
                calling convention removal, enum annotation). Default True.
            max_types: Maximum number of type definitions to include.

        Returns:
            ContextResult with all gathered context.
        """
        result = ContextResult()

        # 1. Extract type references from code
        result.type_references = self.extract_type_references(code)
        if parent_class:
            result.type_references.discard(parent_class)

        # 2. Look up type definitions
        result.type_definitions = self.lookup_type_definitions(
            result.type_references, max_types=max_types
        )
        result.type_context_str = self.format_type_context(result.type_definitions)

        # 3. Get parent class context
        if parent_class:
            result.parent_class = parent_class
            header, is_processed = self.get_parent_header(parent_class, namespace)
            result.parent_header = header
            result.parent_is_processed = is_processed

        # 4. Extract enum value mappings
        if include_enums:
            result.numeric_literals = self.extract_numeric_literals(code)
            result.enum_mappings = self.map_enum_values(result.numeric_literals)
            result.enum_context_str = self.format_enum_context(result.enum_mappings)

        # 5. Preprocess code - apply all transformations
        if preprocess_code:
            # Use CodePreprocessor for comprehensive preprocessing
            preprocessing_result = self.code_preprocessor.preprocess(
                code,
                replace_types=True,
                remove_calling_conventions=True,
                annotate_enums=include_enums,
                annotate_constants=include_constants,
            )
            result.preprocessed_code = preprocessing_result.code
            result.preprocessing_summary = preprocessing_result.get_summary()
            result.decompiler_types_replaced = preprocessing_result.decompiler_types_replaced
            result.calling_conventions_removed = preprocessing_result.calling_conventions_removed
        elif include_constants:
            # Fallback to just constant annotation
            result.preprocessed_code = self.annotate_constants(code)
        else:
            result.preprocessed_code = code

        logger.debug(
            f"Gathered context: {len(result.type_references)} types, "
            f"{len(result.enum_mappings)} enum mappings"
            + (f", preprocessing: {result.preprocessing_summary}" if result.preprocessing_summary else "")
        )

        return result

    def gather_header_context(
        self,
        class_name: str,
        struct_code: str,
        methods: Optional[List[Tuple]] = None,
        max_types: int = 10,
    ) -> ContextResult:
        """Gather context for header generation.

        This is used by ClassHeaderGenerator to gather context needed
        for generating a modern C++ header file.

        Args:
            class_name: Name of the class to generate header for.
            struct_code: Raw struct definition code.
            methods: List of method row tuples from database.
            max_types: Maximum number of type definitions to include.

        Returns:
            ContextResult with gathered context.
        """
        result = ContextResult()

        # 1. Extract type references from struct and methods
        result.type_references = self.extract_type_references(struct_code)

        if methods:
            for method_row in methods:
                definition = method_row[3] if len(method_row) > 3 else ""
                refs = self.extract_type_references(definition)
                result.type_references.update(refs)

        # Remove self-reference
        result.type_references.discard(class_name)
        # Remove template parameters if class is templated
        if '<' in class_name:
            base_name = class_name.split('<')[0]
            result.type_references.discard(base_name)

        # 2. Look up type definitions
        result.type_definitions = self.lookup_type_definitions(
            result.type_references, max_types=max_types
        )
        result.type_context_str = self.format_type_context(result.type_definitions)

        logger.debug(
            f"Gathered header context: {len(result.type_references)} types"
        )

        return result

    # ═══════════════════════════════════════════════════════════════════════════
    # Type Reference Extraction
    # ═══════════════════════════════════════════════════════════════════════════

    def extract_type_references(
        self,
        code: str,
        mode: str = "precise",
    ) -> Set[str]:
        """Extract type names referenced in code.

        Args:
            code: C++ code to analyze.
            mode: Extraction mode - "precise" uses multiple patterns,
                  "broad" uses simple capitalized identifier matching.

        Returns:
            Set of type names found in code.
        """
        if mode == "broad":
            return self._extract_types_broad(code)
        return self._extract_types_precise(code)

    def _extract_types_precise(self, code: str) -> Set[str]:
        """Extract types using precise regex patterns."""
        types: Set[str] = set()

        for pattern in self._compiled_patterns:
            matches = pattern.findall(code)
            for match in matches:
                # Handle tuple matches (from regex groups)
                if isinstance(match, tuple):
                    # Get last non-empty group
                    type_name = next(
                        (m for m in reversed(match) if m and m.strip()),
                        None
                    )
                else:
                    type_name = match

                if type_name and self._is_valid_type_ref(type_name):
                    types.add(type_name.strip())

        return types

    def _extract_types_broad(self, code: str) -> Set[str]:
        """Extract types using broad capitalized identifier matching."""
        pattern = r'\b([A-Z][A-Za-z0-9_]*)\b'
        matches = re.findall(pattern, code)
        return {m for m in matches if self._is_valid_type_ref(m)}

    def _is_valid_type_ref(self, name: str) -> bool:
        """Check if a name is a valid type reference."""
        if not name or name in PRIMITIVE_TYPES:
            return False

        # Must start with uppercase or lowercase 'e' (enum convention)
        if not (name[0].isupper() or name.startswith('e')):
            # Unless it's namespace-qualified
            if '::' not in name:
                return False

        return True

    # ═══════════════════════════════════════════════════════════════════════════
    # Type Definition Lookup
    # ═══════════════════════════════════════════════════════════════════════════

    def lookup_type_definitions(
        self,
        type_names: Set[str],
        max_types: int = 10,
    ) -> Dict[str, Tuple[str, bool]]:
        """Look up definitions for given types from database.

        Args:
            type_names: Set of type names to look up.
            max_types: Maximum number to fetch (for token budget).

        Returns:
            Dict mapping type name to (definition_code, is_processed).
        """
        definitions: Dict[str, Tuple[str, bool]] = {}

        for name in sorted(type_names)[:max_types]:
            try:
                type_data, is_processed = self.db.get_type_with_fallback(name)
                if type_data:
                    if is_processed:
                        code = type_data.get('processed_header', '')
                    else:
                        # Raw type tuple: code is at index 5
                        code = type_data[5] if len(type_data) > 5 else ""

                    if code:
                        definitions[name] = (code, is_processed)
            except Exception as e:
                logger.warning(f"Failed to look up type {name}: {e}")

        return definitions

    def format_type_context(
        self,
        type_definitions: Dict[str, Tuple[str, bool]],
    ) -> str:
        """Format type definitions as context string for prompt.

        Args:
            type_definitions: Dict from lookup_type_definitions().

        Returns:
            Formatted multi-line string with type definitions.
        """
        if not type_definitions:
            return ""

        sections = []
        for name, (code, is_processed) in sorted(type_definitions.items()):
            status = "modernized" if is_processed else "raw decompiled"
            header_path = self._compute_header_path(name)

            section = (
                f"// Reference: {name} ({status})\n"
                f"// Defined in: \"{header_path}\"\n"
                f"{code}"
            )
            sections.append(section)

        return "\n\n".join(sections)

    def _compute_header_path(self, type_name: str) -> str:
        """Compute expected header file path for a type."""
        # Handle namespace::Type format
        if '::' in type_name:
            parts = type_name.split('::')
            # Use first namespace and last name
            namespace = parts[0]
            simple_name = parts[-1]
            return f"{namespace}/{simple_name}.h"
        return f"{type_name}.h"

    # ═══════════════════════════════════════════════════════════════════════════
    # Parent Class Context
    # ═══════════════════════════════════════════════════════════════════════════

    def get_parent_header(
        self,
        parent_class: str,
        namespace: Optional[str] = None,
    ) -> Tuple[Optional[str], bool]:
        """Get the header definition for a parent class.

        Args:
            parent_class: Name of the parent class.
            namespace: Optional namespace to try qualified lookup.

        Returns:
            Tuple of (header_code, is_processed). Returns (None, False) if not found.
        """
        # Try with namespace first
        if namespace:
            qualified = f"{namespace}::{parent_class}"
            result = self._lookup_single_type(qualified)
            if result[0]:
                return result

        # Try plain name
        return self._lookup_single_type(parent_class)

    def _lookup_single_type(self, name: str) -> Tuple[Optional[str], bool]:
        """Look up a single type definition."""
        try:
            type_data, is_processed = self.db.get_type_with_fallback(name)
            if type_data:
                if is_processed:
                    return type_data.get('processed_header'), True
                else:
                    code = type_data[5] if len(type_data) > 5 else None
                    return code, False
        except Exception as e:
            logger.warning(f"Failed to look up {name}: {e}")

        return None, False

    # ═══════════════════════════════════════════════════════════════════════════
    # Enum Value Mapping
    # ═══════════════════════════════════════════════════════════════════════════

    def extract_numeric_literals(self, code: str) -> Set[int]:
        """Extract numeric literals from code.

        Finds both hex (0x...) and decimal literals, excluding
        common non-enum values like 0, 1, 2, -1.

        Args:
            code: C++ code to analyze.

        Returns:
            Set of integer values found.
        """
        values: Set[int] = set()

        # Extract hex literals
        hex_pattern = r'0[xX][0-9a-fA-F]+'
        for match in re.findall(hex_pattern, code):
            try:
                values.add(int(match, 16))
            except ValueError:
                pass

        # Extract decimal literals (avoiding parts of identifiers)
        dec_pattern = r'(?<![0-9a-fA-Zx])\d+(?![0-9a-fA-Zx])'
        for match in re.findall(dec_pattern, code):
            try:
                val = int(match)
                values.add(val)
            except ValueError:
                pass

        # Filter out common non-enum values
        values -= {0, 1, 2, -1}

        return values

    def map_enum_values(
        self,
        values: Set[int],
    ) -> Dict[int, List[EnumValueMapping]]:
        """Map numeric values to possible enum constants.

        Args:
            values: Set of numeric values to map.

        Returns:
            Dict mapping value to list of possible enum mappings.
        """
        if not values:
            return {}

        mappings: Dict[int, List[EnumValueMapping]] = {}

        # Get all enums from database
        try:
            enums = self.db.get_enums()
        except Exception as e:
            logger.warning(f"Failed to get enums: {e}")
            return {}

        # Parse each enum's definition for values
        enum_value_pattern = re.compile(r'(\w+)\s*=\s*(0[xX][0-9a-fA-F]+|\d+)')

        for enum_row in enums:
            enum_name = enum_row[2] if len(enum_row) > 2 else ""
            enum_code = enum_row[5] if len(enum_row) > 5 else ""

            if not enum_name or not enum_code:
                continue

            # Find all value assignments in enum
            for match in enum_value_pattern.finditer(enum_code):
                const_name = match.group(1)
                value_str = match.group(2)

                try:
                    if value_str.lower().startswith('0x'):
                        val = int(value_str, 16)
                    else:
                        val = int(value_str)

                    # Check if this value is in our target set
                    if val in values:
                        mapping = EnumValueMapping(
                            value=val,
                            enum_name=enum_name,
                            constant_name=const_name,
                            hex_value=f"0x{val:x}",
                        )

                        if val not in mappings:
                            mappings[val] = []
                        mappings[val].append(mapping)

                except ValueError:
                    pass

        return mappings

    def format_enum_context(
        self,
        mappings: Dict[int, List[EnumValueMapping]],
    ) -> str:
        """Format enum value mappings as context for prompt.

        Args:
            mappings: Dict from map_enum_values().

        Returns:
            Formatted reference table for LLM prompt.
        """
        if not mappings:
            return ""

        lines = ["Enum Value Reference (use these instead of magic numbers):"]

        for value in sorted(mappings.keys()):
            for mapping in mappings[value]:
                lines.append(
                    f"  {value} ({mapping.hex_value}) -> "
                    f"{mapping.full_qualified}"
                )

        return "\n".join(lines)

    # ═══════════════════════════════════════════════════════════════════════════
    # Constant Annotation
    # ═══════════════════════════════════════════════════════════════════════════

    def annotate_constants(self, code: str) -> str:
        """Annotate known constants in code with inline comments.

        Uses the ConstantReplacer to add type and value information
        to recognized constants.

        Args:
            code: Raw C++ code.

        Returns:
            Code with constant annotations.
        """
        return self.constant_replacer.process_code(code)

    # ═══════════════════════════════════════════════════════════════════════════
    # Utility Methods
    # ═══════════════════════════════════════════════════════════════════════════

    def is_template_instantiation(self, type_name: str) -> bool:
        """Check if a type name is a template instantiation."""
        return '<' in type_name and type_name.endswith('>')

    def get_template_base_name(self, type_name: str) -> str:
        """Get base name of a template type (strip parameters)."""
        if '<' in type_name:
            return type_name.split('<')[0].strip()
        return type_name

    def refresh_caches(self) -> None:
        """Refresh internal caches after database updates."""
        if self._type_resolver:
            self._type_resolver.refresh_processed_types()
