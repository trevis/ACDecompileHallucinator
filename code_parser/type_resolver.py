"""
TypeResolver
============
Centralized type resolution system that provides programmatic lookup
of types, enums, constants, and their definitions from the database.

This replaces LLM-based type inference with deterministic database lookups,
enabling faster processing and more accurate type resolution.
"""

import logging
import re
import sqlite3
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Set, Tuple

from .db_handler import DatabaseHandler

logger = logging.getLogger(__name__)


# Primitive types that should not be resolved from database
PRIMITIVE_TYPES: Set[str] = {
    # C++ primitives
    'void', 'bool', 'char', 'short', 'int', 'long', 'float', 'double',
    'signed', 'unsigned', 'wchar_t', 'char16_t', 'char32_t',
    # Fixed-width types
    'int8_t', 'int16_t', 'int32_t', 'int64_t',
    'uint8_t', 'uint16_t', 'uint32_t', 'uint64_t',
    'size_t', 'ssize_t', 'ptrdiff_t', 'intptr_t', 'uintptr_t',
    # Windows types
    'BOOL', 'BYTE', 'WORD', 'DWORD', 'QWORD',
    'CHAR', 'WCHAR', 'TCHAR',
    'SHORT', 'USHORT', 'INT', 'UINT', 'LONG', 'ULONG',
    'LONGLONG', 'ULONGLONG',
    'HANDLE', 'HWND', 'HINSTANCE', 'HMODULE',
    'LPVOID', 'LPCVOID', 'LPSTR', 'LPCSTR', 'LPWSTR', 'LPCWSTR',
    'FLOAT', 'DOUBLE',
    # STL common
    'string', 'wstring', 'vector', 'list', 'map', 'set', 'pair',
    'unique_ptr', 'shared_ptr', 'weak_ptr',
}


@dataclass
class TypeInfo:
    """Represents a resolved type with its metadata."""
    name: str                           # Full qualified name
    simple_name: str                    # Name without namespace
    namespace: Optional[str]            # Namespace if any
    kind: str                           # 'enum', 'struct', 'typedef', 'primitive'
    definition: str                     # Code definition (raw or processed)
    is_processed: bool                  # True if from processed_types table
    is_template: bool                   # True if template instantiation
    parent: Optional[str]               # Base class for structs
    file_path: str                      # Expected header path
    dependencies: List[str] = field(default_factory=list)


@dataclass
class EnumValueInfo:
    """Represents an enum value from parsed definition."""
    name: str
    value: Optional[int]    # None if not explicit or expression
    enum_type: str          # Parent enum name


@dataclass
class ConstantInfo:
    """Represents a constant from acclient.txt."""
    name: str
    value: str
    type_id: Optional[str]
    is_ldata: bool
    address: Optional[str]


