---
paths: "**/*.py"
---
# Python Code Style

## Requirements
- Python 3.10+ required
- Type hints for all function parameters and return types
- Dataclasses for all data entities
- Docstrings for public functions (Google style)

## Conventions
- Use `logging` module, not `print()` for output
- Prefer `pathlib.Path` over `os.path`
- Use context managers for file operations
- Constants in UPPER_SNAKE_CASE

## Imports
```python
# Standard library first
import logging
from pathlib import Path
from typing import Optional, List, Dict

# Third-party
from tqdm import tqdm

# Local
from code_parser.db_handler import DatabaseHandler
```

## Error Handling
- Use specific exception types, not bare `except:`
- Log errors with context before re-raising
- Create custom exceptions for domain-specific errors
