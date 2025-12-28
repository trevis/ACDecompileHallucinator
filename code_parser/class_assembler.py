"""
Class Assembler
===============
Assembles final C++ output files from processed components.
No LLM needed - just combines processed headers and method implementations.
"""
from typing import List, Optional
from pathlib import Path
from dataclasses import dataclass


@dataclass
class ProcessedMethod:
    """Represents a processed method ready for assembly"""
    name: str
    full_name: str
    parent_class: Optional[str]
    processed_code: str
    dependencies: List[str]
    offset: str


class ClassAssembler:
    """Assembles final C++ source files from processed components.

    This is a non-LLM component that simply combines:
    - Generated headers (from ClassHeaderGenerator)
    - Processed method implementations (from FunctionProcessor)
    """

    def __init__(self, output_dir: Path):
        """Initialize the assembler with the output directory.

        Creates the output directory if it doesn't exist.

        Args:
            output_dir: Base output directory for generated files.
                Subdirectories for include/ and src/ will be created
                as needed when writing files.
        """
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
    
    def assemble_source(self, class_name: str,
                        processed_methods: List[ProcessedMethod],
                        namespace: Optional[str] = None) -> str:
        """Combine processed methods into a .cpp source file content string.

        Generates the source file content by adding the appropriate include
        directive and concatenating all processed method implementations.

        Args:
            class_name: Name of the class being assembled.
            processed_methods: List of ProcessedMethod objects containing
                the modernized method implementations.
            namespace: Optional namespace for the class. Used to construct
                the include path (e.g., "Turbine" -> "Turbine/ClassName.h").

        Returns:
            Complete source file content as a string, including the include
            directive and all method implementations.
        """
        lines = []
        if namespace:
            # Use forward slash for include path regardless of OS
            include_path = f"{namespace.replace('::', '/')}/{class_name}.h"
            lines.append(f'#include "{include_path}"')
        else:
            lines.append(f'#include "{class_name}.h"')
        lines.append('')
        
        for method in processed_methods:
            # Add the processed code
            code = method.processed_code.strip()
            if code:
                lines.append(code)
                lines.append('')
        
        return '\n'.join(lines)
    
    def write_source_file(self, class_name: str,
                          processed_methods: List[ProcessedMethod],
                          namespace: Optional[str] = None) -> Path:
        """Write the assembled source file to disk.

        Filters out methods with empty code, assembles the remaining methods
        into a .cpp file, and writes it to the appropriate location under
        the src/ subdirectory.

        Args:
            class_name: Name of the class being written.
            processed_methods: List of ProcessedMethod objects to include.
                Methods with empty processed_code are filtered out.
            namespace: Optional namespace for organizing the output. Creates
                a subdirectory structure matching the namespace
                (e.g., "Turbine::UI" -> "src/Turbine/UI/").

        Returns:
            Path to the written .cpp file, or None if no methods had
            non-empty code to write.
        """
        # Skip if no methods with actual code
        non_empty_methods = [m for m in processed_methods if m.processed_code.strip()]
        if not non_empty_methods:
            return None
        
        # Build output path
        if namespace:
            source_dir = self.output_dir / "src" / namespace.replace('::', '/')
        else:
            source_dir = self.output_dir / "src"
        
        source_dir.mkdir(parents=True, exist_ok=True)
        source_path = source_dir / f"{class_name}.cpp"
        
        # Generate and write content
        content = self.assemble_source(class_name, non_empty_methods, namespace)
        source_path.write_text(content, encoding='utf-8')
        
        return source_path
    
    def write_header_file(self, class_name: str,
                          header_code: str,
                          namespace: Optional[str] = None,
                          path: Optional[Path] = None) -> Path:
        """Write a header file to disk.

        Writes the generated header content to the appropriate location.
        If a specific path is provided, uses that; otherwise, constructs
        the path based on the class name and optional namespace under
        the include/ subdirectory.

        Args:
            class_name: Name of the class for the header file name.
            header_code: The complete header file content to write.
            namespace: Optional namespace for organizing the output. Creates
                a subdirectory structure matching the namespace
                (e.g., "Turbine" -> "include/Turbine/").
            path: Optional explicit path to write to. If provided, overrides
                the default path construction based on namespace.

        Returns:
            Path to the written .h file, or None if header_code was
            empty or whitespace-only.
        """
        if not header_code or not header_code.strip():
            return None
        
        if path:
            header_path = path
            header_path.parent.mkdir(parents=True, exist_ok=True)
        else:
            # Build output path
            if namespace:
                header_dir = self.output_dir / "include" / namespace.replace('::', '/')
            else:
                header_dir = self.output_dir / "include"
            
            header_dir.mkdir(parents=True, exist_ok=True)
            header_path = header_dir / f"{class_name}.h"
        
        header_path.write_text(header_code, encoding='utf-8')
        
        return header_path
    
    def write_enum_header(self, enum_name: str,
                          enum_code: str,
                          namespace: Optional[str] = None) -> Path:
        """Write an enum header file to disk.

        Writes the enum definition to a header file. Automatically adds
        '#pragma once' if not already present. Enums are written directly
        without LLM processing since they don't require modernization.

        Args:
            enum_name: Name of the enum for the header file name.
            enum_code: The enum definition code to write.
            namespace: Optional namespace for organizing the output. Creates
                a subdirectory structure matching the namespace
                (e.g., "Turbine" -> "include/Turbine/").

        Returns:
            Path to the written .h file, or None if enum_code was empty.
        """
        if not enum_code:
            return None
        
        # Build output path
        if namespace:
            header_dir = self.output_dir / "include" / namespace.replace('::', '/')
        else:
            header_dir = self.output_dir / "include"
        
        header_dir.mkdir(parents=True, exist_ok=True)
        header_path = header_dir / f"{enum_name}.h"
        
        # Add pragma once if not present
        content = enum_code
        if not content.strip().startswith('#pragma once'):
            content = '#pragma once\n\n' + content
        
        header_path.write_text(content, encoding='utf-8')
        
        return header_path
