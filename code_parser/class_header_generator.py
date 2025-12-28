"""
Class Header Generator
======================
Generates modernized C++ header files for classes, combining struct definitions
with method signatures, using LLM processing.
"""
import json
import logging
import re
from typing import List, Dict, Optional, Set, Any
from pathlib import Path

logger = logging.getLogger(__name__)


class ClassHeaderGenerator:
    """
    Generates class header files by combining struct definitions with method signatures.
    
    Workflow:
    1. Gather struct definition and all methods for a class
    2. Find type references and get their definitions as context
    3. Send to LLM for modernization
    4. Save processed header to database and file
    """
    
    # Few-shot examples

    FEW_SHOT_CLASS = """Task: Generate a C++ header file for the given class.

Required Elements:
- #pragma once
- Minimal necessary #includes
- Class declaration with all members and methods
- Documentation comments

Output Requirements:
- Output ONLY the header file code with no explanations
- Preserve all original class names, method names, and type definitions exactly as provided
- Keep all existing methods; do not add or remove any
- Add descriptive code comments for clarity
- Produce valid C++ syntax

Include Directives:
- For referenced types with "// Defined in:" comments, use that path for the #include
- Remove all forward declarations

Type Handling:
- Never modify existing defined types
- Never define or forward-declare base classes or referenced types
- Assume base classes and member types are handled elsewhere

Method Handling:
- Never inline function definitions
- Clean up destructor signatures to valid C++ syntax
- Skip compiler-generated methods (e.g., vector/scalar deleting destructors)

Virtual Table Handling:
- When a struct has a separate vtbl struct, merge the virtual methods into the main class
- Convert vtbl function pointers to virtual method declarations
- Remove __thiscall calling convention and "this" parameter

Example:

Input:
struct __cppobj Archive {
    Archive_vtbl *__vftable /*VFT*/;
    unsigned int m_flags;
    TResult m_hrError;
    SmartBuffer m_buffer;
    unsigned int m_currOffset;
    HashTable<unsigned long, Interface *, 0> *m_pcUserDataHash;
    IArchiveVersionStack *m_pVersionStack;
};

struct /*VFT*/ Archive_vtbl {
    void (__thiscall *InitForPacking)(Archive *this, const ArchiveInitializer *, const SmartBuffer *);
    void (__thiscall *InitForUnpacking)(Archive *this, const ArchiveInitializer *, const SmartBuffer *);
    void (__thiscall *SetCheckpointing)(Archive *this, bool);
    void (__thiscall *InitVersionStack)(Archive *this);
    void (__thiscall *CreateVersionStack)(Archive *this);
};

Output:
#pragma once

#include "ArchiveInitializer.h"
#include "SmartBuffer.h"
#include "TResult.h"
#include "HashTable.h"
#include "Interface.h"
#include "IArchiveVersionStack.h"

/**
 * Archive class for serialization and deserialization operations.
 * Manages packing/unpacking of data with version control and checkpointing support.
 */
class Archive {
public:
    // Virtual methods
    virtual void InitForPacking(const ArchiveInitializer *initializer, const SmartBuffer *buffer);
    virtual void InitForUnpacking(const ArchiveInitializer *initializer, const SmartBuffer *buffer);
    virtual void SetCheckpointing(bool enabled);
    virtual void InitVersionStack();
    virtual void CreateVersionStack();

    // Member variables
    unsigned int m_flags;
    TResult m_hrError;
    SmartBuffer m_buffer;
    unsigned int m_currOffset;
    HashTable<unsigned long, Interface *, 0> *m_pcUserDataHash;
    IArchiveVersionStack *m_pVersionStack;
};
"""

    FEW_SHOT_TEMPLATE = """Task: Generate a C++ header file for the given template class.

Required Elements:
- #pragma once
- Minimal necessary #includes
- Template declaration with all members
- Full inline method definitions (required for templates)
- Documentation comments

Template Parameterization:
- Convert concrete template instantiations to generic templates
  - Example: List<int> → template <typename T> class List
- Use `template <typename T>` for single-type parameters
- Use `template <typename T, typename U, ...>` for multiple type parameters
- Use `template <typename T, size_t N>` for non-type parameters when appropriate
- Preserve any non-type template parameters from the original

Output Requirements:
- Output ONLY the header file code with no explanations
- Preserve all original method names and signatures
- Replace concrete types with template parameters where applicable
- Keep all existing methods; do not add or remove any
- Add descriptive code comments for clarity
- Produce valid C++ syntax
- Only output the template class. No forward declarations, just the class and methods.

Include Directives:
- For referenced types with "// Defined in:" comments, use that path for the #include
- Do not forward-declare types

Type Handling:
- Replace concrete template types (e.g., int in List<int>) with template parameters (T)
- Preserve non-template member types unless they're void/undefined
- Never define or forward-declare base classes or referenced types
- Assume external types are handled elsewhere

Method Handling:
- Provide full inline definitions for all template methods (inside class body or immediately after)
- Never leave template method declarations without definitions
- Convert struct to class for templates
- Clean up destructor signatures to valid C++ syntax
- Skip compiler-generated methods (e.g., vector/scalar deleting destructors)

Example:

Input:
class List<int> {
public:
    int* _items;
    int _count;
    int _capacity;
    void Add(int item);
    int Get(int index);
};

// Method definitions:
void List<int>::Add(int item) {
    if (_count >= _capacity) Resize();
    _items[_count++] = item;
}

int List<int>::Get(int index) {
    return _items[index];
}

Output:
#pragma once

/**
 * Generic list container for storing elements of type T.
 * Provides dynamic array functionality with automatic resizing.
 */
template <typename T>
class List {
public:
    // Pointer to the underlying array of items
    T* _items;
    
    // Current number of items in the list
    int _count;
    
    // Current allocated capacity
    int _capacity;
    
    /**
     * Adds an item to the list, resizing if necessary.
     */
    void Add(T item) {
        if (_count >= _capacity) Resize();
        _items[_count++] = item;
    }
    
    /**
     * Gets the item at the specified index.
     */
    T Get(int index) {
        return _items[index];
    }

private:
    void Resize();
};
"""
    
    def __init__(self, db_handler, llm_client=None, debug_dir: Optional[Path] = None):
        """Initialize the header generator with database and optional LLM client.

        Sets up the generator with required dependencies for type lookups and
        optional LLM processing. The LLM client can be set later if not provided
        at initialization.

        Args:
            db_handler: DatabaseHandler instance for type lookups and storage.
            llm_client: Optional LLM client for processing. Must have a 'generate'
                method or be callable. Can be set later via the `llm` attribute.
            debug_dir: Optional directory path for debug output files. When set,
                prompts and responses are saved for each processed class.

        Example:
            >>> db = DatabaseHandler('types.db')
            >>> generator = ClassHeaderGenerator(db)
            >>> generator.llm = LLMClient()  # Set LLM client later
        """
        self.db = db_handler
        self.llm = llm_client
        self.debug_dir = Path(debug_dir) if debug_dir else None
        self.dependency_analyzer = None  # Set externally if needed

        # Debug tracking
        self._last_analysis_prompt: Optional[str] = None
        self._last_analysis: Optional[str] = None
        self._last_prompt: Optional[str] = None
        self._last_response: Optional[str] = None
        self._last_types: Optional[Dict[str, Any]] = None
    
    def _write_debug(self, class_name: str):
        """Write debug files for the last generated header"""
        if not self.debug_dir:
            return
        
        # Build path: namespace/class/_header/
        # Handle namespaced class names like "Namespace::ClassName"
        parts = class_name.replace('::', '/').split('/')
        
        # Create directory: debug_dir/namespace/class/_header/
        header_dir = self.debug_dir / '/'.join(parts) / '_header'
        header_dir.mkdir(parents=True, exist_ok=True)

        # Write analysis prompt/response
        if self._last_analysis_prompt:
            (header_dir / "analysis_prompt.txt").write_text(self._last_analysis_prompt, encoding='utf-8')
        if self._last_analysis:
            (header_dir / "analysis.txt").write_text(self._last_analysis, encoding='utf-8')
        
        # Write prompt
        if self._last_prompt:
            prompt_path = header_dir / "prompt.txt"
            prompt_path.write_text(self._last_prompt, encoding='utf-8')
        
        # Write response
        if self._last_response:
            response_path = header_dir / "response.txt"
            response_path.write_text(self._last_response, encoding='utf-8')
        
        # Write types
        if self._last_types:
            types_path = header_dir / "types.json"
            types_path.write_text(json.dumps(self._last_types, indent=2), encoding='utf-8')
    
    def gather_class_info(self, class_name: str) -> Dict:
        """Collect struct definition and method information for a class.

        Queries the database for the struct definition, all associated methods,
        and any nested types belonging to the specified class. This information
        is used as input for LLM-based header generation.

        Args:
            class_name: The fully qualified name of the class to gather info for.
                Can include namespace (e.g., "Turbine::PlayerModule").

        Returns:
            A dictionary containing:
                - class_name (str): The input class name.
                - struct (tuple): The struct definition row from the database,
                    or None if not found.
                - methods (list): List of method rows belonging to this class.
                - vtable (str): VTable code if present, otherwise None.
                - nested (list): List of dicts for nested types, each containing
                    'name', 'struct', and 'methods' keys.

        Example:
            >>> info = generator.gather_class_info("PlayerModule")
            >>> info['class_name']
            'PlayerModule'
            >>> len(info['methods'])
            15
        """
        # Get struct definition
        struct_rows = self.db.get_type_by_name(class_name, 'struct')
        struct = struct_rows[0] if struct_rows else None
        
        # Get all methods for this class
        methods = self.db.get_methods_by_parent(class_name)
        
        # Get all nested types
        nested_types = self.db.get_nested_types(class_name)
        nested_info = []
        for nt in nested_types:
            nt_name = nt[2]
            nt_methods = self.db.get_methods_by_parent(nt_name)
            nested_info.append({
                'name': nt_name,
                'struct': nt,
                'methods': nt_methods
            })

        return {
            'class_name': class_name,
            'struct': struct,
            'methods': methods,
            'vtable': struct[10] if struct and len(struct) > 10 else None,  # vtable_code column
            'nested': nested_info
        }
    
    def is_template_instantiation(self, type_name: str) -> bool:
        """Check if a type name represents a template instantiation.

        Determines whether the given type name follows the pattern of a C++
        template instantiation (e.g., "List<int>", "Map<string, int>").

        Args:
            type_name: The type name to check for template instantiation pattern.

        Returns:
            True if the type name contains angle brackets indicating a template
            instantiation, False otherwise.

        Example:
            >>> generator.is_template_instantiation("List<int>")
            True
            >>> generator.is_template_instantiation("PlayerModule")
            False
        """
        return '<' in type_name and type_name.endswith('>')

    def get_template_base_name(self, type_name: str) -> str:
        """Extract the base template name from a template instantiation.

        Strips template parameters from a type name to get the underlying
        template class name. If the type is not a template instantiation,
        returns the original name unchanged.

        Args:
            type_name: The type name to extract the base name from.
                Can be a template instantiation like "List<int>" or a
                regular type name like "PlayerModule".

        Returns:
            The base template name without template parameters.
            For "List<int>", returns "List".
            For "Map<string, int>", returns "Map".
            For non-template types, returns the input unchanged.

        Example:
            >>> generator.get_template_base_name("List<int>")
            'List'
            >>> generator.get_template_base_name("PlayerModule")
            'PlayerModule'
        """
        if '<' in type_name:
            return type_name.split('<')[0].strip()
        return type_name

    def extract_method_signature(self, method_row) -> str:
        """Extract a clean method signature from a method definition row.

        Parses a method row from the database and extracts just the method
        signature, cleaning up decompiler-specific artifacts like calling
        conventions (__thiscall, __cdecl, etc.).

        Args:
            method_row: A tuple from the database containing method information.
                Expected format: (id, name, full_name, definition, namespace, parent, ...).
                The definition at index 3 contains the full method body.

        Returns:
            A cleaned method signature string ending with a semicolon.
            Decompiler calling conventions are removed, and only the first
            line (signature) is extracted from the definition.

        Example:
            >>> method = (1, 'Update', 'Player::Update', 'void __thiscall Update() { ... }', ...)
            >>> generator.extract_method_signature(method)
            'void Update();'
        """
        # method_row format: (id, name, full_name, definition, namespace, parent, ...)
        definition = method_row[3] if len(method_row) > 3 else ""

        # Extract just the first line (signature)
        lines = definition.split('\n')
        signature = lines[0] if lines else ""

        # Clean up decompiler artifacts
        for modifier in ['__cdecl', '__stdcall', '__thiscall', '__userpurge',
                        '__usercall', '__fastcall', '__noreturn']:
            signature = signature.replace(modifier, '')

        # Remove body brace if present
        signature = signature.split('{')[0].strip()
        if not signature.endswith(';'):
            signature += ';'

        return signature
    
    def find_type_references(self, class_info: Dict) -> Set[str]:
        """Find all type names referenced in the class definition and methods.

        Scans the struct definition and method signatures to identify all
        referenced types that may need to be included or forward-declared
        in the generated header.

        Uses the dependency analyzer if available for more accurate extraction,
        otherwise falls back to simple regex pattern matching for capitalized
        identifiers.

        Args:
            class_info: A dictionary containing class information as returned
                by gather_class_info(). Must contain 'class_name' and optionally
                'struct' and 'methods' keys.

        Returns:
            A set of type names referenced in the class code. The class's own
            name is excluded from the results. Types are extracted from:
            - Member variable declarations
            - Method parameter types
            - Method return types
            - Base class references

        Example:
            >>> info = generator.gather_class_info("PlayerModule")
            >>> refs = generator.find_type_references(info)
            >>> 'GameObject' in refs
            True
        """
        references = set()

        # Use dependency analyzer if available
        if self.dependency_analyzer:
            struct = class_info.get('struct')
            if struct:
                code = struct[5] if len(struct) > 5 else ""
                references.update(self.dependency_analyzer.extract_type_references(code))

            # Check method signatures too
            for method in class_info.get('methods', []):
                definition = method[3] if len(method) > 3 else ""
                refs = self.dependency_analyzer.extract_type_references(definition)
                references.update(refs)
        else:
            # Fallback: simple pattern matching
            struct = class_info.get('struct')
            if struct:
                code = struct[5] if len(struct) > 5 else ""
                # Look for capitalized words that might be types
                pattern = re.compile(r'\b([A-Z][A-Za-z0-9_]*)\b')
                matches = pattern.findall(code)
                references.update(matches)

        # Remove the class itself
        references.discard(class_info['class_name'])

        return references

    def _get_type_filepath(self, type_name: str) -> str:
        """Get the expected include path for a type.

        Converts a fully qualified type name to its expected header file path
        based on namespace conventions. Namespaced types use the first
        namespace component as a directory prefix.

        Args:
            type_name: The fully qualified type name, potentially including
                namespace separators (e.g., "Turbine::Debug::Assert").

        Returns:
            The expected relative header file path for the type.
            For "Turbine::Debug::Assert", returns "Turbine/Assert.h".
            For non-namespaced types like "PlayerModule", returns "PlayerModule.h".

        Example:
            >>> generator._get_type_filepath("Turbine::Debug::Assert")
            'Turbine/Assert.h'
            >>> generator._get_type_filepath("SmartBuffer")
            'SmartBuffer.h'
        """
        if '::' in type_name:
            # Turbine::Debug::Assert -> Turbine/Assert.h
            parts = type_name.split('::')
            return f"{parts[0]}/{parts[-1]}.h"
        return f"{type_name}.h"
    
    def get_reference_context(self, type_names: Set[str], max_types: int = 10) -> str:
        """Get type definitions for referenced types to provide as LLM context.

        Looks up each referenced type in the database and builds a context
        string containing their definitions. Prefers modernized (processed)
        definitions when available, falling back to raw decompiled code.

        Each type definition includes a comment indicating its expected
        include path, which the LLM can use to generate correct #include
        directives.

        Args:
            type_names: Set of type names to look up in the database.
            max_types: Maximum number of types to include in the context.
                Limits context size to avoid exceeding LLM token limits.
                Defaults to 10.

        Returns:
            A formatted string containing type definitions, each prefixed
            with comments indicating the type name, whether it's modernized
            or raw, and its expected include path. Returns empty string if
            no types are found.

        Example:
            >>> refs = {'SmartBuffer', 'TResult'}
            >>> context = generator.get_reference_context(refs)
            >>> '// Reference: SmartBuffer' in context
            True
        """
        context_parts = []
        included = 0
        
        for name in sorted(type_names):
            if included >= max_types:
                break
            
            type_def, is_processed = self.db.get_type_with_fallback(name)
            if type_def:
                # Calculate path
                file_path = self._get_type_filepath(name)
                path_info = f"// Defined in: \"{file_path}\"\n"

                if is_processed and type_def.get('processed_header'):
                    # Ensure we only include the core definition from the processed header
                    # (Though processed_header should already be just the header)
                    context_parts.append(f"// Reference: {name} (modernized)\n{path_info}{type_def['processed_header']}")
                    included += 1
                elif type_def.get('code'):
                    context_parts.append(f"// Reference: {name} (raw decompiled)\n{path_info}{type_def['code']}")
                    included += 1
        
        return "\n\n".join(context_parts)
    
    def analyze_class(self, class_name: str) -> Optional[str]:
        """Analyze a class structure and extract all referenced types using LLM.

        Performs an initial analysis pass on the class using the LLM to identify
        all type references in the struct definition, vtable, and methods. This
        analysis is more thorough than regex-based extraction and can identify
        types in complex contexts.

        The analysis combines LLM-extracted types with regex-based extraction
        for comprehensive coverage.

        Args:
            class_name: The name of the class to analyze.

        Returns:
            A JSON string containing analysis results with the following keys:
                - analysis: The raw LLM response.
                - referenced_types: Combined list of all discovered types.
                - llm_extracted_types: Types found by the LLM.
                - regex_extracted_types: Types found by regex patterns.
            Returns None if no LLM client is set or the class has no struct
            definition.

        Example:
            >>> analysis = generator.analyze_class("PlayerModule")
            >>> import json
            >>> data = json.loads(analysis)
            >>> 'referenced_types' in data
            True
        """
        if not self.llm:
            return None

        class_info = self.gather_class_info(class_name)
        if not class_info.get('struct'):
            return None
            
        struct = class_info.get('struct')
        struct_code = struct[5] if struct and len(struct) > 5 else ""
        
        # Find and resolve type references using regex patterns
        existing_references = self.find_type_references(class_info)
        methods_str = ""
        
        vtable_code = class_info.get('vtable', "")
        vtable_str = f"\nVTable:\n{vtable_code}\n" if vtable_code else ""
        
        # Build prompt for type extraction
        prompt = f"""Extract all type references from the following C++ class: {class_name}

Struct Definition:
{struct_code}
{vtable_str}

Methods:
{methods_str}
"""
        # Add nested types to analysis
        if class_info.get('nested'):
            prompt += "\nNested Types:\n"
            for nt in class_info['nested']:
                nt_code = nt['struct'][5] if nt['struct'] and len(nt['struct']) > 5 else ""
                prompt += f"--- Nested: {nt['name']} ---\n{nt_code}\n"
                nt_sigs = [self.extract_method_signature(m) for m in nt['methods']]
                if nt_sigs:
                    prompt += "Methods:\n" + "\n".join(nt_sigs) + "\n"

        prompt += """Task: Extract all type names referenced in the provided code.

Include:
- Base class names
- Member variable types
- Method parameter and return types
- Types used in method bodies
- Template parameter types
- Nested type references (e.g., std::vector<int> → both "std" and "vector" and "int")

Exclude:
- Built-in primitive types (int, char, bool, float, double, void, etc.)
- CV-qualifiers (const, volatile) and storage specifiers (static, extern)
- Pointer/reference symbols (*, &)

Output Requirements:
- Output ONLY a JSON array of unique type names
- One type per line for readability
- Alphabetically sorted
- No explanations or additional text

Example Input:
class Player : public GameObject {
    HealthComponent* _health;
    std::vector<Item*> _inventory;
    
    void TakeDamage(DamageInfo damage);
    Transform GetTransform();
};

Example Output:
[
  "DamageInfo",
  "GameObject",
  "HealthComponent",
  "Item",
  "Transform",
  "vector"
]
"""
        self._last_analysis_prompt = prompt
        analysis = self._call_llm(prompt)
        
        # Try to parse the response to get the list of referenced types
        # Instead of parsing as JSON, find all quoted strings which represent type names
        import re
        extracted_references = set()
        try:
            # Find all quoted strings in the response
            quoted_strings = re.findall(r'"([^"]*)"', analysis)
            if quoted_strings:
                extracted_references = set(quoted_strings)
        except Exception as e:
            # If parsing fails, use the existing regex-based extraction
            extracted_references = existing_references

        # Combine existing references with LLM-extracted references
        combined_references = existing_references | extracted_references
        
        # Format the analysis to include the combined references
        analysis_result = {
            "analysis": analysis,
            "referenced_types": list(combined_references),
            "llm_extracted_types": list(extracted_references),
            "regex_extracted_types": list(existing_references)
        }
        
        self._last_analysis = json.dumps(analysis_result, indent=2)
        return json.dumps(analysis_result)

    def build_prompt(self, class_info: Dict, reference_context: str = "", analysis: str = "",
                     method_definitions: List[str] = None, is_template: bool = False) -> str:
        """Build the LLM prompt for C++ header generation.

        Constructs a complete prompt for the LLM including the class definition,
        method signatures or definitions, vtable information, nested types,
        and reference context for dependent types. Selects appropriate few-shot
        examples based on whether the class is a template.

        Args:
            class_info: Dictionary containing class information as returned by
                gather_class_info(). Must include 'class_name' and 'struct'.
            reference_context: Optional string containing definitions of
                referenced types for context. Generated by get_reference_context().
            analysis: Optional analysis string (currently unused in prompt).
            method_definitions: Optional list of full method definition strings.
                If provided, these are included instead of just signatures.
                Required for template classes that need inline definitions.
            is_template: Whether this class is a template instantiation.
                Affects which few-shot example is included in the prompt.

        Returns:
            A complete prompt string ready to send to the LLM for header
            generation.

        Example:
            >>> info = generator.gather_class_info("PlayerModule")
            >>> context = generator.get_reference_context({'GameObject'})
            >>> prompt = generator.build_prompt(info, context)
            >>> 'Generate a clean, modern C++ header' in prompt
            True
        """
        struct = class_info.get('struct')
        struct_code = struct[5] if struct and len(struct) > 5 else ""
        
        # Build methods list
        if method_definitions:
            methods_str = "\n".join(f"// Method Definition:\n{defn}" for defn in method_definitions)
        else:
            # Build method signatures list
            method_sigs = []
            for method in class_info.get('methods', []):
                sig = self.extract_method_signature(method)
                if sig:
                    method_sigs.append(sig)
            
            methods_str = "\n".join(f"// Method Signature: {sig}" for sig in method_sigs) if method_sigs else "// No methods"
        
        vtable_code = class_info.get('vtable', "")
        vtable_str = f"\nVTable:\n{vtable_code}\n" if vtable_code else ""
        
        prompt = f"""Generate a clean, modern C++ header for the class: {class_info['class_name']}
 
Struct Definition:
{struct_code}
{vtable_str}

Methods:
{methods_str}
"""
        # Add nested types to prompt
        if class_info.get('nested'):
            prompt += "\nNested Types to include in this SAME header file:\n"
            for nt in class_info['nested']:
                nt_code = nt['struct'][5] if nt['struct'] and len(nt['struct']) > 5 else ""
                prompt += f"\n--- Nested Type: {nt['name']} ---\n"
                prompt += f"Definition:\n{nt_code}\n"
                
                nt_sigs = []
                for m in nt['methods']:
                    sig = self.extract_method_signature(m)
                    if sig: nt_sigs.append(sig)
                
                if nt_sigs:
                    prompt += "Methods:\n" + "\n".join(nt_sigs) + "\n"
        
        if reference_context:
            prompt += f"""
Referenced Types (for context only, do not redefine):
{reference_context}
"""
        
        few_shot = self.FEW_SHOT_TEMPLATE if is_template else self.FEW_SHOT_CLASS
        prompt += f"\n{few_shot}"

        return prompt

    def generate_header(self, class_name: str, save_to_db: bool = True, analysis: str = None) -> Optional[str]:
        """Generate a modernized C++ header for the specified class.

        Main entry point for header generation. Gathers class information,
        finds type references, builds the LLM prompt, calls the LLM, and
        optionally stores the result in the database.

        The generation process:
        1. Gathers struct definition and methods from database
        2. Finds all referenced types (via regex and optional analysis)
        3. Gets context definitions for referenced types
        4. Builds prompt with appropriate few-shot examples
        5. Calls LLM to generate modernized header
        6. Optionally saves result to database

        Args:
            class_name: Name of the class to generate a header for.
                Can include namespace (e.g., "Turbine::PlayerModule").
            save_to_db: Whether to save the generated header to the database.
                Defaults to True.
            analysis: Optional JSON analysis string from analyze_class().
                If provided, type references are extracted from it to
                supplement regex-based discovery.

        Returns:
            The generated C++ header code as a string, or None if:
            - No LLM client is configured
            - No struct definition exists for the class

        Raises:
            ValueError: If no LLM client is set.

        Example:
            >>> header = generator.generate_header("PlayerModule")
            >>> header.startswith('#pragma once')
            True
        """
        if not self.llm:
            raise ValueError("LLM client not set. Set it via generator.llm = client")
        
        # Gather class information
        class_info = self.gather_class_info(class_name)
        
        if not class_info.get('struct'):
            logger.warning(f"No struct definition found for {class_name}")
            return None
        
        # Start with references found via regex patterns
        references = self.find_type_references(class_info)
        
        # If analysis is provided, extract types from it by finding quoted strings
        if analysis:
            import re
            try:
                # Find all quoted strings in the analysis (these represent type names)
                quoted_strings = re.findall(r'"([^"]*)"', analysis)
                if quoted_strings:
                    analysis_references = set(quoted_strings)
                    # Combine with regex-based references
                    references = references | analysis_references
            except Exception as e:
                # If parsing fails, just use the regex-based references
                pass
        
        # Combine all references - deduplicate by using set operations
        all_references = {f for f in references if "<" not in f}

        # Get context for all referenced types
        context = self.get_reference_context(all_references)
        
        # Gather full method definitions for templates
        method_definitions = None
        method_definitions = [m[3] for m in class_info.get('methods', [])]
        
        # Track types for debug
        self._last_types = {
            "referenced": list(all_references),
            "context_preview": context[:500] + "..." if len(context) > 500 else context
        }
        
        # Build prompt (no longer include analysis text, just use extracted types)
        is_template = self.is_template_instantiation(class_name)
        prompt = self.build_prompt(class_info, context, analysis=None, # No analysis text in prompt
                                   method_definitions=method_definitions,
                                   is_template=is_template)
        self._last_prompt = prompt
        
        # Call LLM
        header = self._call_llm(prompt)
        self._last_response = header
        
        # Write debug output
        self._write_debug(class_name)
        
        if header and save_to_db:
            struct = class_info['struct']
            original_code = struct[5] if struct and len(struct) > 5 else ""
            engine_name = self.llm.name if self.llm and hasattr(self.llm, 'name') else "lm-studio"
            self.db.store_processed_type(
                name=class_name,
                type_kind='struct',
                original_code=original_code,
                processed_header=header,
                dependencies=list(all_references),
                engine_used=engine_name
            )
        
        return header

    def _call_llm(self, prompt: str) -> str:
        """Call the LLM with the given prompt and return the response.

        Wrapper method that handles different LLM client interfaces. The client
        can either have a 'generate' method or be directly callable.

        This method can be overridden in subclasses for custom LLM integration
        or retry logic.

        Args:
            prompt: The complete prompt string to send to the LLM.

        Returns:
            The LLM's response text.

        Raises:
            NotImplementedError: If the LLM client doesn't have a 'generate'
                method and isn't callable.

        Example:
            >>> response = generator._call_llm("Generate a header for...")
        """
        if hasattr(self.llm, 'generate'):
            return self.llm.generate(prompt)
        elif callable(self.llm):
            return self.llm(prompt)
        else:
            raise NotImplementedError("LLM client must have a 'generate' method or be callable")
    
    def write_header_file(self, class_name: str, header_code: str,
                          output_dir: Path, namespace: str = None) -> Path:
        """Write the generated header code to a file on disk.

        Creates the appropriate directory structure based on namespace and
        writes the header content to a .h file. Directories are created
        as needed.

        The output path follows the convention:
        - With namespace: output_dir/include/{namespace}/{class_name}.h
        - Without namespace: output_dir/include/{class_name}.h

        Args:
            class_name: Name of the class (used as the filename).
            header_code: The generated header content to write.
            output_dir: Base output directory. The 'include' subdirectory
                will be created under this path.
            namespace: Optional namespace for subdirectory organization.
                Namespace separators (::) are converted to directory
                separators.

        Returns:
            Path object pointing to the written header file.

        Example:
            >>> path = generator.write_header_file(
            ...     "PlayerModule",
            ...     header_code,
            ...     Path("output"),
            ...     namespace="Turbine"
            ... )
            >>> path
            PosixPath('output/include/Turbine/PlayerModule.h')
        """
        # Build output path
        if namespace:
            header_dir = output_dir / "include" / namespace.replace('::', '/')
        else:
            header_dir = output_dir / "include"
        
        header_dir.mkdir(parents=True, exist_ok=True)
        header_path = header_dir / f"{class_name}.h"
        
        header_path.write_text(header_code, encoding='utf-8')
        
        return header_path