class TypeResolver:
    """
    Centralized type resolution system.

    Responsibilities:
    - Load and cache all type information from database
    - Resolve type names to their definitions (processed or raw)
    - Extract enum values for switch statement modernization
    - Provide constant/macro mappings
    - Handle namespace-qualified lookups
    - Map decompiler types to modern C++ equivalents
    """

    # Decompiler type mappings to modern C++ equivalents
    DECOMPILER_TYPE_MAP: Dict[str, str] = {
        'BOOL': 'bool',
        '_BOOL4': 'bool',
        '_BOOL1': 'bool',
        '__int64': 'int64_t',
        '__int32': 'int32_t',
        '__int16': 'int16_t',
        '__int8': 'int8_t',
        'undefined4': 'uint32_t',
        'undefined2': 'uint16_t',
        'undefined1': 'uint8_t',
        'undefined': 'uint8_t',
        'longlong': 'int64_t',
        'ulonglong': 'uint64_t',
    }

    def __init__(self, db_handler: DatabaseHandler):
        """
        Initialize the type resolver.

        Args:
            db_handler: Database handler instance for querying types
        """
        self.db = db_handler

        # Type caches
        self._type_cache: Dict[str, TypeInfo] = {}
        self._type_by_simple_name: Dict[str, List[str]] = {}  # simple_name -> [full_names]

        # Enum value caches
        self._enum_values: Dict[str, List[EnumValueInfo]] = {}  # enum_name -> values
        self._value_to_enums: Dict[int, List[EnumValueInfo]] = {}  # value -> enum_values

        # Constant cache
        self._constants: Dict[str, ConstantInfo] = {}

        # Load data
        self._load_types()
        self._load_constants()
        self._load_enum_values()

        logger.info(
            f"TypeResolver initialized: {len(self._type_cache)} types, "
            f"{sum(len(v) for v in self._enum_values.values())} enum values, "
            f"{len(self._constants)} constants"
        )

    def _load_types(self) -> None:
        """Load all types from database into cache."""
        # Load raw types
        for row in self.db.get_all_types():
            type_info = self._row_to_type_info(row, is_processed=False)
            self._add_to_cache(type_info)

        # Load processed types (overwrite raw if exists)
        with sqlite3.connect(self.db.db_path) as conn:
            cursor = conn.cursor()
            cursor.execute('''
                SELECT name, type, processed_header, engine_used
                FROM processed_types
            ''')
            for row in cursor.fetchall():
                name, kind, header, engine = row
                existing = self._type_cache.get(name)
                if existing:
                    # Update with processed info
                    existing.definition = header
                    existing.is_processed = True
                else:
                    # Create new entry
                    namespace, simple = self.split_namespace(name)
                    type_info = TypeInfo(
                        name=name,
                        simple_name=simple,
                        namespace=namespace,
                        kind=kind or 'struct',
                        definition=header,
                        is_processed=True,
                        is_template=self.is_template_instantiation(name),
                        parent=None,
                        file_path=self._compute_file_path(name, namespace)
                    )
                    self._add_to_cache(type_info)

    def _row_to_type_info(self, row: Tuple, is_processed: bool = False) -> TypeInfo:
        """Convert database row to TypeInfo."""
        # Row format: (id, type, name, namespace, parent, code, ...)
        _, kind, name, namespace, parent, code = row[:6]

        simple_name = name.split('::')[-1] if '::' in name else name

        return TypeInfo(
            name=name,
            simple_name=simple_name,
            namespace=namespace,
            kind=kind,
            definition=code or '',
            is_processed=is_processed,
            is_template=self.is_template_instantiation(name),
            parent=parent,
            file_path=self._compute_file_path(name, namespace)
        )

    def _compute_file_path(self, name: str, namespace: Optional[str]) -> str:
        """Compute expected header file path for a type."""
        simple = name.split('::')[-1] if '::' in name else name

        # Remove template parameters for file path
        if '<' in simple:
            simple = simple[:simple.index('<')]

        if namespace:
            return f"{namespace.replace('::', '/')}/{simple}.h"
        return f"{simple}.h"

    def _add_to_cache(self, type_info: TypeInfo) -> None:
        """Add type to cache structures."""
        self._type_cache[type_info.name] = type_info

        # Index by simple name
        if type_info.simple_name not in self._type_by_simple_name:
            self._type_by_simple_name[type_info.simple_name] = []
        if type_info.name not in self._type_by_simple_name[type_info.simple_name]:
            self._type_by_simple_name[type_info.simple_name].append(type_info.name)

    def _load_constants(self) -> None:
        """Load all constants from database into cache."""
        for row in self.db.get_constants():
            # Row format varies, handle both dict and tuple
            if isinstance(row, dict):
                name = row.get('name', '')
                value = row.get('value', '')
                type_id = row.get('type_id')
                is_ldata = row.get('is_ldata', False)
                address = row.get('address')
            else:
                name, value, type_id = row[:3]
                is_ldata = row[3] if len(row) > 3 else False
                address = row[4] if len(row) > 4 else None

            self._constants[name] = ConstantInfo(
                name=name,
                value=str(value),
                type_id=type_id,
                is_ldata=bool(is_ldata),
                address=address
            )

    def _load_enum_values(self) -> None:
        """Parse all enum definitions and extract values."""
        for name, type_info in self._type_cache.items():
            if type_info.kind == 'enum' and type_info.definition:
                values = self._parse_enum_definition(type_info.definition, name)
                if values:
                    self._enum_values[name] = values
                    # Index by value
                    for ev in values:
                        if ev.value is not None:
                            if ev.value not in self._value_to_enums:
                                self._value_to_enums[ev.value] = []
                            self._value_to_enums[ev.value].append(ev)

    def _parse_enum_definition(self, enum_code: str, enum_name: str) -> List[EnumValueInfo]:
        """
        Parse enum definition to extract name-value pairs.

        Handles:
        - Explicit values (NAME = 0x10)
        - Implicit values (sequential numbering)
        - Hex and decimal values
        """
        values = []
        current_value = 0

        # Extract body between { }
        body_match = re.search(r'\{(.+?)\}', enum_code, re.DOTALL)
        if not body_match:
            return values

        body = body_match.group(1)

        # Split by comma, handling multiline
        entries = re.split(r',\s*', body)

        for entry in entries:
            entry = entry.strip()
            if not entry or entry.startswith('//'):
                continue

            # Remove inline comments
            if '//' in entry:
                entry = entry[:entry.index('//')].strip()
            if '/*' in entry:
                entry = re.sub(r'/\*.*?\*/', '', entry).strip()

            if not entry:
                continue

            # Handle NAME = VALUE or just NAME
            if '=' in entry:
                parts = entry.split('=', 1)
                name = parts[0].strip()
                value_str = parts[1].strip()

                # Remove any trailing comments or extra chars
                value_str = value_str.rstrip(',; \t\n\r')

                # Parse value (hex, decimal, expression)
                try:
                    if value_str.startswith('0x') or value_str.startswith('0X'):
                        current_value = int(value_str, 16)
                    elif value_str.startswith('-'):
                        current_value = int(value_str)
                    elif value_str.isdigit():
                        current_value = int(value_str)
                    else:
                        # Expression - set to None
                        current_value = None
                except ValueError:
                    current_value = None
            else:
                name = entry.rstrip(',; \t\n\r')

            if name:
                values.append(EnumValueInfo(
                    name=name,
                    value=current_value,
                    enum_type=enum_name
                ))

                if current_value is not None:
                    current_value += 1

        return values

    # =========================================================================
    # Type Lookup Methods
    # =========================================================================

    def resolve_type(self, type_name: str) -> Optional[TypeInfo]:
        """
        Resolve a type name to its TypeInfo.

        Args:
            type_name: Full or partial type name

        Returns:
            TypeInfo if found, None otherwise
        """
        # Direct lookup first
        if type_name in self._type_cache:
            return self._type_cache[type_name]

        # Try simple name lookup
        simple = type_name.split('::')[-1] if '::' in type_name else type_name

        # Remove template parameters for lookup
        if '<' in simple:
            simple = simple[:simple.index('<')]

        matches = self._type_by_simple_name.get(simple, [])
        if len(matches) == 1:
            return self._type_cache[matches[0]]
        elif len(matches) > 1:
            # Multiple matches - try to find exact match with namespace
            if '::' in type_name:
                for full_name in matches:
                    if full_name.endswith(type_name):
                        return self._type_cache[full_name]
            # Return first match
            return self._type_cache[matches[0]]

        return None

    def get_type_definition(self, type_name: str) -> Optional[str]:
        """Get the code definition for a type."""
        info = self.resolve_type(type_name)
        return info.definition if info else None

    def is_type_known(self, type_name: str) -> bool:
        """Check if a type is in the database."""
        return self.resolve_type(type_name) is not None

    def get_all_types_of_kind(self, kind: str) -> List[TypeInfo]:
        """Get all types of a specific kind (enum, struct, etc.)."""
        return [t for t in self._type_cache.values() if t.kind == kind]

    # =========================================================================
    # Enum Operations
    # =========================================================================

    def get_enum_values(self, enum_name: str) -> List[EnumValueInfo]:
        """Get all values for an enum type."""
        # Direct lookup
        if enum_name in self._enum_values:
            return self._enum_values[enum_name]

        # Try resolving the name
        info = self.resolve_type(enum_name)
        if info and info.name in self._enum_values:
            return self._enum_values[info.name]

        return []

    def find_enum_by_value(self, value: int) -> List[EnumValueInfo]:
        """Find enum values that match a specific integer value."""
        return self._value_to_enums.get(value, [])

    def is_enum_value(self, identifier: str) -> bool:
        """Check if an identifier is a known enum value."""
        for values in self._enum_values.values():
            for ev in values:
                if ev.name == identifier:
                    return True
        return False

    # =========================================================================
    # Constant Operations
    # =========================================================================

    def get_constant(self, name: str) -> Optional[ConstantInfo]:
        """Get a constant by name."""
        return self._constants.get(name)

    def get_constant_by_value(self, value: str) -> List[ConstantInfo]:
        """Find constants that have a specific value."""
        return [c for c in self._constants.values() if c.value == value]

    def resolve_constant_value(self, name: str) -> Optional[str]:
        """Get the value of a constant by name."""
        const = self._constants.get(name)
        return const.value if const else None

    def get_all_constants(self) -> Dict[str, ConstantInfo]:
        """Get all constants."""
        return self._constants.copy()

    # =========================================================================
    # Namespace Handling
    # =========================================================================

    def resolve_qualified_name(
        self,
        simple_name: str,
        context_namespace: Optional[str] = None
    ) -> List[str]:
        """
        Resolve a simple name to all possible qualified names.

        Args:
            simple_name: Unqualified type name
            context_namespace: Optional namespace context for preference

        Returns:
            List of matching fully qualified names
        """
        matches = self._type_by_simple_name.get(simple_name, [])

        if context_namespace and len(matches) > 1:
            # Prefer types in the same namespace
            preferred = [m for m in matches if m.startswith(context_namespace + '::')]
            if preferred:
                return preferred

        return matches

    def split_namespace(self, full_name: str) -> Tuple[Optional[str], str]:
        """Split a fully qualified name into (namespace, simple_name)."""
        if '::' in full_name:
            parts = full_name.rsplit('::', 1)
            return parts[0], parts[1]
        return None, full_name

    def get_file_path(self, type_name: str) -> str:
        """Get expected header file path for a type."""
        info = self.resolve_type(type_name)
        if info:
            return info.file_path

        # Fallback computation
        namespace, simple = self.split_namespace(type_name)
        return self._compute_file_path(type_name, namespace)

    # =========================================================================
    # Type Classification
    # =========================================================================

    def is_primitive(self, type_name: str) -> bool:
        """Check if a type is a primitive type."""
        # Clean up type name
        clean = type_name.strip()
        clean = re.sub(r'\s*[*&]+\s*$', '', clean)  # Remove pointer/ref
        clean = re.sub(r'\s*const\s*', '', clean)   # Remove const

        return clean in PRIMITIVE_TYPES

    def is_template_instantiation(self, type_name: str) -> bool:
        """Check if a type name is a template instantiation."""
        return '<' in type_name and '>' in type_name

    def extract_template_parameters(self, type_name: str) -> List[str]:
        """Extract template parameters from a template instantiation."""
        match = re.search(r'<(.+)>', type_name)
        if not match:
            return []

        # Simple split by comma (doesn't handle nested templates perfectly)
        params_str = match.group(1)
        params = []
        depth = 0
        current = []

        for char in params_str:
            if char == '<':
                depth += 1
                current.append(char)
            elif char == '>':
                depth -= 1
                current.append(char)
            elif char == ',' and depth == 0:
                params.append(''.join(current).strip())
                current = []
            else:
                current.append(char)

        if current:
            params.append(''.join(current).strip())

        return params

    # =========================================================================
    # Decompiler Type Mapping
    # =========================================================================

    def map_decompiler_type(self, decompiler_type: str) -> str:
        """Map decompiler-specific types to modern C++ equivalents."""
        return self.DECOMPILER_TYPE_MAP.get(decompiler_type, decompiler_type)

    def normalize_type_name(self, type_name: str) -> str:
        """Normalize a type name by applying decompiler mappings."""
        # Apply direct mapping
        mapped = self.map_decompiler_type(type_name)
        if mapped != type_name:
            return mapped

        # Handle pointer/reference modifiers
        suffix = ''
        clean = type_name.strip()
        while clean.endswith('*') or clean.endswith('&'):
            suffix = clean[-1] + suffix
            clean = clean[:-1].strip()

        mapped = self.map_decompiler_type(clean)
        return mapped + suffix if suffix else mapped

    # =========================================================================
    # Cache Management
    # =========================================================================

    def refresh_processed_types(self) -> None:
        """Refresh cache with newly processed types from database."""
        with sqlite3.connect(self.db.db_path) as conn:
            cursor = conn.cursor()
            cursor.execute('''
                SELECT name, type, processed_header, engine_used
                FROM processed_types
            ''')
            for row in cursor.fetchall():
                name, kind, header, engine = row
                existing = self._type_cache.get(name)
                if existing:
                    existing.definition = header
                    existing.is_processed = True
                else:
                    namespace, simple = self.split_namespace(name)
                    type_info = TypeInfo(
                        name=name,
                        simple_name=simple,
                        namespace=namespace,
                        kind=kind or 'struct',
                        definition=header,
                        is_processed=True,
                        is_template=self.is_template_instantiation(name),
                        parent=None,
                        file_path=self._compute_file_path(name, namespace)
                    )
                    self._add_to_cache(type_info)

        logger.debug(f"Refreshed processed types. Total types: {len(self._type_cache)}")

    def get_stats(self) -> Dict[str, Any]:
        """Get statistics about cached types."""
        return {
            'total_types': len(self._type_cache),
            'enums': len([t for t in self._type_cache.values() if t.kind == 'enum']),
            'structs': len([t for t in self._type_cache.values() if t.kind == 'struct']),
            'processed': len([t for t in self._type_cache.values() if t.is_processed]),
            'enum_values': sum(len(v) for v in self._enum_values.values()),
            'constants': len(self._constants),
        }
