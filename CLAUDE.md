# CLAUDE.md

C++ code modernization pipeline for Asheron's Call client decompilation.

## Quick Start

```bash
# Parse decompiled files into database
python process.py parse --header acclient/acclient.h --source acclient/acclient.c

# Process with Claude Code engine (recommended)
python llm_process.py --engine claude-code --class AC1ClientChatManager

# Process with LM Studio (requires local server at localhost:1234)
python llm_process.py --engine lm-studio

# Check progress
python process.py stats
```

## Project Structure

```
.
├── process.py              # Preprocessing CLI
├── llm_process.py          # LLM orchestration (multi-engine)
├── code_parser/
│   ├── llm_processor.py    # Main orchestrator
│   ├── header_parser.py    # Parse acclient.h
│   ├── source_parser.py    # Parse acclient.c
│   ├── db_handler.py       # SQLite operations
│   ├── dependency_analyzer.py  # Topological sort
│   ├── class_header_generator.py
│   ├── function_processor.py
│   ├── class_assembler.py
│   ├── type_resolver.py    # Programmatic type extraction
│   └── exceptions.py       # Exception hierarchy
├── engines/
│   ├── __init__.py         # Public API exports
│   ├── base.py             # Abstract LLMEngine interface
│   ├── lm_studio.py        # LM Studio OpenAI-compatible backend
│   ├── claude_code.py      # Claude Code CLI with skills
│   └── registry.py         # Engine discovery and registration
├── .claude/
│   ├── rules/              # Modular instructions
│   │   ├── python-style.md
│   │   ├── cpp-output.md
│   │   ├── workflow.md
│   │   └── database.md
│   ├── skills/             # Claude Code skills
│   │   ├── analyze-deps/
│   │   ├── modernize-class/
│   │   ├── modernize-method/
│   │   ├── process-batch/
│   │   └── verify-logic/
│   └── settings.json       # Project permissions
└── output/                 # Generated C++ code
    ├── include/{Namespace}/{Class}.h
    └── src/{Namespace}/{Class}.cpp
```

## Engine Architecture

The project supports multiple LLM backends via a pluggable engine system:

```bash
# Claude Code (uses skills from .claude/skills/)
python llm_process.py --engine claude-code --class PlayerModule

# LM Studio (local LLM via OpenAI-compatible API)
python llm_process.py --engine lm-studio --lm-studio-url http://localhost:1234/v1
```

### Available Engines

| Engine | Description | Requirements |
|--------|-------------|--------------|
| `claude-code` | Claude Code CLI with skills integration | `claude` CLI installed |
| `lm-studio` | Local LLM via OpenAI-compatible API | LM Studio running |

### Claude Code Skills

Skills provide structured prompts for specific modernization tasks:

| Skill | Purpose |
|-------|---------|
| `modernize-class` | Transform decompiled classes to modern C++ headers |
| `modernize-method` | Modernize individual functions to C++17+ |
| `analyze-deps` | Analyze class dependencies |
| `verify-logic` | Verify semantic equivalence |
| `process-batch` | Batch processing orchestration |

## Migration Status (Complete)

| Phase | Description | Status |
|-------|-------------|--------|
| 0 | Engine Abstraction | Complete |
| 1 | Engine Tracking | Complete |
| 2 | Type Hints & TypeResolver | Complete |
| 3 | Claude Code Skills | Complete |

All phases have been implemented and merged. See [HYBRID_MIGRATION_PLAN.md](HYBRID_MIGRATION_PLAN.md) for details.

## Key Files

- **Input**: `acclient/acclient.h`, `acclient/acclient.c`, `acclient/acclient.txt` (constants)
- **Database**: `mcp-sources/types.db` (parsed types, methods, processing state)
- **Cache**: `mcp-sources/llm_cache.db` (LLM response cache)

## Rules Reference

Detailed instructions in `.claude/rules/`:
- [python-style.md](.claude/rules/python-style.md) - Python conventions
- [cpp-output.md](.claude/rules/cpp-output.md) - C++ generation rules
- [workflow.md](.claude/rules/workflow.md) - Processing pipeline
- [database.md](.claude/rules/database.md) - Database schema

## Database Schema

### Core Tables

```sql
-- Parsed types from acclient.h
types(id, kind, name, code, namespace, parent, is_template)

-- Parsed methods from acclient.c
methods(id, name, definition, parent, offset, is_virtual)

-- Processing state
processed_types(name, processed_header, processed_at, engine_used)
processed_methods(id, parent, name, processed_code, confidence, engine_used)

-- Named constants
constants(name, value, type)
```

## CLI Reference

### llm_process.py

```bash
python llm_process.py [OPTIONS]

Options:
  --db PATH              Path to types.db (default: mcp-sources/types.db)
  --output PATH          Output directory (default: ./output)
  --engine ENGINE        LLM engine: claude-code, lm-studio (default: lm-studio)
  --class NAME           Process single class only
  --parallel N           Parallel workers (1=sequential, >1=parallel by dependency level)
  --debug                Enable debug output
  --dry-run              Show plan without processing
  --force                Reprocess even if already done
  --verbose              Verbose logging
  --no-skills            Disable skill-based prompt enhancement
```

### Parallel Processing

Types are grouped by dependency level. Types at the same level have no inter-dependencies
and can be processed concurrently:

```bash
# Process with 4 parallel workers
python llm_process.py --engine claude-code --parallel 4
```

### process.py

```bash
python process.py COMMAND [OPTIONS]

Commands:
  parse      Parse acclient.h/c into database
  stats      Show database statistics
  list-classes  List all classes with method counts
```

## Commit Guidelines

- Conventional commits: `type: description`
- Types: feat, fix, refactor, docs, test, chore
- No AI attribution in commit messages
