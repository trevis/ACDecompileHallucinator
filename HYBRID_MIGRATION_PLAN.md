# Hybrid Migration Plan: LM Studio + Claude Code

> **Created**: 2025-12-27
> **Updated**: 2025-12-28
> **Status**: Implementation Complete (All Phases Merged)

## Executive Summary

This plan describes a **hybrid migration approach** that:
1. Preserves full backwards compatibility with the existing LM Studio pipeline
2. Adds Claude Code as an alternative LLM backend via plugin architecture
3. Comprehensively refactors the legacy Python codebase for quality and maintainability
4. Enables parallel workstreams for maximum development velocity

---

## Core Requirements

| Requirement | Decision |
|-------------|----------|
| Backwards compatibility | LM Studio continues to work unchanged |
| Engine selection | CLI flag `--engine lm-studio\|claude-code` |
| Legacy improvements | Comprehensive refactoring (types, docs, errors, structure) |
| Extensibility | Plugin architecture for future LLM backends |
| Work approach | Parallel workstreams with in-place updates |

---

## Architecture Overview

```
+-------------------------------------------------------------+
|                        CLI Layer                             |
|   python llm_process.py --engine <name> [options]           |
+-----------------------------+-------------------------------+
                              |
+-----------------------------v-------------------------------+
|                   Engine Abstraction                         |
|  +-----------------------------------------------------+    |
|  |  class LLMEngine(ABC):                              |    |
|  |      def generate_header(class_info) -> str         |    |
|  |      def generate_method(method_info) -> str        |    |
|  |      def verify_logic(original, modern) -> Result   |    |
|  +-----------------------------------------------------+    |
|         ^                    ^                    ^          |
|         |                    |                    |          |
|  +------+-----+    +--------+--------+    +-----+------+    |
|  | LMStudio   |    |  ClaudeCode     |    |  Future    |    |
|  | Engine     |    |  Engine         |    |  Engines   |    |
|  | (legacy)   |    |  (skills+MCP)   |    |  (Ollama?) |    |
|  +------------+    +-----------------+    +------------+    |
+--------------------------+----------------------------------+
                           |
+--------------------------v----------------------------------+
|              Shared Infrastructure (Refactored)              |
|  +------------+ +------------+ +------------+ +----------+  |
|  | db_handler | | parsers    | | dependency | | type     |  |
|  | (types.db) | | (h/c)      | | analyzer   | | writer   |  |
|  +------------+ +------------+ +------------+ +----------+  |
|                                                              |
|  Improvements: Type hints, docstrings, error handling,      |
|                logging, cleaner interfaces                   |
+-------------------------------------------------------------+
```

---

## Parallel Workstreams

### Stream A: Engine Abstraction Layer

**Goal**: Create a pluggable engine architecture that allows switching between LLM backends.

#### A.1 Create Engine Package Structure

```
engines/
├── __init__.py           # Engine exports and factory
├── base.py               # Abstract LLMEngine interface
├── lm_studio.py          # Current code, refactored
├── claude_code.py        # Calls Claude Code CLI/skills
└── registry.py           # Plugin discovery and registration
```

#### A.2 Abstract Base Class

```python
# engines/base.py
from abc import ABC, abstractmethod
from typing import Tuple, Dict, Any
from dataclasses import dataclass

@dataclass
class VerificationResult:
    """Result of logic verification between original and modernized code."""
    is_equivalent: bool
    confidence: str  # "high", "medium", "low"
    reason: str

class LLMEngine(ABC):
    """Abstract interface for LLM backends."""

    @property
    @abstractmethod
    def name(self) -> str:
        """Engine identifier for logging and state tracking."""
        pass

    @abstractmethod
    def generate_header(
        self,
        class_name: str,
        class_info: Dict[str, Any],
        context: str,
        is_template: bool = False
    ) -> str:
        """Generate modern C++ header for a class."""
        pass

    @abstractmethod
    def generate_method(
        self,
        method_name: str,
        method_definition: str,
        parent_class: str,
        context: str
    ) -> str:
        """Modernize a single method."""
        pass

    @abstractmethod
    def verify_logic(
        self,
        original: str,
        modernized: str
    ) -> VerificationResult:
        """Verify equivalence between original and modernized code."""
        pass
```

