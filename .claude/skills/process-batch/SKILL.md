---
name: process-batch
description: Orchestrate parallel processing of multiple classes through the modernization pipeline. Use when batch processing classes, coordinating header and method generation, or managing large-scale code modernization tasks.
allowed-tools: Read, Grep, Glob, Bash
---

# Process Batch

Orchestrate parallel processing of multiple classes through the modernization pipeline.

## When to Use

- Processing multiple classes in dependency order
- Coordinating header generation followed by method modernization
- Managing large-scale modernization tasks efficiently
- Resuming interrupted batch processing

## Workflow

```
1. Analyze Dependencies
   └── Use /analyze-deps to determine processing order

2. For each class (in dependency order):
   ├── Generate Header
   │   └── Use /modernize-class
   ├── Save to output/include/{Namespace}/{Class}.h
   ├── Update database (processed_types)
   │
   ├── Modernize Methods
   │   └── Use /modernize-method for each method
   ├── Verify Each Method
   │   └── Use /verify-logic
   ├── Save to output/src/{Namespace}/{Class}.cpp
   └── Update database (processed_methods)

3. Report Results
   └── Summary of processed classes, methods, and any failures
```

## Parallel Processing Strategy

When processing independent classes (no dependencies between them), process in parallel:

```
Batch 1 (parallel):
  - Transform
  - Vector3
  - Quaternion

Batch 2 (parallel, after Batch 1):
  - Component (depends on Transform)
  - Entity (depends on Vector3)

Batch 3 (after Batch 2):
  - Player (depends on Component, Entity)
```

## State Management

Track progress in the database to enable resume:

```sql
-- Check what's already processed
SELECT name FROM processed_types WHERE engine_used = 'claude-code';
SELECT name, parent FROM processed_methods WHERE engine_used = 'claude-code';

-- Find unprocessed classes
SELECT t.name FROM types t
LEFT JOIN processed_types pt ON t.name = pt.name
WHERE pt.name IS NULL AND t.type = 'struct';
```

## Error Handling

### Verification Failures
If a method fails verification:
1. Log the failure with reason
2. Retry with feedback (up to 5 attempts)
3. If still failing, mark with comment and continue
4. Report in final summary

### Missing Dependencies
If a referenced type is not found:
1. Check if it's a system type (int, void, etc.)
2. Check if it's in the ignore list
3. Log warning and continue with available context
4. Add to "needs attention" list in summary

### Cycle Detection
If dependency cycle is found:
1. Log the cycle
2. Use forward declarations for minimal breaking
3. Process remaining types
4. Report cycle in summary

## Progress Reporting

Provide periodic status updates:

```
Processing Batch: 3 of 7
  Classes: 45/120 (37%)
  Methods: 312/890 (35%)
  Current: Player (12 methods)
  Failures: 2 verification, 1 missing type
```

## Resume Support

To resume an interrupted batch:

```bash
python llm_process.py --engine claude-code --resume
```

The processor will:
1. Query processed_types and processed_methods
2. Skip already-processed items
3. Continue from where it left off

## Output Structure

```
output/
├── include/
│   ├── Turbine/
│   │   ├── Player.h
│   │   ├── Entity.h
│   │   └── Component.h
│   └── Client/
│       └── UI/
│           └── Window.h
└── src/
    ├── Turbine/
    │   ├── Player.cpp
    │   ├── Entity.cpp
    │   └── Component.cpp
    └── Client/
        └── UI/
            └── Window.cpp
```

## Final Summary Report

```
Batch Processing Complete
=========================
Total Classes: 120
  Processed: 118
  Skipped: 2 (cycles)

Total Methods: 890
  Processed: 875
  Verified: 870
  Failed: 5

Verification Failures:
  - Player::OnDeath: Missing virtual call
  - Entity::Update: Changed loop condition

Dependency Cycles:
  - A <-> B (resolved with forward declaration)

Time Elapsed: 45m 32s
Average per class: 23s
```
