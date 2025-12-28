---
name: verify-logic
description: Verify that modernized C++ code preserves the exact logic of the original decompiled function. Use when checking code equivalence, validating refactoring changes, or ensuring no logic was accidentally altered during modernization.
allowed-tools: Read
---

# Verify Logic Equivalence

Compare original decompiled code with modernized version to ensure logic preservation.

## When to Use

- After modernizing a function to verify no logic was changed
- When reviewing refactored code for semantic equivalence
- To validate that only allowed transformations were applied

## Instructions

1. Receive both ORIGINAL and MODERNIZED code
2. Analyze control flow in both versions
3. Compare arithmetic and logical operations
4. Check that all side effects are preserved
5. Return a structured verification result

## What IS Allowed

These transformations are acceptable and should NOT cause verification failure:

- **Variable renaming**: `v1` -> `health`, `a2` -> `deltaTime`
- **Type updates**: `int` -> `bool`, `unsigned int` -> `uint32_t`
- **Structure changes**: Adding early returns, simplifying nested ifs
- **Safety checks**: Adding null checks before dereferencing
- **Style improvements**: Consistent bracing, spacing, formatting
- **Decompiler artifact removal**: `__thiscall`, explicit `this->`
- **Comment additions**: Explaining logic purpose

## What is NOT Allowed

These changes indicate logic modification and should FAIL verification:

- **Missing operations**: Any arithmetic/logic operation removed
- **Added operations**: New functionality not in original
- **Changed conditions**: Different comparison operators or values
- **Missing side effects**: Writes to member variables, calls removed
- **Different return values**: Changed return expressions
- **Reordered operations**: If order matters for side effects

## Response Format

Return ONLY a JSON object:

```json
{
  "equivalent": true,
  "confidence": "high",
  "reason": ""
}
```

Or if not equivalent:

```json
{
  "equivalent": false,
  "confidence": "high",
  "reason": "Missing assignment to _flags in else branch"
}
```

### Confidence Levels

- **high**: Clear determination, no ambiguity
- **medium**: Minor uncertainties but likely correct
- **low**: Significant ambiguity, manual review recommended

## Example Verification

### Original
```cpp
void __thiscall Player::TakeDamage(Player *this, int amount)
{
    this->_health = this->_health - amount;
    if (this->_health <= 0) {
        this->_flags = this->_flags | 0x1;
        (**(void (__thiscall **)(Player *))this->vtable)(this);
    }
}
```

### Modernized
```cpp
void Player::TakeDamage(int amount) {
    _health -= amount;
    if (_health <= 0) {
        // Set dead flag
        _flags |= 0x1;
        // Call virtual destructor/death handler
        OnDeath();
    }
}
```

### Result
```json
{
  "equivalent": true,
  "confidence": "high",
  "reason": ""
}
```

Note: The virtual call `(**(void...))` to `OnDeath()` is acceptable because it represents the same vtable call, just with a readable name.