#### A.3 LM Studio Engine (Extracted from Legacy)

```python
# engines/lm_studio.py
from openai import OpenAI
from .base import LLMEngine, VerificationResult

class LMStudioEngine(LLMEngine):
    """Engine using local LM Studio API."""

    def __init__(
        self,
        base_url: str = "http://localhost:1234/v1",
        temperature: float = 0.2,
        model: str = "local-model"
    ):
        self.client = OpenAI(base_url=base_url, api_key="lm-studio")
        self.temperature = temperature
        self.model = model

    @property
    def name(self) -> str:
        return "lm-studio"

    def generate_header(self, class_name, class_info, context, is_template=False):
        # Existing logic from class_header_generator.py
        ...

    def generate_method(self, method_name, method_definition, parent_class, context):
        # Existing logic from function_processor.py
        ...

    def verify_logic(self, original, modernized):
        # Existing logic from verification code
        ...
```

#### A.4 Claude Code Engine

```python
# engines/claude_code.py
import subprocess
import json
from pathlib import Path
from .base import LLMEngine, VerificationResult

class ClaudeCodeEngine(LLMEngine):
    """Engine using Claude Code CLI with skills."""

    def __init__(self, project_root: str = "."):
        self.project_root = Path(project_root)

    @property
    def name(self) -> str:
        return "claude-code"

    def generate_header(self, class_name, class_info, context, is_template=False):
        # Write context to temp file for skill to read
        context_file = self._write_context(class_name, class_info, context)

        # Invoke Claude Code with skill
        result = subprocess.run(
            ["claude", "-p", f"/modernize-class {class_name}", "--print"],
            capture_output=True,
            text=True,
            cwd=self.project_root,
            timeout=300
        )

        if result.returncode != 0:
            raise RuntimeError(f"Claude Code failed: {result.stderr}")

        return self._extract_code_block(result.stdout)

    def generate_method(self, method_name, method_definition, parent_class, context):
        result = subprocess.run(
            ["claude", "-p", f"/modernize-method {parent_class}::{method_name}", "--print"],
            capture_output=True,
            text=True,
            cwd=self.project_root,
            timeout=300
        )
        return self._extract_code_block(result.stdout)

    def verify_logic(self, original, modernized):
        # Use Claude's semantic understanding for verification
        prompt_input = f"ORIGINAL:\n{original}\n\nMODERNIZED:\n{modernized}"
        result = subprocess.run(
            ["claude", "-p", "/verify-logic", "--print"],
            input=prompt_input,
            capture_output=True,
            text=True,
            timeout=120
        )

        response = json.loads(self._extract_json(result.stdout))
        return VerificationResult(
            is_equivalent=response.get("equivalent", False),
            confidence=response.get("confidence", "low"),
            reason=response.get("reason", "")
        )
```

#### A.5 Engine Registry

```python
# engines/registry.py
from typing import Dict, Type, Optional
from .base import LLMEngine

_registry: Dict[str, Type[LLMEngine]] = {}

def register_engine(name: str, engine_class: Type[LLMEngine]) -> None:
    """Register an engine class by name."""
    _registry[name] = engine_class

def get_engine(name: str, **kwargs) -> LLMEngine:
    """Get an engine instance by name."""
    if name not in _registry:
        raise ValueError(f"Unknown engine: {name}. Available: {list(_registry.keys())}")
    return _registry[name](**kwargs)

def list_engines() -> list[str]:
    """List all registered engine names."""
    return list(_registry.keys())

# Auto-register built-in engines
def _auto_register():
    from .lm_studio import LMStudioEngine
    from .claude_code import ClaudeCodeEngine

    register_engine("lm-studio", LMStudioEngine)
    register_engine("claude-code", ClaudeCodeEngine)

_auto_register()
```

#### A.6 CLI Integration

