import logging
from dataclasses import dataclass
from typing import Optional, Dict, List
from pathlib import Path
from .constants import should_ignore_global_method, should_ignore_class_method, should_ignore_class

logger = logging.getLogger(__name__)


@dataclass
class Method:
    """Represents a parsed method with its metadata"""
    name: str = ""
    full_name: str = ""
    definition: str = ""
    namespace: Optional[str] = None
    parent: Optional[str] = None
    is_generic: bool = False
    is_ignored: bool = False
    offset: str = ""
    return_type: str = ""
    file: str = ""
    
    FUNC_MODIFIERS = [
        '__cdecl', '__stdcall', '__thiscall', '__userpurge',
        '__usercall', '__fastcall', '__noreturn', '__spoils<ecx>'
    ]
    
    @property
    def safe_name(self) -> str:
        """Returns a filesystem-safe version of the method name.

        Transforms the full name by replacing characters that are problematic
        for filesystems (::, <, >, commas, spaces) with underscores. Also
        removes vtbl suffixes and truncates to 100 characters if needed.

        Returns:
            A sanitized name safe for use in file paths.
        """
        # Replace problematic characters that can cause long filenames
        safe_name = self.full_name.replace('::', '__').replace('_vtbl', '')
        # Handle template characters that can cause extremely long names
        safe_name = safe_name.replace('<', '_').replace('>', '_').replace(',', '_').replace(' ', '_')
        # Truncate if too long for filesystem limits
        if len(safe_name) > 100:
            safe_name = safe_name[:100]
        return safe_name
    
    @property
    def is_global(self) -> str:
        """Checks if this method is a global function.

        A method is considered global if it has no namespace separator (::)
        in its full name, indicating it is not a class member.

        Returns:
            True if the method is a global function, False if it belongs
            to a class or namespace.
        """
        return "::" not in self.full_name
    
    @property
    def simple_name(self) -> str:
        """Returns the method name without namespace or vtbl suffix.

        Returns:
            The base method name with any _vtbl suffix removed.
        """
        return self.name.replace('_vtbl', '')
    
    def clean_definition(self, def_line: str) -> str:
        """Remove calling convention modifiers from a function definition.

        Strips IDA-specific calling convention modifiers like __cdecl,
        __thiscall, __userpurge, etc. that are artifacts of decompilation.

        Args:
            def_line: The raw function definition line from decompiled code.

        Returns:
            The cleaned definition line with modifiers removed.
        """
        for modifier in self.FUNC_MODIFIERS:
            def_line = def_line.replace(modifier, '')
        return def_line
    
    def get_out_file(self, src_path: str, structs_dict: Dict[str, any] = None) -> Path:
        """Determine the output file path for this method.

        Computes the appropriate output path based on the method's parent
        class and namespace. Methods are grouped by their parent class
        to avoid extremely long filenames from template instantiations.

        Args:
            src_path: Base path for output files.
            structs_dict: Optional dictionary mapping struct names to Struct
                objects. Used to determine output file for class methods.

        Returns:
            Path object pointing to the output .cpp file location.
        """
        # Use the parent class name for the file, not the full method name
        # This prevents extremely long filenames with complex template instantiations
        if self.parent and self.parent in structs_dict:
            # If parent exists in structs_dict, use parent's get_out_file method
            out_file = structs_dict[self.parent].get_out_file(src_path, structs_dict)
        elif self.parent:
            # Use parent class name for the file, not the full method name
            # Clean the parent name to make it filesystem-safe
            safe_parent = self.parent.replace('::', '__').replace('<', '_').replace('>', '_').replace(',', '_').replace(' ', '_').replace('*', '_').replace('?', '_').replace('"', '_').replace('|', '_')
            # Truncate if too long
            if len(safe_parent) > 100:
                safe_parent = safe_parent[:100]
            
            if self.namespace:
                # Sanitize namespace for Windows filesystem
                safe_ns = self.namespace.split('::')[0].replace('<', '_').replace('>', '_').replace(',', '_').replace(' ', '_').replace('*', '_').replace('?', '_').replace('"', '_').replace('|', '_')
                namespace_dir = src_path / safe_ns
                namespace_dir.mkdir(exist_ok=True)
                out_file = namespace_dir / f"{safe_parent}.cpp"
            else:
                out_file = src_path / f"{safe_parent}.cpp"
        elif self.namespace:
            # Check if namespace itself is a struct
            if structs_dict and self.namespace in structs_dict:
                out_file = src_path / f"{self.safe_name.split('__')[0]}.cpp"
            else:
                # Clean namespace for filename
                safe_namespace = self.namespace.replace('::', '__').replace('<', '_').replace('>', '_').replace(',', '_').replace(' ', '_').replace('*', '_').replace('?', '_').replace('"', '_').replace('|', '_')
                # Truncate if too long
                if len(safe_namespace) > 10:
                    safe_namespace = safe_namespace[:100]
                    
                # Sanitize namespace directory for Windows filesystem
                safe_ns_dir = self.namespace.split('::')[0].replace('<', '_').replace('>', '_').replace(',', '_').replace(' ', '_').replace('*', '_').replace('?', '_').replace('"', '_').replace('|', '_')
                namespace_dir = src_path / safe_ns_dir
                namespace_dir.mkdir(exist_ok=True)
                out_file = namespace_dir / f"{safe_namespace}.cpp"
        else:
            # For global functions, use safe_name but truncate if needed
            safe_name = self.safe_name.replace('<', '_').replace('>', '_').replace(',', '_').replace(' ', '_').replace('*', '_').replace('?', '_').replace('"', '_').replace('|', '_')
            if len(safe_name) > 100:
                safe_name = safe_name[:100]
            out_file = src_path / f"{safe_name}.cpp"
        
        return out_file

    def write_to_file(self, src_path: str, structs: Dict[str, any]):
        """Write the method definition to its output file.

        Appends the method with its offset comment to the appropriate output
        file. If the method has a parent class, also registers the method
        with that struct's methods list.

        Args:
            src_path: Base path for output files.
            structs: Dictionary mapping struct names to Struct objects,
                used for determining output file location and registering
                methods with their parent structs.
        """
        out_file = self.get_out_file(src_path, structs)

        if self.parent and self.parent in structs:
            structs[self.parent].methods.append(self)

        with open(out_file, 'a') as f:
            f.write(f"// Function Offset: 0x{self.offset}\n")
            f.write(f"{self.definition}\n\n")

    def extract_func_name(self, def_line: str):
        """Extract function name, return type, parent class, and namespace.

        Parses a function signature to extract its components. Handles
        special cases like operator overloads, destructors, and template
        functions. Updates this Method instance with the extracted data.

        Args:
            def_line: The function definition line (e.g., "int Foo::bar(int x)").

        Returns:
            A tuple of (simple_name, None) where simple_name is the function
            name without namespace. The second element is always None for
            compatibility.

        Raises:
            ValueError: If no opening parenthesis is found in the signature.
        """
        def_line = self.clean_definition(def_line)
        def_line = def_line.replace(' * *', '**')
        def_line = def_line.replace(' * ', '* ')
        def_line = def_line.replace("`vector deleting destructor'", 'VectorDeletingDestructor')
        def_line = def_line.replace("`scalar deleting destructor'", 'ScalarDeletingDestructor')
        def_line = def_line.replace("operator>", "operatorGreaterThan")
        def_line = def_line.replace("operator<", "operatorLessThan")
        def_line = def_line.replace("@<eax>", "")
        def_line = def_line.replace("@<al>", "")

        # Find the last opening parenthesis for arguments
        last_paren = def_line.find('(')
        if last_paren == -1:
            raise ValueError("No opening parenthesis found in signature")
        
        # Extract everything after the last '(' as arguments
        args_start = last_paren + 1
        args_string = def_line[args_start:].rstrip(')')
        
        # Now work backwards from the '(' to find the function name
        # We need to count <> pairs to handle templates
        i = last_paren - 1
        angle_depth = 0
        func_name_end = last_paren
        
        # Traverse left, counting <> pairs
        while i >= 0:
            char = def_line[i]
            
            if char == '>':
                angle_depth += 1
            elif char == '<':
                angle_depth -= 1
            elif char == ' ' and angle_depth <= 0:
                # Found a space outside of template brackets
                # This marks the end of the function name
                break
            
            i -= 1
        
        # Function name is from i+1 to last_paren
        func_name_start = i + 1
        self.name = def_line[func_name_start:func_name_end]
        
        # Return type is everything before the function name
        self.return_type = def_line[:func_name_start].strip()

        if self.name.startswith('*'):
            self.name = self.name[1:]
            self.return_type = self.return_type + '*'

        if self.return_type == "":
            logger.warning(f"Bad return type: {def_line}")

        self.full_name = self.name

        if ("::" in self.name):
            parts = self.name.split("::")
            self.name = parts[-1]
            self.parent = "::".join(parts[:-1])

        if self.parent and "::" in self.parent:
            parts = self.parent.split("::")
            self.parent = parts[-1]
            self.namespace = "::".join(parts[:-1])

        self.name = self.name.replace("operatorGreaterThan", "operator>")
        self.name  = self.name.replace("operatorLessThan", "operator<")
        self.name = self.name.replace('VectorDeletingDestructor', "`vector deleting destructor'")
        self.name = self.name.replace('ScalarDeletingDestructor', "`scalar deleting destructor'")

        return self.simple_name, None

    def parse(self, line: str, lines: List[str], i: int) -> int:
        """Parse a function definition from source lines.

        Extracts the function offset, name, body, and metadata from the
        source lines. Updates this Method instance with the parsed data
        and determines if the method should be ignored based on naming
        conventions.

        Args:
            line: The function header line containing the offset.
            lines: List of all source lines being parsed.
            i: Current line index in the lines list.

        Returns:
            A tuple of (new_index, simple_name, method) where new_index is
            the line index after parsing, simple_name is the function name
            (or None if parsing failed), and method is the populated Method
            instance (or None if parsing failed).
        """
        func_buffer = []
        parts = line.split(' ')
        if len(parts) < 2:
            logger.warning(f"Bad line format: {line}")
        else:
            self.offset = line.split(' ')[1].strip('()')

        i = i + 1
        def_line = lines[i]

        while def_line.startswith('//'):
            i = i + 1
            def_line = lines[i]

        if def_line.startswith('#'):
            return i, None, None

        simple_name, parent = self.extract_func_name(def_line)

        # Collect function body
        while lines[i] != "}" and i < len(lines) - 1:
            func_buffer.append(lines[i])
            i += 1
        func_buffer.append(lines[i])
        i += 1

        self.definition = "\n".join(func_buffer)

        if self.name.startswith('`'):
            self.is_ignored = True
        elif self.is_global:
            self.is_ignored = should_ignore_global_method(self.name)
        else:
            self.is_ignored = should_ignore_class_method(self.full_name)
        
        if self.namespace and self.namespace.startswith('`'):
            self.is_ignored = True

        if self.parent and (should_ignore_class(self.parent) or self.parent.startswith('`')):
            self.is_ignored = True
        
        if "DeletingDestructor" in self.name or 'deleting destructor' in self.name:
            self.is_ignored = True

        return i, simple_name, self