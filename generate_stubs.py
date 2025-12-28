#!/usr/bin/env python3
"""
Generate stub headers for missing dependencies.

This script analyzes generated C++ source files and creates minimal stub
headers for any types that don't have headers yet. This enables clangd
to provide navigation and basic intellisense even for incomplete codebases.

Usage:
    python generate_stubs.py [--output-dir PATH] [--source-dir PATH]
    python generate_stubs.py --analyze-only  # Just show what's missing
"""

import argparse
import re
import sys
from pathlib import Path
from typing import Set, Dict, List, Tuple

# Known primitive/built-in types to ignore
BUILTIN_TYPES = {
    # C++ primitives
    'void', 'bool', 'char', 'short', 'int', 'long', 'float', 'double',
    'unsigned', 'signed', 'const', 'volatile', 'static', 'inline', 'virtual',
    # Fixed-width types
    'int8_t', 'int16_t', 'int32_t', 'int64_t',
    'uint8_t', 'uint16_t', 'uint32_t', 'uint64_t',
    'size_t', 'ptrdiff_t', 'intptr_t', 'uintptr_t',
    # Windows types
    'BOOL', 'BYTE', 'WORD', 'DWORD', 'LONG', 'ULONG', 'HANDLE',
    'HRESULT', 'LPVOID', 'LPCSTR', 'LPWSTR', 'WCHAR', 'HWND', 'HINSTANCE',
    'UINT', 'INT', 'LPARAM', 'WPARAM', 'LRESULT', 'CALLBACK', 'WINAPI',
    'PVOID', 'LPSTR', 'LPCWSTR', 'TCHAR', 'CHAR', 'WCHAR', 'SHORT', 'USHORT',
    # Windows API functions (often appear capitalized)
    'InterlockedIncrement', 'InterlockedDecrement', 'InterlockedExchange',
    'InterlockedCompareExchange', 'InterlockedAdd', 'GetLastError', 'SetLastError',
    # Common decompiler types
    '_BYTE', '_WORD', '_DWORD', '_QWORD', '__int64', '__int32', '__int16', '__int8',
}

# Common English words that might appear capitalized in comments
COMMENT_WORDS = {
    'The', 'This', 'That', 'These', 'Those', 'If', 'When', 'Where', 'While',
    'For', 'Each', 'All', 'Any', 'Some', 'No', 'Not', 'And', 'Or', 'But',
    'Returns', 'Return', 'Initializes', 'Initialize', 'Creates', 'Create',
    'Constructs', 'Constructor', 'Destructor', 'Handles', 'Handle',
    'Checks', 'Check', 'Sets', 'Set', 'Gets', 'Get', 'Adds', 'Add',
    'Removes', 'Remove', 'Updates', 'Update', 'Saves', 'Save', 'Loads', 'Load',
    'Reads', 'Read', 'Writes', 'Write', 'Copies', 'Copy', 'Clears', 'Clear',
    'Restores', 'Restore', 'Refreshes', 'Refresh', 'Validates', 'Validate',
    'Allocates', 'Allocate', 'Releases', 'Release', 'Frees', 'Free',
    'Registers', 'Register', 'Unregisters', 'Unregister',
    'Serializes', 'Serialize', 'Deserializes', 'Deserialize',
    'Increments', 'Increment', 'Decrements', 'Decrement',
    'Called', 'Calls', 'Call', 'Invokes', 'Invoke',
    'Note', 'Notes', 'See', 'Also', 'TODO', 'FIXME', 'XXX', 'HACK',
    'Otherwise', 'However', 'Therefore', 'Because', 'Since', 'Before', 'After',
    'Original', 'Modified', 'Changed', 'Fixed', 'Added', 'Removed',
    'Static', 'Virtual', 'Override', 'Final', 'Default', 'Deleted',
    'Public', 'Private', 'Protected', 'Friend', 'Class', 'Struct', 'Enum',
    'Memory', 'Layout', 'Offset', 'Index', 'Pointer', 'Reference',
    'Unique', 'Shared', 'Weak', 'Raw', 'Smart', 'Auto',
    'SKIP', 'VERIFICATION', 'FAILED', 'SUCCESS', 'ERROR', 'WARNING',
    'IMPORTANT', 'CRITICAL', 'DEBUG', 'INFO', 'TRACE',
    'Pre', 'Post', 'Init', 'Cleanup', 'Setup', 'Teardown',
    'Scans', 'Scan', 'Cleans', 'Clean', 'Grows', 'Grow', 'Shrinks', 'Shrink',
    'Accesses', 'Access', 'Retrieves', 'Retrieve', 'Stores', 'Store',
    'Captures', 'Capture', 'Requires', 'Require', 'Determines', 'Determine',
    'Delegates', 'Delegate', 'Appends', 'Append', 'Prepends', 'Prepend',
    'Associates', 'Associate', 'Disassociates', 'Disassociate',
    'Factory', 'Singleton', 'Builder', 'Visitor', 'Observer', 'Strategy',
    'Base', 'Derived', 'Parent', 'Child', 'Root', 'Leaf', 'Node',
    'Invalid', 'Valid', 'Empty', 'Full', 'Null', 'None', 'True', 'False',
    'MSVC', 'GCC', 'Clang', 'EAX', 'EBX', 'ECX', 'EDX', 'RTTI',
    'MI', 'SI', 'DI', 'Vtable', 'FROM', 'TO', 'AT', 'BY', 'IN', 'ON',
    'Certain', 'Radius', 'Event', 'Send', 'Receive', 'Pack', 'UnPack',
    'Decompiler', 'Compiler', 'Linker', 'Loader', 'Runtime',
}

