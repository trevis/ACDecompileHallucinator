---
name: modernize-class
description: Generate modern C++ headers from decompiled class definitions. Use when modernizing struct definitions, converting decompiled classes to clean C++17+ headers, or generating class headers with proper includes and member declarations.
allowed-tools: Read, Grep, Glob
---

# Modernize Class Header

Generate clean, modern C++ header files from decompiled class/struct definitions.

## When to Use

- Converting decompiled `struct __cppobj` definitions to modern C++ classes
- Generating headers with proper `#pragma once`, includes, and member declarations
- Modernizing template class instantiations to generic templates

## Instructions

1. Read the class definition from the database or provided context
2. Identify all referenced types (base classes, member types, parameter types)
3. Look up processed headers for referenced types to use proper include paths
4. Generate a modern C++ header following the strict rules below

## Strict Rules

- Output ONLY code, no explanations
- Use `#pragma once` instead of include guards
- Do NOT rename classes or method names
- For referenced types, use the path from "// Defined in:" comments for includes
- Remove ALL forward declarations - use includes instead
- Do NOT change existing types unless void/undefined
- Keep ALL existing methods in place, do not invent new methods
- NEVER define base or referenced types in this header
- Do NOT inline function definitions (except for templates)
- Clean up destructor method signatures to valid C++
- Convert `struct __cppobj` to `class` with proper access specifiers

## Example

### Input
```cpp
struct __cppobj Player : GameObject
{
    int _health;
    void Update(float dt);
};
```

### Output
```cpp
#pragma once
#include "GameObject.h"

/*
  Represents a player entity inheriting from GameObject,
  handling health management and status flags.
*/
class Player : public GameObject {
public:
    // Health of the player
    int _health;

    // Update the player
    void Update(float dt);
};
```

## Template Handling

For template instantiations (e.g., `List<int>`), generate a generic template:

### Input
```cpp
class List<int> {
public:
    int* _items;
    void Add(int item);
};

// Method Definition:
void List<int>::Add(int item) {
    _items[_count++] = item;
}
```

### Output
```cpp
#pragma once

template <typename T>
class List {
public:
    T* _items;

    void Add(T item) {
        _items[_count++] = item;
    }
};
```

## Output Path Convention

Headers are written to: `output/include/{Namespace}/{ClassName}.h`

For example:
- `Turbine::Player` -> `output/include/Turbine/Player.h`
- `Client::UI::Window` -> `output/include/Client/UI/Window.h`
