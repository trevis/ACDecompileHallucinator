# Modernization Workflow

## Processing Pipeline

```
1. Preprocessing (process.py)
   └── Parse acclient.h/c → types.db

2. LLM Processing (llm_process.py --engine <engine>)
   ├── Select engine (claude-code or lm-studio)
   ├── Load class from database
   ├── Analyze dependencies
   ├── Generate modern header
   ├── Process each method
   ├── Verify logic preservation
   └── Write output files

3. Verification
   ├── Tier 1: Syntax validation
   ├── Tier 2: Control flow matching
   └── Tier 3: Semantic equivalence
```

## Engine Selection

```bash
# Claude Code with skills (recommended)
python llm_process.py --engine claude-code --class ClassName

# LM Studio local LLM
python llm_process.py --engine lm-studio --class ClassName
```

## Class Processing Order

1. **Dependency Analysis**: Topological sort based on type references
2. **Level Processing**: Process base classes before derived
3. **Method Processing**: All methods of a class before moving to next

## Retry Strategy

```
Attempt 1: Standard generation
Attempt 2: Specific fix for syntax errors
Attempt 3: Minimal diff approach for logic issues
Attempt 4: Conservative transformation
Attempt 5: Fallback with explicit constraints
```

## State Tracking

- `types.db`: Raw parsed data and processing state
- `processed_types`: Track which types have been modernized (includes engine_used)
- `processed_methods`: Track which methods have been modernized (includes engine_used)
- `llm_cache.db`: Cache LLM responses to avoid re-processing