# Method name patterns to exclude (verbs that indicate function names, not types)
METHOD_NAME_PATTERNS = {
    'Get', 'Set', 'Is', 'Has', 'Can', 'Do', 'Make', 'Find', 'Add', 'Remove',
    'Create', 'Delete', 'Update', 'Insert', 'Apply', 'Save', 'Load', 'Init',
    'Start', 'Stop', 'Begin', 'End', 'Open', 'Close', 'Read', 'Write',
    'Send', 'Recv', 'Listen', 'Handle', 'Process', 'Parse', 'Build',
    'Register', 'Unregister', 'Subscribe', 'Unsubscribe', 'Notify',
    'Lock', 'Unlock', 'Acquire', 'Release', 'Alloc', 'Free', 'New',
    'Validate', 'Check', 'Verify', 'Test', 'Assert', 'Ensure',
    'Convert', 'Transform', 'Translate', 'Format', 'Encode', 'Decode',
    'Serialize', 'Deserialize', 'Pack', 'Unpack', 'Compress', 'Decompress',
    'RecvNotice', 'SendNotice', 'OnEvent', 'HandleEvent', 'Restore', 'Use',
}

# Known method names that look like types (verb + type suffix pattern)
KNOWN_METHODS = {
    'SetStringInfo', 'GetStringInfo', 'GetIChatClient', 'SetIChatClient',
    'RestoreDefaultValue', 'RestoreSavedValue', 'SaveToServer', 'LoadFromServer',
    'SaveCurrentValue', 'UseTime', 'GetTime', 'SetTime',
}

# Patterns that indicate NOT a type (COM methods, callbacks, etc.)
SKIP_PATTERNS = [
    'RecvNotice_',      # Notification methods
    'OnEvent_',         # Event handlers
    'IUnknown_',        # COM interface methods
    'You_Must_',        # Static assertion messages
    'Available',        # Generic word
    'QueryInterface',   # COM method
    'DynamicCast',      # Method name
    'OnChanged',        # Callback
    'OnInitialize',     # Callback
    'IncludeFile',      # Method name
]

