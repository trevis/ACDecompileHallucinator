from dataclasses import dataclass
from typing import Optional, Dict, List
from pathlib import Path
from .constants import should_ignore_class


@dataclass
class Enum:
    """Represents a parsed enum with its metadata"""
    name: str = ""
    definition: str = ""
    namespace: Optional[str] = None
    is_ignored: bool = False
    comment: str = ""
    
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
        """Returns a filesystem-safe version of the enum name.

        Transforms the full name by replacing namespace separators (::)
        with double underscores.

        Returns:
            A sanitized name safe for use in file paths.
        """
        return self.full_name.replace('::', '__')
    
    @property
    def simple_name(self) -> str:
        """Returns the enum name without namespace.

        Returns:
            The base enum name without any namespace prefix.
        """
        return self.name
    
    def get_comment_header(self) -> str:
        """Generate a C++ comment header for the enum.

        Creates a formatted comment including the enum's simple name,
        fully qualified name, and any associated comment from parsing.

        Returns:
            A formatted comment string identifying the enum with its
            full context.
        """
        comment = f"// Enum {self.simple_name} -- "
        if self.namespace:
            comment = comment + f"{self.namespace}::"
        comment = comment + f"{self.name} {self.comment}\n"
        return comment

    def parse_namespace(self, full_name: str) -> tuple[Optional[str], str]:
        """Split a fully qualified name into namespace and simple name.

        Args:
            full_name: The fully qualified name (e.g., "Turbine::ErrorCode").

        Returns:
            A tuple of (namespace, simple_name) where namespace is None if
            no :: separator is present. For nested namespaces, only the
            first part is used as namespace.
        """
        parts = full_name.split("::")
        if len(parts) == 1:
            return None, full_name
        return  parts[0], parts[-1]

    def write_to_file(self, enums_path: str, structs: Dict[str, any] = None):
        """Write the enum definition to its output file.

        Determines the appropriate output file based on whether the enum's
        namespace corresponds to a struct. If so, writes to the struct's file;
        otherwise creates a file based on the enum's namespace or name.

        Args:
            enums_path: Base path for output files.
            structs: Optional dictionary mapping struct names to Struct objects.
                If the enum's namespace matches a struct, the enum is written
                to that struct's output file.
        """
        # If structs are provided and the enum's namespace has a struct defined,
        # write the enum to the struct file instead of its own file
        if self.namespace and structs and self.namespace in structs:
                out_file = structs[self.namespace].get_out_file(enums_path)
        else:
            # Default behavior if no structs provided or no namespace
            if self.namespace:
                namespace_dir = enums_path / self.namespace.split('::')[0]
                namespace_dir.mkdir(exist_ok=True)
                out_file = namespace_dir / f"{self.safe_name.split('__')[-1]}.cpp"
            else:
                out_file = enums_path / f"{self.safe_name}.cpp"

        with open(out_file, 'a') as f:
            f.write(f"{self.get_comment_header()}{self.definition}\n\n")

    def parse_enum(self, def_line: str, lines: List[str], i: int) -> int:
        """Parse an enum definition from source lines.

        Extracts the enum name, namespace, and body from the source lines.
        Updates this Enum instance with the parsed data.

        Args:
            def_line: The enum definition line (e.g., "enum Foo {").
            lines: List of all source lines being parsed.
            i: Current line index in the lines list.

        Returns:
            A tuple of (new_index, enum_name, enum) where new_index is
            the line index after parsing, enum_name is the simple name,
            and enum is the populated Enum instance.
        """
        comment = lines[i - 1] if i > 0 else ""
        # Handle regular enum
        # Extract enum name from definition line
        def_line_clean = def_line.replace('enum ', '').replace('struct ', '').strip()
        if '{' in def_line_clean:
            # Enum with inline definition
            enum_name = def_line_clean.split('{')[0].strip()
        else:
            enum_name = def_line_clean.split()[0] if def_line_clean.split() else ""

        if "::" in enum_name:
            namespace, simple_name = self.parse_namespace(enum_name)
            self.name = simple_name
            self.namespace = namespace
        else:
            self.name = enum_name
            self.namespace = None

        # Collect enum body
        enum_buffer = [def_line]
        i += 1
        while i < len(lines) and lines[i].strip() != "};":
            enum_buffer.append(lines[i])
            i += 1
        if i < len(lines):
            enum_buffer.append(lines[i])  # Add closing };
            i += 1

        self.definition = "\n".join(enum_buffer)
        self.comment = comment
        self.is_ignored = should_ignore_class(self.full_name)

        return i, self.name, self
