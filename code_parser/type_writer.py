"""
Type Writer - Manages output of parsed types to files and database.
"""
import shutil
from pathlib import Path
from typing import List

from .struct import Struct
from .enum import Enum
from .method import Method
from .db_handler import DatabaseHandler


class TypeWriter:
    """Writes parsed types to files and/or database"""
    
    def __init__(self, output_path: str, use_database: bool = True):
        """Initialize the type writer with output configuration.

        Creates the output directory structure and optionally initializes
        a database for storing parsed types. If the output directory exists,
        it will be completely removed and recreated.

        Args:
            output_path: Path to the output directory where files will be written.
            use_database: If True, creates a SQLite database (types.db) in the
                output directory for storing parsed type information.
        """
        self.output_path = Path(output_path)
        self.src_path = self.output_path / 'src'
        self.use_database = use_database

        if self.output_path.exists():
            shutil.rmtree(self.output_path)
        self.output_path.mkdir(parents=True)
        self.src_path.mkdir(parents=True)

        if use_database:
            self.db_path = self.output_path / 'types.db'
            self.db_handler = DatabaseHandler(self.db_path)
        
    def write_typedefs(self, typedefs: List[str]):
        """Write typedef declarations to a summary file.

        Writes all typedefs sorted alphabetically to 'typedefs.txt' in the
        output directory.

        Args:
            typedefs: List of typedef declaration strings to write.
        """
        with open(self.output_path / 'typedefs.txt', 'w') as f:
            for typedef in sorted(typedefs):
                f.write(f"{typedef}\n")
    
    def write_funcs(self, funcs: Dict[str, Method], structs: Dict[str, Struct],
                    replacer=None):
        """Write method/function definitions to files and database.

        Categorizes functions into four groups based on scope (global vs class)
        and ignored status, writing summary files for each category. Non-ignored
        functions are also written to individual source files and optionally
        stored in the database.

        Args:
            funcs: Dictionary mapping function names to Method objects.
            structs: Dictionary mapping struct names to Struct objects, used
                for resolving parent class information during file output.
            replacer: Optional constant replacer object with a process_code()
                method for substituting named constants in function bodies.
        """
        categories = {
            'class_functions.txt': lambda e: not e.is_global and not e.is_ignored,
            'class_functions_ignored.txt': lambda e: not e.is_global and e.is_ignored,
            'global_functions.txt': lambda e: e.is_global and not e.is_ignored,
            'global_functions_ignored.txt': lambda e: e.is_global and e.is_ignored
        }
        for filename, filter_func in categories.items():
            with open(self.output_path / filename, 'w') as f:
                found_funcs = []
                for k in sorted(funcs.keys()):
                    func = funcs[k]
                    if filter_func(func):
                        found_funcs.append(f"{func.full_name}\n")
                        if not func.is_ignored:
                            # Apply constant replacement if available
                            if replacer:
                                # We need to modify the definition temporarily or pass it 
                                # to a modified write_to_file... 
                                # But Method.write_to_file uses self.definition.
                                # Let's patch the definition.
                                original_def = func.definition
                                try:
                                    func.definition = replacer.process_code(func.definition)
                                    func.write_to_file(self.src_path, structs)
                                    
                                    # Store modified method to database
                                    if self.use_database:
                                        self.db_handler.store_method(func)
                                finally:
                                    # Restore original for safety (though we might not need to)
                                    func.definition = original_def
                            else:
                                func.write_to_file(self.src_path, structs)
                                if self.use_database:
                                    self.db_handler.store_method(func)

                sorted_funcs = sorted(found_funcs)
                f.write("".join(sorted_funcs))

    def write_enums(self, enums: Dict[str, Enum], structs: Dict[str, Struct] = None):
        """Write enum definitions to files and database.

        Separates enums into ignored and non-ignored categories, writing
        summary files for each. Non-ignored enums are written to individual
        source files organized by namespace and stored in the database.

        Args:
            enums: Dictionary mapping enum names to Enum objects.
            structs: Optional dictionary of structs for namespace resolution
                during file output.
        """
        categories = {
            'enums.txt': lambda e: not e.is_ignored,
            'enums_ignored.txt': lambda e: e.is_ignored
        }
        
        for filename, filter_func in categories.items():
            written_enums = []
            found_enums = []
            with open(self.output_path / filename, 'w') as summary_file:
                for name in sorted(enums.keys()):
                    enum = enums[name]
                    if not enum.comment:
                        continue
                    if filter_func(enum):
                        if enum.simple_name not in written_enums:
                            enum_out = ""
                            if enum.namespace:
                                enum_out = enum_out + f"{enum.namespace}::"
                            enum_out = enum_out + f"{enum.simple_name} {enum.comment}\n"
                            found_enums.append(enum_out)
                            
                        if not enum.is_ignored:
                            enum.write_to_file(self.src_path, structs)
                            if self.use_database:
                                self.db_handler.store_enum(enum)
                        
                    
                    written_enums.append(enum.simple_name)
                sorted_enums = sorted(found_enums)
                summary_file.write("".join(sorted_enums))

    def write_structs(self, structs: Dict[str, Struct]):
        """Write struct/class definitions to files and database.

        Categorizes structs into regular, generic (template), and ignored groups,
        writing summary files for each. Non-ignored, non-generic structs are
        written to individual source files. Vtable structs are detected and
        associated with their parent struct in the database.

        Args:
            structs: Dictionary mapping struct names to Struct objects.
        """
        categories = {
            'structs.txt': lambda s: not s.is_generic and not s.is_ignored,
            'structs_generic.txt': lambda s: s.is_generic and not s.is_ignored,
            'structs_ignored.txt': lambda s: s.is_ignored
        }
        
 
        for filename, filter_func in categories.items():
            written_structs = []
            found_structs = []
            with open(self.output_path / filename, 'w') as summary_file:
                for name in sorted(structs.keys()):
                    struct = structs[name]
                    if not struct.comment:
                        continue
                    if filter_func(struct):
                        if struct.simple_name not in written_structs:
                            struct_out = ""
                            if struct.namespace:
                                struct_out = struct_out + f"{struct.namespace}::"
                            struct_out = struct_out + f"{struct.simple_name} {struct.comment}\n"
                            found_structs.append(struct_out)
                            
                        if not struct.is_ignored:
                            if not struct.is_generic:
                                struct.write_to_file(self.src_path, structs)

                            # Store to database with vtable handling if enabled
                            if self.use_database and 'vtbl' not in name:
                                # Check if there's a corresponding vtable for this struct
                                vtable_code = None
                                # Look for vtable with the same name pattern
                                for other_name, other_struct in structs.items():
                                    if other_name == f"{name}_vtbl" or other_name == f"{name}Vtbl":
                                        vtable_code = other_struct.definition
                                        break
                                
                                # Store struct in database with vtable code if available
                                self.db_handler.store_struct(struct, vtable_code)

                    written_structs.append(struct.simple_name)
                sorted_structs = sorted(found_structs)
                summary_file.write("".join(sorted_structs))