# Regex patterns for finding type references
INCLUDE_PATTERN = re.compile(r'#include\s*[<"]([^>"]+)[>"]')
CLASS_INHERIT_PATTERN = re.compile(r'class\s+\w+\s*:\s*(?:public|private|protected)?\s*([\w:,\s]+)\s*\{')
TYPE_REF_PATTERN = re.compile(r'\b([A-Z][A-Za-z0-9_]*(?:<[^>]+>)?)\b')
TEMPLATE_PATTERN = re.compile(r'(\w+)<([^>]+)>')


def extract_includes(content: str) -> Set[str]:
    """Extract all #include paths from source content."""
    includes = set()
    for match in INCLUDE_PATTERN.finditer(content):
        includes.add(match.group(1))
    return includes


def strip_comments(content: str) -> str:
    """Remove C and C++ style comments from source content."""
    # Remove single-line comments
    content = re.sub(r'//.*$', '', content, flags=re.MULTILINE)
    # Remove multi-line comments
    content = re.sub(r'/\*.*?\*/', '', content, flags=re.DOTALL)
    return content


def extract_type_references(content: str) -> Set[str]:
    """Extract all type references from source content (excluding comments)."""
    types = set()

    # Strip comments first to avoid picking up words from documentation
    code_only = strip_comments(content)

    for match in TYPE_REF_PATTERN.finditer(code_only):
        type_name = match.group(1)

        # Skip builtins
        if type_name in BUILTIN_TYPES:
            continue

        # Skip known method names that look like types
        if type_name in KNOWN_METHODS:
            continue

        # Skip common English words from comments that might leak through
        if type_name in COMMENT_WORDS:
            continue

        # Skip common keywords that look like types
        if type_name in {'True', 'False', 'NULL', 'nullptr', 'this', 'return', 'if', 'else', 'for', 'while'}:
            continue

        # Skip if it looks like a method name (starts with common verb patterns)
        if any(type_name.startswith(prefix) for prefix in METHOD_NAME_PATTERNS):
            # But allow if it's clearly a type (e.g., ends with common type suffixes)
            if not any(type_name.endswith(suffix) for suffix in ['Info', 'Data', 'Desc', 'Profile', 'Table', 'List', 'Map', 'Set', 'Array', 'Vector', 'Queue', 'Stack', 'Buffer', 'Cache', 'Manager', 'Handler', 'Factory', 'Builder', 'Helper', 'Client', 'Server', 'Module', 'System', 'Service', 'Controller', 'Provider', 'Registry', 'Repository']):
                continue

        # Skip enum value patterns (e.g., AcceptLootPermits_PlayerOption)
        # These are enum members with format: EnumValue_EnumTypeName
        if '_' in type_name and type_name.split('_')[-1] in {'PlayerOption', 'Option', 'Type', 'Flag', 'Mode', 'State'}:
            continue

        # Skip ALL_CAPS names (likely macros/constants, not types)
        if type_name.isupper() or (type_name.replace('_', '').isupper() and '_' in type_name):
            continue

        # Handle templates - extract base type and template args
        template_match = TEMPLATE_PATTERN.match(type_name)
        if template_match:
            base_type = template_match.group(1)
            template_args = template_match.group(2)
            if base_type not in COMMENT_WORDS:
                types.add(base_type)
            # Also extract types from template arguments
            for arg in template_args.split(','):
                arg = arg.strip().split()[-1]  # Get the type part
                if arg and arg[0].isupper() and arg not in BUILTIN_TYPES and arg not in COMMENT_WORDS:
                    types.add(arg)
        else:
            types.add(type_name)

    return types


def find_existing_headers(include_dir: Path) -> Dict[str, Path]:
    """Find all existing header files and map type names to paths."""
    headers = {}
    if not include_dir.exists():
        return headers

    for header_path in include_dir.rglob('*.h'):
        # Map both the filename and the relative path
        type_name = header_path.stem
        headers[type_name] = header_path

        # Also map with namespace path
        rel_path = header_path.relative_to(include_dir)
        headers[str(rel_path)] = header_path

    return headers


