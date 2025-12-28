"""
Function Processor
==================
Processes individual functions/methods through the LLM pipeline,
resolving type references and storing processed output.
"""
import logging
import re
from typing import List, Dict, Optional, Set, Tuple
from pathlib import Path
from dataclasses import dataclass

from .skill_loader import SkillLoader, get_skill_loader
from .error_memory import ErrorMemory, get_error_memory

logger = logging.getLogger(__name__)


@dataclass
class ProcessedFunction:
    """Represents a processed function result"""
    name: str
    full_name: str
    parent_class: Optional[str]
    original_code: str
    processed_code: str
    dependencies: List[str]
    offset: str


class FunctionProcessor:
    """
    Processes individual functions with type context.
    
    Workflow:
    1. Extract type references from function code
    2. Look up type definitions (processed first, then raw)
    3. Send to LLM with type context
    4. Store processed result
    """
    
    # Few-shot example for function modernization
    FEW_SHOT_FUNCTION = """
Task: Modernize decompiled C++ functions to clean, idiomatic C++17+ code.
Requirements:

Preserve ALL original logic and structure exactly
Keep function names and parameters unchanged
Rename local variables for clarity
Remove decompiler artifacts (__thiscall, explicit this, etc.)
Replace numeric literals with enum values where definitions exist
Ensure code compiles and is valid C++

Output Format:

Function comment describing purpose
Function body only
NO includes, forward declarations, class definitions, or explanatory text

Critical Constraint:
Output ONLY the function comment and code body—nothing else.

Example Input 1:
int __cdecl LandDefs::get_dir(int ix, int iy)
{
  int result; // eax

  if ( ix >= 0 )
  {
    if ( ix <= 0 )
    {
      if ( iy >= 0 )
        result = iy > 0;
      else
        result = 2;
    }
    else if ( iy >= 0 )
    {
      result = 4 * (iy > 0) + 3;
    }
    else
    {
      result = 8;
    }
  }
  else if ( iy >= 0 )
  {
    result = (iy > 0) + 4;
  }
  else
  {
    result = 6;
  }
  return result;
}

Example Output 1:
/*
    Determines a direction index (0‑8) based on relative offsets ix and iy.
    The mapping corresponds to the LandDefs::Direction enumeration
*/
int LandDefs::get_dir(int ix, int iy)
{
    if (ix >= 0) {
        if (ix == 0) {                     // vertical axis
            if (iy >= 0) {
                return iy > 0 ? Direction::NORTH_OF_VIEWER : Direction::IN_VIEWER_BLOCK;
            } else {
                return Direction::SOUTH_OF_VIEWER;
            }
        } else {                            // ix > 0, east side
            if (iy >= 0) {
                return iy > 0 ? Direction::NORTHEAST_OF_VIEWER : Direction::EAST_OF_VIEWER;
            } else {
                return Direction::SOUTHEAST_OF_VIEWER;
            }
        }
    } else {                                // ix < 0, west side
        if (iy >= 0) {
            return iy > 0 ? Direction::NORTHWEST_OF_VIEWER : Direction::WEST_OF_VIEWER;
        } else {
            return Direction::SOUTHWEST_OF_VIEWER;
        }
    }
}

Example Input 2:
void __thiscall CELLARRAY::add_cell(CELLARRAY *this, const unsigned int cell_id, const CObjCell *cell)
{
  CELLARRAY *v3; // esi
  unsigned int v4; // edx
  unsigned int v5; // eax
  CELLINFO *v6; // ecx

  v3 = this;
  v4 = this->num_cells;
  v5 = 0;
  if ( v4 )
  {
    v6 = this->cells.data;
    while ( cell_id != v6->cell_id )
    {
      ++v5;
      ++v6;
      if ( v5 >= v3->num_cells )
        goto LABEL_5;
    }
  }
  else
  {
LABEL_5:
    if ( v4 >= v3->cells.sizeOf )
      DArray<CELLINFO>::grow(&v3->cells, v4 + 8);
    v3->cells.data[v3->num_cells].cell_id = cell_id;
    v3->cells.data[v3->num_cells++].cell = cell;
  }
}

Example Output 2:
/*
 * Adds a new cell to the array if it does not already exist.
 *
 * The function first scans the current collection for an entry with the same
 * `cell_id`.  If such an entry is found, the call is ignored.  Otherwise,
 * it ensures that there is enough capacity in the underlying dynamic array
 * and appends a new CELLINFO structure containing the supplied id and
 * pointer.
 */
void CELLARRAY::add_cell(uint32_t cell_id, const CObjCell* cell)
{
    // Search for an existing entry with the same ID.
    for (uint32_t i = 0; i < num_cells; ++i) {
        if (cells.data[i].cell_id == cell_id) {
            // Duplicate found – nothing to do.
            return;
        }
    }

    // Ensure there is space for a new element.
    if (num_cells >= cells.sizeOf) {
        DArray<CELLINFO>::grow(&cells, num_cells + 8);
    }

    // Append the new cell information.
    cells.data[num_cells].cell_id = cell_id;
    cells.data[num_cells++].cell   = cell;
}
"""

    # Verification prompt
    VERIFICATION_PROMPT = """
You are a senior code reviewer. Compare the ORIGINAL decompiled function with the MODERNIZED version.

ORIGINAL:
```cpp
{original}
```

MODERNIZED:
```cpp
{processed}
```Task: Verify that the MODERNIZED version preserves the core logic and semantics of the ORIGINAL.
Allowed Changes:
- Local variable renaming for readability
- Replacing inlined values with enums
- Local type updates (e.g., int → bool)
- Structure changes (e.g., early returns, loop refactoring)
- Safety improvements (e.g., null checks, bounds checks)

Validation Criteria:
- Core logic must remain identical

Output must contain ONLY:
- Function comment
- Function declaration/body


Output must NOT contain:
- Include statements
- Class definitions
- Forward declarations
- Any other code or explanatory text. comments in the code are fine.

Response Format:
Return ONLY this JSON object:
{{
  "equivalent": true/false,
  "reason": "Brief explanation if false, otherwise empty string"
}}
"""
    
    def __init__(self, db_handler, llm_client=None, debug_dir: Optional[Path] = None,
                 project_root: Optional[Path] = None, use_skills: bool = True,
                 context_builder=None, use_error_memory: bool = True):
        """
        Initialize the function processor.

        Args:
            db_handler: DatabaseHandler instance for type lookups
            llm_client: Optional LLM client for processing
            debug_dir: Optional directory for debug output
            project_root: Optional project root for skill loading
            use_skills: Whether to load and use skill instructions (default True)
            context_builder: Optional ContextBuilder instance for unified context
                gathering. If provided, can be used instead of internal methods.
            use_error_memory: Whether to use error memory for learning from
                failures (default True).
        """
        self.db = db_handler
        self.llm = llm_client
        self.debug_dir = Path(debug_dir) if debug_dir else None
        self.dependency_analyzer = None  # Set externally if needed
        self.use_skills = use_skills
        self.context_builder = context_builder

        # Load skill instructions for prompt enhancement (if enabled)
        if use_skills:
            self.skill_loader = get_skill_loader(project_root)
            self._skill_instructions = self._load_skill_instructions()
        else:
            self.skill_loader = None
            self._skill_instructions = ""

        # Initialize error memory for learning from failures
        if use_error_memory:
            self.error_memory = get_error_memory(db_handler)
        else:
            self.error_memory = None
        
        # Patterns for extracting types from function code
        self.type_patterns = [
            # Pointer/reference: SomeType* or SomeType&
            re.compile(r'\b([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s*[*&]'),
            # Parameter types: (SomeType param) - also include lowercase enum names like eCombatMode
            re.compile(r'\(\s*(?:[^()]*,\s*)?(const\s+)?([A-Za-z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s+\w+'),
            # Variable declarations: SomeType varname;
            re.compile(r'^\s*([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s+\w+\s*[;=]', re.MULTILINE),
            # Return type: SomeType ClassName::Method
            re.compile(r'^([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s*[*&]?\s+\w+::'),
            # Cast: (SomeType*)
            re.compile(r'\(\s*([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s*\*?\s*\)'),
            # Member access: ->member or .member with type context
            re.compile(r'([A-Z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)::(?:\w+)'),
            # Additional pattern for parameter types with lowercase enum names (like eCombatMode)
            re.compile(r'\b(const\s+)?([a-z][A-Za-z0-9_]*(?:::[A-Z][A-Za-z0-9_]*)*)\s+\w+(?=\s*[),])'),
        ]
        
        # Types to ignore
        self.ignore_types = {
            'int', 'unsigned', 'char', 'short', 'long', 'float', 'double',
            'void', 'bool', 'size_t', 'uint8_t', 'uint16_t', 'uint32_t',
            'uint64_t', 'int8_t', 'int16_t', 'int32_t', 'int64_t',
            'BYTE', 'WORD', 'DWORD', 'QWORD', 'BOOL', 'HANDLE', 'HRESULT',
            'TRUE', 'FALSE', 'NULL', 'nullptr', 'String', 'Vector',
        }

    def _load_skill_instructions(self) -> str:
        """Load skill instructions for prompt enhancement.

        Extracts key instructions from skill files that apply to function
        modernization. This allows both Claude Code and LM Studio engines
        to benefit from the same guidance.

        Returns:
            Formatted skill instructions string for prompt injection.
        """
        instructions = []

        # Load enum replacement instructions
        enum_instructions = self.skill_loader.get_enum_instructions()
        if enum_instructions:
            instructions.append(enum_instructions)

        # Load transformation rules
        rules = self.skill_loader.get_transformation_rules()
        if rules:
            instructions.append(rules)

        if instructions:
            return '\n\n'.join(instructions)
        return ""

    def _write_debug(self, method_name: str, parent_class: str, prompt: str, response: str):
        """Write debug files for function"""
        if not self.debug_dir:
            return
            
        # Build path: debug/namespace/class/method/
        # Handle namespaced class names
        parts = parent_class.replace('::', '/').split('/') if parent_class else ['Global']
        
        method_safe = re.sub(r'[^a-zA-Z0-9_]', '_', method_name)
        
        debug_dir = self.debug_dir / '/'.join(parts) / method_safe
        debug_dir.mkdir(parents=True, exist_ok=True)
        
        (debug_dir / "prompt.txt").write_text(prompt, encoding='utf-8')
        (debug_dir / "response.txt").write_text(response, encoding='utf-8')

    def find_type_references(self, code: str) -> Set[str]:
        """Find all type references in function code.

        Scans the provided C++ code for type names using regex patterns.
        Filters out primitive types and common type aliases.

        Args:
            code: The C++ function code to analyze for type references.

        Returns:
            A set of type names found in the code, excluding primitive types
            and common aliases like int, void, DWORD, etc.
        """
        references = set()
        
        for pattern in self.type_patterns:
            matches = pattern.findall(code)
            for match in matches:
                # Handle both single matches and tuple matches from regex groups
                if isinstance(match, tuple):
                    # For patterns with multiple groups, the type name is in the last non-empty group
                    type_name = None
                    for group in reversed(match):
                        if group:  # Find the last non-empty group which should be the type name
                            type_name = group
                            break
                    if type_name and type_name not in self.ignore_types:
                        if type_name[0].isupper() or type_name.startswith('e') or '::' in type_name:
                            references.add(type_name)
                else:
                    # Handle single string matches
                    if match and match not in self.ignore_types:
                        if match[0].isupper() or match.startswith('e') or '::' in match:
                            references.add(match)
        
        return references
    
    def get_reference_context(self, type_names: Set[str], max_types: int = 10) -> str:
        """Get type definitions for referenced types to provide as LLM context.

        Looks up each referenced type in the database, preferring processed
        (modernized) headers over raw decompiled code. Returns formatted
        context strings suitable for inclusion in LLM prompts.

        Args:
            type_names: Set of type names to look up definitions for.
            max_types: Maximum number of type definitions to include in the
                context. Defaults to 10 to avoid exceeding token limits.

        Returns:
            A formatted string containing type definitions, with comments
            indicating whether each type is modernized or raw decompiled code.
            Returns empty string if no types are found.
        """
        context_parts = []
        included = 0
        
        for name in sorted(type_names):
            if included >= max_types:
                break
            
            type_def, is_processed = self.db.get_type_with_fallback(name)
            if type_def:
                # Helper: Get the expected header file path for a type
                # (Duplicate of ClassHeaderGenerator logic for now, could be unified)
                if '::' in name:
                    parts = name.split('::')
                    file_path = f"{parts[0]}/{parts[-1]}.h"
                else:
                    file_path = f"{name}.h"
                
                path_info = f"// Defined in: \"{file_path}\"\n"

                if is_processed and type_def.get('processed_header'):
                    # Use just the class declaration from header
                    header = type_def['processed_header']
                    context_parts.append(f"// Reference: {name} (modernized)\n{path_info}{header}")
                    included += 1
                elif type_def.get('code'):
                    context_parts.append(f"// Reference: {name} (raw decompiled)\n{path_info}{type_def['code']}")
                    included += 1
        
        return "\n\n".join(context_parts)
    
    def get_parent_header_context(self, parent_class: Optional[str]) -> str:
        """Get the processed header of the parent class for LLM context.

        Retrieves the class definition for the parent class, preferring
        the modernized processed header if available, falling back to
        raw decompiled code.

        Args:
            parent_class: Name of the parent class to look up. Can be None
                for global functions.

        Returns:
            A formatted string containing the parent class definition with
            a comment header, or empty string if parent_class is None or
            not found in the database.
        """
        if not parent_class:
            return ""
        
        # Get the processed header for the parent class
        parent_type_def, is_processed = self.db.get_type_with_fallback(parent_class)
        
        if parent_type_def and is_processed and parent_type_def.get('processed_header'):
            # Extract just the class declaration part from the header for function context
            header = parent_type_def['processed_header']
            return f"// Parent class definition:\n{header}"
        elif parent_type_def and parent_type_def.get('code'):
            # Fallback to raw code if no processed header exists
            return f"// Parent class definition (raw):\n{parent_type_def['code']}"
        
        return ""


    def get_enum_value_context(self, method_code: str) -> str:
        """Extract numeric literals and match to known enum values.

        Scans the method code for numeric literals (decimal and hex) and
        looks up matching enum values in the database. Returns a formatted
        reference section to help the LLM replace magic numbers with enum constants.

        Args:
            method_code: The raw decompiled method code to analyze.

        Returns:
            A formatted string listing known enum values that match numeric
            literals found in the code, or empty string if none found.
        """
        # Find all numeric literals in the code (decimal and hex)
        hex_pattern = r'0[xX][0-9a-fA-F]+'
        decimal_pattern = r'(?<![0-9a-fA-Zx])\d+(?![0-9a-fA-Zx])'

        hex_matches = re.findall(hex_pattern, method_code)
        decimal_matches = re.findall(decimal_pattern, method_code)

        # Convert to integer values
        values_to_check = set()
        for hex_val in hex_matches:
            try:
                values_to_check.add(int(hex_val, 16))
            except ValueError:
                pass
        for dec_val in decimal_matches:
            try:
                val = int(dec_val)
                # Skip very common values that are likely not enums
                if val not in (0, 1, 2, -1):
                    values_to_check.add(val)
            except ValueError:
                pass

        if not values_to_check:
            return ''

        # Look up enum values from the database
        # Types table columns: id(0), type(1), name(2), namespace(3), parent(4), code(5), ...
        enum_mappings = []

        for enum_row in self.db.get_enums():
            enum_name = enum_row[2]  # name column
            enum_code = enum_row[5] if len(enum_row) > 5 else ''

            if not enum_code:
                continue

            # Parse enum values from the definition
            value_pattern = r'(\w+)\s*=\s*(0[xX][0-9a-fA-F]+|\d+)'
            for match in re.finditer(value_pattern, enum_code):
                const_name = match.group(1)
                const_val_str = match.group(2)
                try:
                    if const_val_str.lower().startswith('0x'):
                        const_val = int(const_val_str, 16)
                    else:
                        const_val = int(const_val_str)

                    if const_val in values_to_check:
                        enum_mappings.append({
                            'enum': enum_name,
                            'const': const_name,
                            'value': const_val,
                            'hex': hex(const_val) if const_val >= 10 else str(const_val)
                        })
                except ValueError:
                    pass

        if not enum_mappings:
            return ''

        # Format as reference section
        lines = ['Enum Value Reference (use these instead of magic numbers):']

        # Group by enum name
        by_enum = {}
        for mapping in enum_mappings:
            enum = mapping['enum']
            if enum not in by_enum:
                by_enum[enum] = []
            by_enum[enum].append(mapping)

        for enum_name, mappings in sorted(by_enum.items()):
            for m in sorted(mappings, key=lambda x: x['value']):
                lines.append(f"  {m['value']} ({m['hex']}) -> {enum_name}::{m['const']}")

        return '\n'.join(lines)

    def build_prompt(self, method_definition: str, parent_class: Optional[str],
                     reference_context: str = "", analysis: str = "", 
                     enum_context: str = "", error_warnings: str = "") -> str:
        """Build the LLM prompt for function modernization.

        Constructs a complete prompt including the method to modernize,
        parent class context, referenced type definitions, and few-shot
        examples for guiding the LLM.

        Args:
            method_definition: The raw decompiled C++ method code to modernize.
            parent_class: Name of the class this method belongs to, or None
                for global functions.
            reference_context: Pre-formatted string of referenced type
                definitions to include. Defaults to empty string.
            analysis: Optional analysis string from prior class analysis.
                Currently unused but reserved for future enhancements.
            enum_context: Enum value mappings for magic number replacement.
            error_warnings: Warnings from error memory about similar code
                patterns that failed previously.

        Returns:
            A complete prompt string ready to send to the LLM, including
            the method code, context, and few-shot examples.
        """
        prompt = f"""Modernize this decompiled C++ function:

```cpp
{method_definition}
```
"""
        
        if parent_class:
            prompt += f"\nThis function belongs to class: {parent_class}\n"

        # Add error warnings early in prompt for emphasis
        if error_warnings:
            prompt += f"\n{error_warnings}\n"
        
        # Add parent class header context
        parent_header_context = self.get_parent_header_context(parent_class)
        if parent_header_context:
            prompt += f"""
Parent Class Definition:
{parent_header_context}
"""
        
        if reference_context:
            prompt += f"""
Referenced Types (for context):
{reference_context}
"""

        if enum_context:
            prompt += f"""
{enum_context}

IMPORTANT: Replace ALL numeric literals that appear in the enum reference above with their corresponding enum constants. Use the fully qualified enum name (e.g., EnumName::CONSTANT).
"""

        # Add skill-based instructions (loaded from .claude/skills/)
        if self._skill_instructions:
            prompt += f"""
{self._skill_instructions}
"""

        prompt += f"\n{self.FEW_SHOT_FUNCTION}"

        return prompt

    def build_prompt_with_context(self, method_definition: str, parent_class: Optional[str],
                                   namespace: Optional[str] = None) -> str:
        """Build prompt using the unified ContextBuilder for preprocessing.

        This method uses the ContextBuilder to:
        - Preprocess code (remove calling conventions, replace decompiler types)
        - Gather type references and parent class context
        - Map enum values for magic number replacement

        Args:
            method_definition: The raw decompiled C++ method code.
            parent_class: Name of the class this method belongs to.
            namespace: Optional namespace for the parent class.

        Returns:
            A complete prompt string with preprocessed code and context.
        """
        if self.context_builder is None:
            # Fall back to non-preprocessed prompt
            references = self.find_type_references(method_definition)
            if parent_class:
                references.discard(parent_class)
            context = self.get_reference_context(references)
            enum_context = self.get_enum_value_context(method_definition)
            return self.build_prompt(method_definition, parent_class, context,
                                     enum_context=enum_context)

        # Use ContextBuilder for comprehensive context gathering and preprocessing
        ctx = self.context_builder.gather_method_context(
            code=method_definition,
            parent_class=parent_class,
            namespace=namespace,
            include_enums=True,
            include_constants=True,
            preprocess_code=True,
        )

        # Use preprocessed code or fall back to original
        code_to_use = ctx.preprocessed_code if ctx.preprocessed_code else method_definition

        # Build preprocessing info string
        preprocessing_info = ""
        if ctx.preprocessing_summary:
            preprocessing_info = f"\nPreprocessing Applied: {ctx.preprocessing_summary}\n"

        # Build the prompt using the preprocessed code
        prompt = f"""Modernize this decompiled C++ function:

```cpp
{code_to_use}
```
"""
        if parent_class:
            prompt += f"\nThis function belongs to class: {parent_class}\n"

        if preprocessing_info:
            prompt += preprocessing_info

        # Add parent class header context from ContextBuilder
        if ctx.parent_header:
            status = "modernized" if ctx.parent_is_processed else "raw"
            prompt += f"""
Parent Class Definition ({status}):
{ctx.parent_header}
"""

        # Add type context
        if ctx.type_context_str:
            prompt += f"""
Referenced Types (for context):
{ctx.type_context_str}
"""

        # Add enum context
        if ctx.enum_context_str:
            prompt += f"""
{ctx.enum_context_str}

IMPORTANT: Replace ALL numeric literals that appear in the enum reference above with their corresponding enum constants. Use the fully qualified enum name (e.g., EnumName::CONSTANT).
"""

        # Add skill-based instructions
        if self._skill_instructions:
            prompt += f"""
{self._skill_instructions}
"""

        prompt += f"\n{self.FEW_SHOT_FUNCTION}"

        return prompt

    def verify_logic(self, original: str, processed: str) -> Tuple[bool, str]:
        """Verify that modernized code preserves the original logic.

        Sends both the original and processed code to the LLM for semantic
        comparison. The LLM evaluates whether the core logic is preserved
        while allowing acceptable changes like variable renaming, enum
        substitution, and structural improvements.

        Args:
            original: The original decompiled C++ function code.
            processed: The modernized C++ function code to verify.

        Returns:
            A tuple of (equivalent, reason) where:
            - equivalent: True if the logic is preserved, False otherwise.
            - reason: Empty string if equivalent, otherwise an explanation
              of the logic differences detected.

        Raises:
            ValueError: If no LLM client is configured.
        """
        import json
        
        prompt = self.VERIFICATION_PROMPT.format(
            original=original, 
            processed=processed
        )
        
        response = self._call_llm(prompt)
        response = self._clean_llm_output(response)
        
        # Parse JSON response
        try:
            # Try to find JSON block if wrapped
            json_match = re.search(r'\{.*\}', response, re.DOTALL)
            if json_match:
                data = json.loads(json_match.group(0))
                return data.get("equivalent", False), data.get("reason", "")
        except json.JSONDecodeError:
            pass
            
        return False, f"Failed to parse verification response: {response[:50]}..."

    def process_function(self, method_row: Tuple, save_to_db: bool = True, analysis: str = None) -> Optional[ProcessedFunction]:
        """Process a single function through the LLM modernization pipeline.

        Takes a method from the database, gathers type context, sends it
        through the LLM for modernization, verifies the output preserves
        logic, and optionally stores the result.

        The processing pipeline:
        1. Check error memory for relevant warnings
        2. Extract type references from the function code
        3. Gather context from referenced types and parent class
        4. Build and send prompt to LLM (with error warnings if available)
        5. Verify the modernized code preserves logic (with retries)
        6. Record failures/successes to error memory
        7. Store result in database if requested

        Args:
            method_row: Database row tuple containing method information in
                format (id, name, full_name, definition, namespace, parent,
                is_generic, is_ignored, offset, return_type, is_global).
            save_to_db: Whether to persist the processed result to the
                database. Defaults to True.
            analysis: Optional JSON string from prior class analysis
                containing referenced_types list for additional context.

        Returns:
            ProcessedFunction object containing the modernized code and
            metadata, or None if LLM processing failed.

        Raises:
            ValueError: If no LLM client is configured.
        """
        if not self.llm:
            raise ValueError("LLM client not set")
        
        # Extract method info from row
        # Format: (id, name, full_name, definition, namespace, parent, is_generic, is_ignored, offset, return_type, is_global)
        name = method_row[1]
        full_name = method_row[2]
        definition = method_row[3]
        namespace = method_row[4] if len(method_row) > 4 else None
        parent = method_row[5] if len(method_row) > 5 else None
        offset = method_row[8] if len(method_row) > 8 else "0"

        # Check error memory for relevant warnings (if enabled)
        error_warnings = ""
        if self.error_memory:
            error_warnings = self.error_memory.get_warnings_for_code(definition)
            if error_warnings:
                logger.info(
                    f"Found relevant error warnings for {full_name}"
                )
        
        # Find type references from the function definition
        references = self.find_type_references(definition)
        
        # If analysis is provided, extract types from it
        if analysis:
            import json
            try:
                analysis_data = json.loads(analysis)
                # Get referenced types from the analysis result (which is a dict)
                analysis_references = set(analysis_data.get("referenced_types", []))
                # Combine with regex-based references
                references = references | analysis_references
            except (json.JSONDecodeError, AttributeError):
                # If JSON parsing fails or if analysis_data doesn't have get method (is not dict),
                # just use the regex-based references
                pass
        
        # Remove the parent class from references (we're defining it)
        if parent:
            references.discard(parent)
        
        # Get all types from the database and add them as potential references
        all_db_types = set()
        for struct_row in self.db.get_structs():
            all_db_types.add(struct_row[2])  # name column
        for enum_row in self.db.get_enums():
            all_db_types.add(enum_row[2])  # name column
        
        # Combine all references - deduplicate by using set operations
        all_references = references | all_db_types
        
        # Get context for referenced types
        context = self.get_reference_context(all_references)

        # Get enum value context for magic number replacement
        enum_context = self.get_enum_value_context(definition)

        # Build prompt and call LLM - include enum context and error warnings
        prompt = self.build_prompt(
            definition, parent, context, 
            analysis=None, 
            enum_context=enum_context,
            error_warnings=error_warnings
        )
        processed_code = self._call_llm(prompt)
        
        if not processed_code:
            return None
        
        # Clean LLM output
        processed_code = self._clean_llm_output(processed_code)

        # ────────────────────────────────────────────────────────────────────────
        # Verification Step with retry logic
        # ────────────────────────────────────────────────────────────────────────
        
        is_valid = False
        reason = ""
        retry_count = 0
        max_retries = 5
        last_failed_output = None
        
        while not is_valid and retry_count < max_retries:
            is_valid, reason = self.verify_logic(definition, processed_code)
            
            if not is_valid and retry_count < max_retries:
                # Log the validation failure with retry count
                logger.warning(f"Function {full_name} validation failed on attempt {retry_count + 1}/{max_retries}: {reason}")
                
                # Record failure to error memory (if enabled)
                last_failed_output = processed_code
                if self.error_memory:
                    category = self.error_memory.categorize_error(reason)
                    self.error_memory.record_failure(
                        category=category,
                        original_code=definition,
                        failed_output=processed_code,
                        error_description=reason,
                        method_name=name,
                        class_name=parent,
                    )
                
                # Build a feedback prompt to improve the function based on the verification failure
                feedback_prompt = f"""Original function:
```cpp
{definition}
```

Attempted modernization:
```cpp
{processed_code}
```

Verification feedback: {reason}

Please regenerate the function addressing the issues mentioned in the verification feedback. Ensure that the logic remains identical while improving the code style and structure where possible.
"""
                
                # Call the LLM again with the feedback
                processed_code = self._call_llm(feedback_prompt)
                if processed_code:
                    processed_code = self._clean_llm_output(processed_code)
                
                retry_count += 1
            else:
                # Log the successful validation
                if is_valid:
                    if retry_count == 0:
                        logger.info(f"Function {full_name} validation successful on first attempt")
                    else:
                        logger.info(f"Function {full_name} validation successful after {retry_count} retries")
                        # Record successful retry to error memory
                        if self.error_memory and last_failed_output:
                            self.error_memory.record_success_after_retry(
                                definition, processed_code
                            )

        # Debug output
        if self.debug_dir and parent:
             # Use full parent name for debug path if possible
             parent_full = f"{namespace}::{parent}" if namespace else parent
             self._write_debug(name, parent_full, prompt, processed_code)
             # Also write verification debug
             method_safe = re.sub(r'[^a-zA-Z0-9_]', '_', name)
             parts = parent_full.replace('::', '/').split('/')
             debug_dir = self.debug_dir / '/'.join(parts) / method_safe
             
             (debug_dir / "verification.txt").write_text(
                 f"Equivalent: {is_valid}\nReason: {reason}\nRetries: {retry_count}",
                 encoding='utf-8'
             )
        
        if not is_valid:
            processed_code = f"// VERIFICATION FAILED after {max_retries} attempts: {reason}\n{processed_code}"

        result = ProcessedFunction(
            name=name,
            full_name=full_name,
            parent_class=parent,
            original_code=definition,
            processed_code=processed_code,
            dependencies=list(references),
            offset=offset
        )
        
        if save_to_db:
            # Create a method-like object for storing
            from .method import Method
            method = Method()
            method.name = name
            method.full_name = full_name
            method.parent = parent
            method.namespace = namespace
            method.definition = definition
            method.offset = offset
            
            engine_name = self.llm.name if self.llm and hasattr(self.llm, 'name') else "lm-studio"
            self.db.store_processed_method(method, processed_code, list(references), engine_used=engine_name)
        
        return result
    
    def process_class_methods(self, class_name: str,
                              save_to_db: bool = True) -> List[ProcessedFunction]:
        """Process all unprocessed methods for a given class.

        Retrieves all methods belonging to the specified class that have
        not yet been processed, and runs each through the LLM pipeline.
        Methods are processed sequentially.

        Args:
            class_name: Name of the class whose methods should be processed.
                Must match the parent field in the methods table.
            save_to_db: Whether to persist processed results to the database.
                Defaults to True.

        Returns:
            List of ProcessedFunction objects for successfully processed
            methods. Methods that fail processing are not included.
        """
        # Get unprocessed methods for this class
        methods = self.db.get_unprocessed_methods(parent_class=class_name)
        
        results = []
        for method in methods:
            result = self.process_function(method, save_to_db=save_to_db)
            if result:
                results.append(result)
        
        return results
    
    def _call_llm(self, prompt: str) -> str:
        """Call the LLM with the given prompt"""
        if hasattr(self.llm, 'generate'):
            return self.llm.generate(prompt)
        elif callable(self.llm):
            return self.llm(prompt)
        else:
            raise NotImplementedError("LLM client must have 'generate' method or be callable")
    
    def _clean_llm_output(self, text: str) -> str:
        """Remove common LLM output artifacts"""
        if not text:
            return ""
        
        # Remove markdown code blocks
        text = re.sub(r'^```(?:cpp|c\+\+|c|\w+)?\s*\n?', '', text, flags=re.M | re.I)
        text = re.sub(r'\n?```$', '', text, flags=re.M)
        
        # Remove markdown formatting
        text = re.sub(r'\*\*([^*]+)\*\*', r'\1', text)
        text = re.sub(r'__([^_]+)__', r'\1', text)
        
        return text.strip()
    
    def write_source_file(self, class_name: str, functions: List[ProcessedFunction],
                          output_dir: Path, namespace: str = None) -> Path:
        """Write processed functions to a C++ source file.

        Generates a .cpp file containing all the processed function
        implementations for a class. The file is organized with proper
        includes and offset comments for each function.

        The output path follows the pattern:
        - With namespace: output_dir/src/{namespace}/{class_name}.cpp
        - Without namespace: output_dir/src/{class_name}.cpp

        Args:
            class_name: Name of the class, used for the filename and
                include directive.
            functions: List of ProcessedFunction objects to write.
                Each function's processed_code and offset are included.
            output_dir: Base output directory. A 'src' subdirectory will
                be created if it doesn't exist.
            namespace: Optional namespace for organizing output into
                subdirectories. Namespace separators (::) are converted
                to directory separators.

        Returns:
            Path to the written .cpp file.
        """
        # Build output path
        if namespace:
            source_dir = output_dir / "src" / namespace.replace('::', '/')
        else:
            source_dir = output_dir / "src"
        
        source_dir.mkdir(parents=True, exist_ok=True)
        source_path = source_dir / f"{class_name}.cpp"
        
        # Build source content
        content = f'#include "{class_name}.h"\n\n'
        
        for func in functions:
            content += f"// Offset: 0x{func.offset}\n"
            content += func.processed_code
            content += "\n\n"
        
        source_path.write_text(content, encoding='utf-8')
        
        return source_path
