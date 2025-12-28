---
name: modernize-method
description: Modernize decompiled C++ functions to clean, idiomatic C++17+. Use when converting raw decompiled methods, removing decompiler artifacts like __thiscall, or improving function readability while preserving exact logic.
allowed-tools: Read, Grep, Glob
---

# Modernize Method

Transform decompiled C++ functions into clean, modern C++ while preserving exact logic.

## When to Use

- Converting raw decompiled functions with artifacts like `__thiscall`, `__cdecl`
- Modernizing explicit `this` pointer usage to implicit member access
- Improving local variable names for readability
- Adding explanatory comments to complex logic

## Instructions

1. Read the original decompiled function
2. Identify the parent class and look up its processed header
3. Find all referenced types and get their definitions
4. **Check for enum types in parameters, return types, and member variables**
5. Generate modernized code following strict rules
6. Verify logic preservation (use /verify-logic if needed)

## Strict Rules

- Output ONLY the function code, no explanations or extra declarations
- Do NOT rename the function or parameters
- Renaming local variables for readability IS encouraged
- Remove decompiler artifacts:
  - `__thiscall`, `__cdecl`, `__stdcall`, `__fastcall`
  - Explicit `this` pointer (e.g., `this->_health` -> `_health`)
  - `__userpurge`, `__usercall`
- Use modern types:
  - `uint32_t` instead of `unsigned int`
  - `bool` instead of `int` for boolean values
  - `nullptr` instead of `NULL`
- Add comments to explain logic/purpose
- Do NOT add or remove logic
- Keep function signature compatible with class header
- Do NOT invent constants that don't exist

## Enum Value Replacement (CRITICAL)

**ALWAYS replace numeric literals with enum constants when the context indicates an enum type.**

### When to Replace

1. **Parameter type context**: If a parameter is typed as an enum (e.g., `DamageType type`),
   replace numeric literals: `if (type == 5)` -> `if (type == DamageType::Fire)`

2. **Switch statements**: When switching on an enum-typed variable, use enum constants:
   ```cpp
   // BAD: switch(mode) { case 1: ... case 2: ... }
   // GOOD: switch(mode) { case CombatMode::Melee: ... case CombatMode::Missile: ... }
   ```

3. **Comparisons**: Replace numeric comparisons with enum values:
   ```cpp
   // BAD: if (damage_type == 5)
   // GOOD: if (damage_type == DamageType::Fire)
   ```

4. **Assignments**: Replace numeric assignments to enum-typed members:
   ```cpp
   // BAD: this->_mode = 3;
   // GOOD: this->_mode = CombatMode::Magic;
   ```

5. **Function arguments**: When calling functions that take enum parameters

### How to Identify Enum Context

- Check the **"Referenced Types"** section for enum definitions with values
- Look at **parameter types** in the function signature
- Check **member variable types** in the parent class header
- If an **"Enum Value Reference"** section is provided, use those EXACT mappings

### Critical Rule

**If a numeric value matches a known enum constant from the provided context, YOU MUST USE THE ENUM CONSTANT instead of the magic number.**

## Example

### Input
```cpp
void __thiscall Player::TakeDamage(Player *this, int amount, int damage_type)
{
    if (damage_type == 5) {
        amount = amount * 2;  // Fire weakness
    }
    this->_health = this->_health - amount;
}
```

### Context (enum provided)
```cpp
enum DamageType {
    Slash = 1,
    Pierce = 2,
    Bludgeon = 3,
    Cold = 4,
    Fire = 5,
    Acid = 6
};
```

### Output
```cpp
/*
    Applies damage to the player, with fire weakness modifier.
*/
void Player::TakeDamage(int amount, DamageType damage_type) {
    if (damage_type == DamageType::Fire) {
        amount *= 2;  // Fire weakness doubles damage
    }
    _health -= amount;
}
```

## Verification

After modernizing, the function should be verified to ensure logic equivalence:
- Control flow must be identical
- All arithmetic operations preserved
- All side effects maintained
- Only allowed changes: variable names, types, style, enum constant names

## Output Path Convention

Source files are written to: `output/src/{Namespace}/{ClassName}.cpp`

Each function includes an offset comment:
```cpp
// Offset: 0x00401234
void Player::TakeDamage(int amount, DamageType damage_type) {
    // ...
}
```