def analyze_missing_types(source_dir: Path, include_dir: Path) -> Tuple[Set[str], Dict[str, Set[str]]]:
    """
    Analyze source files to find missing type headers.

    Returns:
        Tuple of (all_missing_types, per_file_missing_types)
    """
    existing_headers = find_existing_headers(include_dir)
    existing_types = set(existing_headers.keys())

    # Also track existing header files by full name
    existing_header_files = {h.name for h in include_dir.rglob('*.h') if '_stubs' not in str(h)}

    all_missing = set()
    per_file = {}

    # Scan both .cpp and .h files
    source_files = list(source_dir.rglob('*.cpp')) + list(include_dir.rglob('*.h'))
    # Exclude stubs directory
    source_files = [f for f in source_files if '_stubs' not in str(f)]

    for source_file in source_files:
        content = source_file.read_text(encoding='utf-8', errors='replace')

        # Get includes and type references
        includes = extract_includes(content)
        type_refs = extract_type_references(content)

        # Find included headers that don't exist (most reliable signal)
        missing_includes = set()
        for inc in includes:
            inc_name = Path(inc).name
            inc_stem = Path(inc).stem
            # Skip system headers (with angle brackets pattern or common system paths)
            if '/' in inc or inc.startswith('std') or inc in {'windows.h', 'cstdint', 'cstddef'}:
                continue
            # Check if header exists
            if inc_name not in existing_header_files and inc_stem not in existing_types:
                missing_includes.add(inc_stem)

        # Find types that are referenced but don't have headers
        included_types = {Path(inc).stem for inc in includes}
        missing = type_refs - existing_types - included_types - BUILTIN_TYPES

        # Combine both sources of missing types
        all_missing_for_file = missing | missing_includes

        # Filter out known non-types
        all_missing_for_file = {t for t in all_missing_for_file
                                if not any(t.startswith(p) or t == p.rstrip('_') for p in SKIP_PATTERNS)}

        if all_missing_for_file:
            per_file[str(source_file)] = all_missing_for_file
            all_missing.update(all_missing_for_file)

    return all_missing, per_file


def parse_clangd_output(content: str) -> Tuple[Set[str], Set[str]]:
    """
    Parse clangd diagnostics output to find missing types/headers.

    Handles formats from clangd --check output.

    Returns:
        Tuple of (missing_types, template_types)
    """
    missing = set()
    templates = set()

    # Match "unknown type name 'X'" (case-insensitive)
    for match in re.finditer(r"unknown type name '(\w+)'", content, re.IGNORECASE):
        missing.add(match.group(1))

    # Match "'X.h' file not found" (from [pp_file_not_found] errors)
    for match in re.finditer(r"'(\w+)\.h' file not found", content, re.IGNORECASE):
        missing.add(match.group(1))

    # Match IncludeCleaner format: include "X.h" : no such file or directory
    for match in re.finditer(r'include "(\w+)\.h"[^:]*: no such file', content, re.IGNORECASE):
        missing.add(match.group(1))

    # Match "use of undeclared identifier 'X'" - often indicates missing namespace or type
    for match in re.finditer(r"use of undeclared identifier '(\w+)'", content, re.IGNORECASE):
        type_name = match.group(1)
        # Only add if it looks like a type/namespace (PascalCase or starts with _ prefix)
        if type_name[0].isupper() or type_name.startswith('_'):
            missing.add(type_name)

    # Match "no type named 'X'" (another variant)
    for match in re.finditer(r"no type named '(\w+)'", content, re.IGNORECASE):
        missing.add(match.group(1))

    # Match "no template named 'X'" - these are definitely templates
    for match in re.finditer(r"no template named '(\w+)'", content, re.IGNORECASE):
        type_name = match.group(1)
        missing.add(type_name)
        templates.add(type_name)

    # Match "incomplete type 'X'"
    for match in re.finditer(r"incomplete type '(\w+)'", content, re.IGNORECASE):
        missing.add(match.group(1))

    return missing, templates