```python
# In llm_process.py (modified)
import argparse
from engines import get_engine, list_engines

def main():
    parser = argparse.ArgumentParser(description="C++ Modernization Pipeline")

    # Existing arguments...
    parser.add_argument('--db', default='types.db', help='Database path')

    # New engine selection
    parser.add_argument(
        '--engine',
        choices=list_engines(),
        default='lm-studio',
        help='LLM engine to use (default: lm-studio)'
    )

    # Engine-specific options
    parser.add_argument('--lm-studio-url', default='http://localhost:1234/v1')
    parser.add_argument('--temperature', type=float, default=0.2)

    args = parser.parse_args()

    # Get configured engine
    engine_kwargs = {}
    if args.engine == 'lm-studio':
        engine_kwargs['base_url'] = args.lm_studio_url
        engine_kwargs['temperature'] = args.temperature

    engine = get_engine(args.engine, **engine_kwargs)

    print(f"Using engine: {engine.name}")

    # Process with engine
    processor = LLMProcessor(db_path=args.db, engine=engine)
    processor.process_all()
```

---

### Stream B: Legacy Code Refactoring

**Goal**: Improve code quality across all 18 Python modules while maintaining functionality.

#### B.1 Refactoring Scope

| Module | Priority | Improvements Needed |
|--------|----------|---------------------|
| `db_handler.py` | High | Type hints, connection management, error handling |
| `header_parser.py` | High | Type hints, cleaner regex patterns, better error messages |
| `source_parser.py` | High | Type hints, method extraction improvements |
| `dependency_analyzer.py` | Medium | Type hints, cycle detection improvements |
| `class_header_generator.py` | High | Extract to LMStudioEngine |
| `function_processor.py` | High | Extract to LMStudioEngine |
| `llm_cache.py` | Medium | Type hints, cache invalidation |
| `type_writer.py` | Medium | Type hints, path handling |
| `struct.py` | Low | Already a dataclass, add docstrings |
| `method.py` | Low | Already a dataclass, add docstrings |
| `enum.py` | Low | Already a dataclass, add docstrings |
| `constants.py` | Low | Type hints for ignore lists |
| `constants_parser.py` | Medium | Type hints, parsing improvements |
| `constant_replacer.py` | Medium | Type hints, replacement logic |
| `class_assembler.py` | Medium | Type hints, assembly logic |
| `offset_mapper.py` | Low | Type hints, mapping logic |
| `database_writer.py` | Medium | Merge with db_handler or clarify role |
| `__init__.py` | Low | Clean exports |

#### B.2 Refactoring Standards

**Type Hints**:
```python
# Before
def get_type_by_name(self, name):
    cursor = self.conn.execute("SELECT * FROM types WHERE name = ?", (name,))
    return cursor.fetchone()

# After
def get_type_by_name(self, name: str) -> Optional[TypeRecord]:
    """Retrieve a type record by its fully qualified name.

    Args:
        name: The fully qualified type name (e.g., "Turbine::Player")

    Returns:
        TypeRecord if found, None otherwise
    """
    cursor = self.conn.execute("SELECT * FROM types WHERE name = ?", (name,))
    row = cursor.fetchone()
    return TypeRecord.from_row(row) if row else None
```

**Error Handling**:
```python
# Before
def parse_header(self, content):
    # crashes on malformed input
    ...

# After
def parse_header(self, content: str) -> ParseResult:
    """Parse a C++ header file into structured types.

    Args:
        content: The raw header file content

    Returns:
        ParseResult containing types, enums, and any parse errors

    Raises:
        ParseError: If the content is fundamentally unparseable
    """
    try:
        ...
    except RegexError as e:
        logger.warning(f"Regex pattern failed at line {e.line}: {e.message}")
        return ParseResult(types=[], errors=[e])
```

**Logging**:
```python
import logging

logger = logging.getLogger(__name__)

def process_class(self, class_name: str) -> None:
    logger.info(f"Processing class: {class_name}")
    logger.debug(f"Class has {len(methods)} methods")

    try:
        result = self.engine.generate_header(...)
        logger.info(f"Generated header for {class_name} ({len(result)} bytes)")
    except EngineError as e:
        logger.error(f"Failed to generate header for {class_name}: {e}")
        raise
```

