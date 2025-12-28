from dataclasses import dataclass
from typing import Optional, Dict, List
from pathlib import Path
from .constants import should_ignore_class
from .method import Method

@dataclass
class Struct:
    """Represents a parsed struct with its metadata"""
    name: str = ""
    definition: str = ""
    namespace: Optional[str] = None
    parent: Optional[str] = None
    is_generic: bool = False
    is_ignored: bool = False
    comment: str = ""
    methods: List = None 
    
    STRUCT_MODIFIERS = [
        '__unaligned ', '__cppobj ', '/*VFT*/ ',
        '__declspec(align(1)) ', '__declspec(align(2)) ',
        '__declspec(align(4)) ', '__declspec(align(8)) '
    ]
    
    @property
    def full_name(self) -> str:
        """Returns the fully qualified name including namespace.

        Returns:
            The fully qualified name in the format "namespace::name" if a
            namespace exists, otherwise just the name.
        """
        if self.namespace:
            return f"{self.namespace}::{self.name}"
        return self.name
    
    @property
    def safe_name(self) -> str:
        """Returns a filesystem-safe version of the struct name.

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
    def simple_name(self) -> str:
        """Returns the struct name without namespace or vtbl suffix.

        Returns:
            The base struct name with any _vtbl suffix removed.
        """
        return self.name.replace('_vtbl', '')
    
    def get_comment_header(self) -> str:
        """Generate a C++ comment header for the struct.

        Returns:
            A formatted comment string identifying the struct by its
            fully qualified name.
        """
        comment = f"// Struct {self.full_name}\n"
        return comment
    
    def extract_struct_name(self, def_line: str) -> tuple[str, Optional[str]]:
        """Extract the struct name and parent class from a definition line.

        Parses a struct definition line to extract the struct name and any
        parent class specified via inheritance syntax.

        Args:
            def_line: The struct definition line (e.g., "struct Foo : Bar").

        Returns:
            A tuple of (struct_name, parent) where parent is None if no
            inheritance is specified.
        """
        def_line = def_line.replace('struct ', '')
        parts = def_line.split(' : ')
        struct_name = parts[0].strip()
        parent = parts[1].strip() if len(parts) > 1 else None
        return struct_name, parent
    
    def parse_namespace(self, full_name: str) -> tuple[Optional[str], str]:
        """Split a fully qualified name into namespace and simple name.

        Args:
            full_name: The fully qualified name (e.g., "Turbine::Physics::Body").

        Returns:
            A tuple of (namespace, simple_name) where namespace is None if
            no :: separator is present. For nested namespaces, all parts
            except the last are joined as the namespace.
        """
        parts = full_name.split("::")
        if len(parts) == 1:
            return None, full_name
        return "::".join(parts[:-1]), parts[-1]

    def clean_struct_definition(self, def_line: str) -> str:
        """Remove IDA-specific modifiers from a struct definition line.

        Strips modifiers like __unaligned, __cppobj, VFT markers, and
        alignment directives that are artifacts of decompilation.

        Args:
            def_line: The raw struct definition line from decompiled code.

        Returns:
            The cleaned definition line with modifiers removed.
        """
        for modifier in self.STRUCT_MODIFIERS:
            def_line = def_line.replace(modifier, '')
        return def_line
    
    def get_out_file(self, src_path: str, structs_dict: Dict[str, any] = None) -> Path:
        """Determine the output file path for this struct.

        Computes the appropriate output path based on the struct's namespace
        and whether its namespace corresponds to another struct. Creates
        namespace directories as needed.

        Args:
            src_path: Base path for output files.
            structs_dict: Optional dictionary mapping struct names to Struct
                objects. Used to check if the namespace is itself a struct.

        Returns:
            Path object pointing to the output .cpp file location.
        """
        # Clean the safe_name to handle template instantiations that result in very long names
        safe_name_clean = self.safe_name.replace('<', '_').replace('>', '_').replace(',', '_').replace(' ', '_').replace('*', '_').replace('?', '_').replace('"', '_').replace('|', '_')
        if len(safe_name_clean) > 100:
            safe_name_clean = safe_name_clean[:100]

        if self.namespace:
            # Check if namespace itself is a struct
            if structs_dict and self.namespace in structs_dict:
                out_file = src_path / f"{safe_name_clean.split('__')[0]}.cpp"
            else:
                # Sanitize namespace for Windows filesystem
                safe_namespace = self.namespace.split('::')[0].replace('<', '_').replace('>', '_').replace(',', '_').replace(' ', '_').replace('*', '_').replace('?', '_').replace('"', '_').replace('|', '_')
                namespace_dir = src_path / safe_namespace
                namespace_dir.mkdir(exist_ok=True)
                out_file = namespace_dir / f"{safe_name_clean.split('__')[-1]}.cpp"
        else:
            out_file = src_path / f"{safe_name_clean}.cpp"

        return out_file

    def write_to_file(self, src_path: str, structs_dict: Dict[str, any]):
        """Write the struct definition to its output file.

        Appends the struct with its comment header to the appropriate output
        file, creating the file if it doesn't exist.

        Args:
            src_path: Base path for output files.
            structs_dict: Dictionary mapping struct names to Struct objects,
                used for determining output file location.
        """
        out_file = self.get_out_file(src_path, structs_dict)


        with open(out_file, 'a') as f:
            f.write(f"{self.get_comment_header()}{self.definition}\n\n")

    def parse_struct(self, def_line: str, lines: List[str], i: int) -> int:
        """Parse a struct definition from source lines.

        Parses either a forward declaration or a full struct definition,
        extracting the name, namespace, parent class, and body. Updates
        this Struct instance with the parsed data.

        Args:
            def_line: The struct definition line (e.g., "struct Foo : Bar {").
            lines: List of all source lines being parsed.
            i: Current line index in the lines list.

        Returns:
            A tuple of (new_index, simple_name, struct) where new_index is
            the line index after parsing, simple_name is the struct name
            without namespace, and struct is the populated Struct instance.
        """
        self.methods = []
        def_line = self.clean_struct_definition(def_line)
        comment = lines[i - 1]

        # Handle forward declarations
        if def_line.endswith(";"):
            struct_name = def_line.replace('struct ', '').replace(';', '')
            namespace, simple_name = self.parse_namespace(struct_name)
            struct = Struct(
                name=simple_name,
                definition=f"{struct_name}\n{{\n}};",
                namespace=namespace
            )
            return i, simple_name, struct

        # Parse full struct definition
        struct_name, parent = self.extract_struct_name(def_line)
        namespace, simple_name = self.parse_namespace(struct_name)

        struct_buffer = [def_line]
        i += 1

        # Collect struct body
        while lines[i] != "};":
            struct_buffer.append(lines[i])
            i += 1
        struct_buffer.append(lines[i])
        i += 1

        # Fill Struct object
        self.name = simple_name
        self.definition = "\n".join(struct_buffer)
        self.namespace = namespace
        self.parent = parent
        self.is_generic = '<' in struct_name
        self.is_ignored = should_ignore_class(struct_name)
        self.comment = comment

        return i, simple_name, self