def find_clangd() -> str | None:
    """Find clangd executable, checking common installation locations."""
    import shutil
    import os

    # First check PATH
    clangd_path = shutil.which('clangd')
    if clangd_path:
        return clangd_path

    # Common system installation paths
    system_paths = [
        '/usr/bin/clangd',
        '/usr/local/bin/clangd',
        '/opt/homebrew/bin/clangd',  # macOS Homebrew
        'C:/Program Files/LLVM/bin/clangd.exe',
        'C:/Program Files (x86)/LLVM/bin/clangd.exe',
        'C:/msys64/mingw64/bin/clangd.exe',
        'C:/msys64/clang64/bin/clangd.exe',
    ]

    # Check IDE extension directories (VSCode, Cursor, etc.)
    home = Path.home()
    ide_extension_patterns = [
        # VSCode on Windows
        home / 'AppData/Roaming/Code/User/globalStorage/llvm-vs-code-extensions.vscode-clangd/install',
        # Cursor on Windows
        home / 'AppData/Roaming/Cursor/User/globalStorage/llvm-vs-code-extensions.vscode-clangd/install',
        # VSCode on macOS
        home / 'Library/Application Support/Code/User/globalStorage/llvm-vs-code-extensions.vscode-clangd/install',
        # VSCode on Linux
        home / '.config/Code/User/globalStorage/llvm-vs-code-extensions.vscode-clangd/install',
        # VSCode Insiders
        home / 'AppData/Roaming/Code - Insiders/User/globalStorage/llvm-vs-code-extensions.vscode-clangd/install',
        home / '.config/Code - Insiders/User/globalStorage/llvm-vs-code-extensions.vscode-clangd/install',
    ]

    # Search IDE extension directories for clangd
    for ext_dir in ide_extension_patterns:
        if ext_dir.exists():
            # Look for clangd in version subdirectories
            for version_dir in ext_dir.iterdir():
                if version_dir.is_dir():
                    for clangd_dir in version_dir.iterdir():
                        if clangd_dir.is_dir():
                            exe_name = 'clangd.exe' if os.name == 'nt' else 'clangd'
                            candidate = clangd_dir / 'bin' / exe_name
                            if candidate.exists():
                                return str(candidate)

    # Check system paths
    for path in system_paths:
        if Path(path).exists():
            return path

    return None


def run_clangd_check(source_dir: Path, include_dir: Path) -> Tuple[Set[str], Set[str]]:
    """
    Run clangd --check on all source files and collect missing types.

    Returns:
        Tuple of (missing_types, template_types)
    """
    import subprocess

    # Find clangd
    clangd_path = find_clangd()

    if not clangd_path:
        print("Warning: clangd not found in PATH. Install LLVM or add clangd to PATH.")
        print("Falling back to static analysis...")
        return set(), set()

    print(f"Using clangd: {clangd_path}")

    all_missing = set()
    all_templates = set()
    stubs_dir = include_dir / "_stubs"

    # Get all source files (use absolute paths)
    source_files = list(source_dir.resolve().rglob('*.cpp')) + list(include_dir.resolve().rglob('*.h'))
    source_files = [f for f in source_files if '_stubs' not in str(f)]

    # Get output directory (parent of src)
    output_dir = source_dir.resolve().parent

    print(f"Checking {len(source_files)} files with clangd...")

    for source_file in source_files:
        try:
            # Run clangd --check=<file> with absolute path
            result = subprocess.run(
                [clangd_path, f'--check={source_file.resolve()}'],
                capture_output=True,
                text=True,
                encoding='utf-8',
                errors='replace',
                timeout=60,  # Increase timeout for large files
                cwd=str(output_dir)  # Run from output directory
            )

            # clangd outputs diagnostics to stderr
            output = result.stderr + result.stdout
            missing, templates = parse_clangd_output(output)
            if missing:
                print(f"  {source_file.name}: found {len(missing)} types: {missing}")
            all_missing.update(missing)
            all_templates.update(templates)

        except subprocess.TimeoutExpired:
            print(f"  Timeout checking {source_file.name}")
        except Exception as e:
            print(f"  Error checking {source_file.name}: {e}")

    return all_missing, all_templates


