---
name: analyze-deps
description: Analyze class dependencies and generate processing order using topological sort. Use when determining build order, finding dependency cycles, or understanding class relationships in the decompiled codebase.
allowed-tools: Read, Grep, Glob
---

# Analyze Dependencies

Analyze type dependencies in the decompiled codebase to determine optimal processing order.

## When to Use

- Before processing a set of classes to determine order
- To find dependency cycles that need breaking
- To understand inheritance and composition relationships
- To generate include graphs for the modernized headers

## Instructions

1. Start from a root class or set of classes
2. Extract all type references from each class:
   - Base class (inheritance)
   - Member variable types
   - Method parameter/return types
   - Nested type references
3. Build a dependency graph
4. Perform topological sort
5. Report any cycles found

## Type Reference Extraction

Look for these patterns in class definitions:

```cpp
// Base class
struct __cppobj Player : GameObject  // -> depends on GameObject

// Member types
Vector3 _position;                    // -> depends on Vector3
SmartPointer<Item> _inventory;        // -> depends on SmartPointer, Item

// Method signatures
void SetTarget(Entity* target);       // -> depends on Entity
Animation* GetAnimation();            // -> depends on Animation
```

## Output Format

### Dependency Graph
```
Player
  -> GameObject (base)
  -> Vector3 (member)
  -> Entity (method param)
  -> Animation (method return)

GameObject
  -> Component (base)
  -> Transform (member)
```

### Processing Order
```
1. Transform (no dependencies)
2. Component (no dependencies)
3. Vector3 (no dependencies)
4. Entity (no dependencies)
5. Animation (no dependencies)
6. GameObject (depends on: Component, Transform)
7. Player (depends on: GameObject, Vector3, Entity, Animation)
```

### Cycle Detection
```
CYCLE DETECTED:
  A -> B -> C -> A

Suggestion: Break cycle by forward-declaring the least complex type
in the cycle and using a pointer instead of value type.
```

## Integration with Serena MCP

When available, use Serena's semantic tools for accurate dependency analysis:

- `find_symbol` - Locate class definitions
- `find_referencing_symbols` - Find where types are used
- `get_symbols_overview` - Understand file structure

## Database Queries

Dependencies can be extracted from the types.db:

```sql
-- Get all types
SELECT name, code, parent FROM types WHERE type = 'struct';

-- Get processed types (already have their dependencies resolved)
SELECT name, dependencies FROM processed_types;
```

## Special Cases

### Template Dependencies
Template instantiations depend on both the template and type arguments:
```cpp
List<Player>  // -> depends on List, Player
```

### Nested Types
Nested types should be processed with their parent:
```cpp
Player::Inventory  // Process as part of Player
```

### Forward Declarations
If a cycle cannot be broken, forward declarations are acceptable:
```cpp
class Entity;  // Forward declare
class Player {
    Entity* _target;  // Use pointer, not value
};
```