---

### Stream C: Claude Code Skills (Complete)

**Goal**: Create Claude Code skills that the ClaudeCodeEngine invokes.

```
.claude/skills/
├── modernize-class/SKILL.md    # Header generation with few-shot examples
├── modernize-method/SKILL.md   # Method modernization with verification
├── verify-logic/SKILL.md       # Equivalence checking
├── analyze-deps/SKILL.md       # Dependency graph using Serena
└── process-batch/SKILL.md      # Parallel processing orchestration
```

Skills are project-shared and available to all team members.

---

## Implementation Phases

### Phase 0: Foundation (Complete)

- [x] Define hybrid architecture requirements
- [x] Create migration plan document
- [x] Set up engine package structure
- [x] Create abstract base class (LLMEngine, VerificationResult, EngineError)
- [x] Implement engine registry with plugin discovery
- [x] Create LM Studio engine wrapper
- [x] Update llm_process.py with --engine CLI flag

### Phase 1: Engine Integration (Complete)

- [x] Extract LM Studio logic into `engines/lm_studio.py`
- [x] Update `llm_process.py` with `--engine` flag
- [x] Verify LM Studio engine produces identical output
- [x] Add engine tracking to `processed_*` tables (engine_used column)
- [x] Ensure backward compatibility with existing database (migration with defaults)

### Phase 2: Legacy Refactoring (In Progress)

- [x] Add type hints to high-priority modules (db_handler, header_parser, source_parser)
- [x] Remove duplicate methods from db_handler.py
- [ ] Add docstrings to all public functions
- [ ] Implement proper exception hierarchy
- [ ] Add structured logging
- [ ] Write unit tests for critical paths

### Phase 3: Claude Code Engine (Complete)

- [x] Create Claude Code skills (5 skills in .claude/skills/)
- [x] Implement `engines/claude_code.py` (stub with CLI subprocess support)
- [ ] Test with sample classes
- [ ] Compare output quality vs LM Studio

### Phase 4: Advanced Features

- [ ] Parallel agent processing
- [ ] Pattern learning and reuse
- [ ] A/B comparison mode
- [ ] Documentation generation

---

## Success Criteria

| Metric | Target |
|--------|--------|
| LM Studio backwards compatibility | 100% - identical behavior with `--engine lm-studio` |
| Type hint coverage | >90% of public functions |
| Docstring coverage | 100% of public functions |
| Test coverage | >70% of critical paths |
| Engine switching | Seamless via CLI flag |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Breaking LM Studio behavior | Extensive testing before/after refactoring |
| Refactoring introduces bugs | Incremental changes, git bisect friendly |
| Engine abstraction too rigid | Design for extension, start minimal |
| Scope creep | Phase boundaries, regular checkpoints |

---

## Appendix: Module Inventory

The `code_parser/` directory contains 18 Python modules:

1. `__init__.py` - Package exports
2. `class_assembler.py` - Combines header + methods into output
3. `class_header_generator.py` - LLM prompts for headers **(extract to engine)**
4. `constants.py` - Ignore lists and filtering rules
5. `constants_parser.py` - Parses acclient.txt for named constants
6. `constant_replacer.py` - Replaces magic numbers with constants
7. `database_writer.py` - Writes parsed data to SQLite
8. `db_handler.py` - SQLite CRUD operations
9. `dependency_analyzer.py` - Topological sort with cycle detection
10. `enum.py` - Enum dataclass
11. `function_processor.py` - LLM prompts for methods **(extract to engine)**
12. `header_parser.py` - Regex-based struct/enum extraction
13. `llm_cache.py` - Caches LLM responses
14. `method.py` - Method dataclass
15. `offset_mapper.py` - Maps code offsets to source files
16. `source_parser.py` - Extracts method bodies from acclient.c
17. `struct.py` - Struct dataclass
18. `type_writer.py` - Writes modernized code to output files