def generate_stub_header(type_name: str, referenced_by: List[str], is_template: bool = False) -> str:
    """Generate a minimal stub header for a missing type."""

    # Check if it looks like an enum (often ends in specific patterns)
    is_likely_enum = any(type_name.endswith(suffix) for suffix in
                         ['_TYPE', '_MODE', '_STATE', '_FLAG', '_KIND', '_STATUS', 'Type', 'Mode', 'State', 'Option'])

    stub = f"""#pragma once
// AUTO-GENERATED STUB HEADER
// This is a minimal stub for clangd support.
// Replace with actual implementation when available.
//
// Referenced by:
"""
    for ref in referenced_by[:5]:  # Limit to 5 references
        stub += f"//   - {ref}\n"
    if len(referenced_by) > 5:
        stub += f"//   ... and {len(referenced_by) - 5} more\n"

    stub += "\n"

    if is_likely_enum:
        stub += f"""enum class {type_name} {{
    UNKNOWN = 0,
    // TODO: Add actual enum values
}};
"""
    elif is_template:
        stub += f"""template<typename T>
class {type_name} {{
public:
    // TODO: Add actual members and methods
    {type_name}() = default;
    virtual ~{type_name}() = default;

    // Common template container methods
    T* data() {{ return nullptr; }}
    const T* data() const {{ return nullptr; }}
    T& operator[](size_t) {{ static T t; return t; }}
    const T& operator[](size_t) const {{ static T t; return t; }}
}};
"""
    else:
        stub += f"""class {type_name} {{
public:
    // TODO: Add actual members and methods
    {type_name}() = default;
    virtual ~{type_name}() = default;
}};
"""

    return stub


def generate_stubs(source_dir: Path, include_dir: Path, dry_run: bool = False) -> int:
    """
    Generate stub headers for all missing types.

    Returns:
        Number of stubs generated
    """
    print(f"Analyzing sources in: {source_dir}")
    print(f"Include directory: {include_dir}")
    print()

    all_missing, per_file = analyze_missing_types(source_dir, include_dir)

    if not all_missing:
        print("No missing type headers found!")
        return 0

    print(f"Found {len(all_missing)} missing types:\n")

    # Build reverse mapping: type -> files that reference it
    type_to_files: Dict[str, List[str]] = {}
    for file_path, types in per_file.items():
        for t in types:
            if t not in type_to_files:
                type_to_files[t] = []
            type_to_files[t].append(Path(file_path).name)

    # Sort by most referenced
    sorted_types = sorted(all_missing, key=lambda t: len(type_to_files.get(t, [])), reverse=True)

    stubs_dir = include_dir / "_stubs"

    if dry_run:
        print("DRY RUN - would generate these stubs:\n")
        for type_name in sorted_types:
            refs = type_to_files.get(type_name, [])
            print(f"  {type_name}.h (referenced by {len(refs)} files)")
        return len(sorted_types)

    # Create stubs directory
    stubs_dir.mkdir(parents=True, exist_ok=True)

    # Generate stubs
    count = 0
    for type_name in sorted_types:
        refs = type_to_files.get(type_name, [])
        stub_content = generate_stub_header(type_name, refs)

        stub_path = stubs_dir / f"{type_name}.h"
        stub_path.write_text(stub_content, encoding='utf-8')
        print(f"  Created: {stub_path.relative_to(include_dir.parent)}")
        count += 1

    print(f"\nGenerated {count} stub headers in {stubs_dir}")
    print(f"\nTo use with clangd, add to your .clangd or compile_commands.json:")
    print(f"  -I{stubs_dir}")

    return count


