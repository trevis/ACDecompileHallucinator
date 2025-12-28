---
paths: "output/**/*.{h,cpp,hpp}"
---
# C++ Output Code Rules

## Generated Header Files

### Structure
```cpp
#pragma once

#include "RequiredType.h"  // Minimal includes only

namespace Turbine {

class ClassName : public BaseClass {
public:
    // Public methods (declarations only, no inline definitions)
    void MethodName(ParamType param);

private:
    // Private members with m_ prefix
    int m_memberVar{0};
};

} // namespace Turbine
```

### Rules
- Use `#pragma once` (never include guards)
- Do NOT inline function definitions in headers
- Do NOT rename classes or methods from decompiled code
- Preserve inheritance and member order exactly

## Generated Source Files

### Modernization Rules
- Remove decompiler artifacts: `__thiscall`, `__cdecl`, explicit `this` pointer
- Use modern types: `uint32_t`, `bool`, `size_t`
- Rename local variables for readability (NOT parameters)
- Add comments explaining complex logic
- Use enum names instead of magic numbers where known

### Preservation Requirements
- Preserve ALL original logic - no additions or removals
- Same control flow structure
- Same arithmetic operations
- Same side effects

### Verification Criteria
- Allow: variable renaming, type modernization, style changes
- Reject: logic changes, missing operations, added functionality