def generate_all_stubs_header(stubs_dir: Path) -> Path:
    """
    Generate an all_stubs.h header that includes all stub headers.
    This can be force-included via -include flag to automatically provide all stubs.
    """
    all_stubs_path = stubs_dir / "all_stubs.h"

    # Get all stub headers (excluding all_stubs.h itself)
    stub_headers = sorted([h for h in stubs_dir.glob('*.h')
                          if h.name not in ('all_stubs.h', 'windows_types.h')])

    content = """#pragma once
// AUTO-GENERATED MASTER INCLUDE FOR ALL STUBS
// This file is force-included via compile_commands.json to provide
// stub definitions for all missing types.
//
// DO NOT EDIT - regenerated by generate_stubs.py

// Windows SDK types must be included first (other stubs may depend on them)
#include "windows_types.h"

"""

    for header in stub_headers:
        content += f'#include "{header.name}"\n'

    all_stubs_path.write_text(content, encoding='utf-8')
    return all_stubs_path


def update_compile_commands(output_dir: Path, stubs_dir: Path) -> bool:
    """
    Update compile_commands.json to force-include all_stubs.h.
    Returns True if updated successfully.
    """
    import json

    compile_commands_path = output_dir / "compile_commands.json"
    if not compile_commands_path.exists():
        print(f"Warning: {compile_commands_path} not found")
        return False

    all_stubs_h = stubs_dir / "all_stubs.h"
    force_include_arg = f"-include"
    force_include_path = str(all_stubs_h.resolve())

    try:
        with open(compile_commands_path, 'r', encoding='utf-8') as f:
            commands = json.load(f)

        modified = False
        for entry in commands:
            args = entry.get('arguments', [])

            # Check if force-include is already present
            has_force_include = False
            for i, arg in enumerate(args):
                if arg == '-include' and i + 1 < len(args) and 'all_stubs.h' in args[i + 1]:
                    has_force_include = True
                    # Update path in case it changed
                    args[i + 1] = force_include_path
                    break

            if not has_force_include:
                # Find index of -c flag and insert before it
                try:
                    c_index = args.index('-c')
                    args.insert(c_index, force_include_path)
                    args.insert(c_index, '-include')
                    modified = True
                except ValueError:
                    # No -c flag, insert before filename (last element)
                    args.insert(-1, '-include')
                    args.insert(-1, force_include_path)
                    modified = True

        if modified:
            with open(compile_commands_path, 'w', encoding='utf-8') as f:
                json.dump(commands, f, indent=2)
            print(f"Updated {compile_commands_path} with -include {all_stubs_h.name}")
        else:
            print(f"compile_commands.json already has force-include for all_stubs.h")

        return True

    except Exception as e:
        print(f"Error updating compile_commands.json: {e}")
        return False


def main():
    parser = argparse.ArgumentParser(
        description="Generate stub headers for missing dependencies"
    )
    parser.add_argument(
        '--source-dir', '-s',
        type=Path,
        default=Path('output/src'),
        help='Directory containing generated .cpp files (default: output/src)'
    )
    parser.add_argument(
        '--output-dir', '-o',
        type=Path,
        default=Path('output/include'),
        help='Directory for header files (default: output/include)'
    )
    parser.add_argument(
        '--analyze-only', '-a',
        action='store_true',
        help='Only analyze and report missing types, do not generate stubs'
    )
    parser.add_argument(
        '--use-clangd',
        action='store_true',
        help='Run clangd --check to find missing types (more accurate but slower)'
    )
    parser.add_argument(
        '--from-file', '-f',
        type=Path,
        help='Parse clangd diagnostics from file instead of running clangd'
    )

    args = parser.parse_args()

    # Types provided by windows_types.h stub (no need to create individual stubs)
    windows_stub_types = {
        'HRESULT', 'HWND', 'HINSTANCE', 'HMODULE', 'HANDLE',
        'BOOL', 'DWORD', 'WORD', 'BYTE', 'LONG', 'ULONG', 'UINT', 'INT',
        'SHORT', 'USHORT', 'LPARAM', 'WPARAM', 'LRESULT',
        'LPVOID', 'PVOID', 'LPCSTR', 'LPSTR', 'LPCWSTR', 'LPWSTR',
        'WCHAR', 'CHAR', 'TCHAR',
    }

    # If using clangd (either running it or from file)
    if args.use_clangd or args.from_file:
        stubs_dir = args.output_dir / "_stubs"
        stubs_dir.mkdir(parents=True, exist_ok=True)

        total_count = 0
        iteration = 0
        max_iterations = 10  # Safety limit

        while iteration < max_iterations:
            iteration += 1
            print(f"\n{'='*60}")
            print(f"Pass {iteration}")
            print(f"{'='*60}")

            if args.from_file:
                if not args.from_file.exists():
                    print(f"Error: Diagnostics file not found: {args.from_file}")
                    return 1
                content = args.from_file.read_text(encoding='utf-8', errors='replace')
                missing_types, template_types = parse_clangd_output(content)
                # File-based mode only runs once
                max_iterations = 1
            else:
                missing_types, template_types = run_clangd_check(args.source_dir, args.output_dir)

            # Filter out Windows types
            missing_types -= windows_stub_types

            if not missing_types:
                print("\nNo missing types found!")
                break

            print(f"\nFound {len(missing_types)} missing types:")
            if template_types - windows_stub_types:
                print(f"  (including {len(template_types - windows_stub_types)} template types)")

            if args.analyze_only:
                print("\nDRY RUN - would generate these stubs:\n")
                for type_name in sorted(missing_types):
                    template_marker = " [template]" if type_name in template_types else ""
                    print(f"  {type_name}.h{template_marker}")
                return len(missing_types)

            count = 0
            for type_name in sorted(missing_types):
                stub_path = stubs_dir / f"{type_name}.h"
                is_template = type_name in template_types
                if not stub_path.exists():  # Don't overwrite existing stubs
                    stub_content = generate_stub_header(type_name, ["clangd"], is_template=is_template)
                    stub_path.write_text(stub_content, encoding='utf-8')
                    template_marker = " [template]" if is_template else ""
                    print(f"  Created: {stub_path}{template_marker}")
                    count += 1
                else:
                    print(f"  Skipped (exists): {stub_path}")

            total_count += count
            print(f"\nGenerated {count} new stub headers this pass")

            # Generate all_stubs.h after each pass
            all_stubs_path = generate_all_stubs_header(stubs_dir)
            print(f"Updated master include: {all_stubs_path}")

            # If no new stubs were created, we're stable
            if count == 0:
                print("\nStable state reached - no new stubs needed.")
                break

        # Update compile_commands.json once at the end
        if not args.analyze_only:
            output_dir = args.source_dir.resolve().parent
            update_compile_commands(output_dir, stubs_dir)

        print(f"\n{'='*60}")
        print(f"Total: Generated {total_count} stub headers across {iteration} passes")
        print(f"{'='*60}")

        return total_count

    if not args.source_dir.exists():
        print(f"Error: Source directory not found: {args.source_dir}")
        return 1

    count = generate_stubs(
        source_dir=args.source_dir,
        include_dir=args.output_dir,
        dry_run=args.analyze_only
    )

    # Generate all_stubs.h and update compile_commands.json
    if count > 0 and not args.analyze_only:
        stubs_dir = args.output_dir / "_stubs"
        all_stubs_path = generate_all_stubs_header(stubs_dir)
        print(f"Generated master include: {all_stubs_path}")

        output_dir = args.source_dir.resolve().parent
        update_compile_commands(output_dir, stubs_dir)

    return 0 if count >= 0 else 1


if __name__ == '__main__':
    sys.exit(main())
