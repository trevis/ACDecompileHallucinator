"""
Code Preprocessor
================
Pre-processes decompiled C++ code before sending to LLM.

This module combines TypeResolver and ConstantReplacer to clean up
decompiled code, removing artifacts and annotating known values.

The goal is to reduce LLM hallucination by:
1. Replacing decompiler-specific types with modern C++ equivalents
2. Annotating magic numbers with potential enum values
3. Removing calling convention artifacts (__thiscall, __cdecl, etc.)
4. Cleaning up explicit this pointer usage annotations
"""

import logging
import re
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Set, Tuple, TYPE_CHECKING

if TYPE_CHECKING:
    from .type_resolver import TypeResolver, EnumValueInfo
    from .constant_replacer import ConstantReplacer
    from .db_handler import DatabaseHandler

logger = logging.getLogger(__name__)


@dataclass
class PreprocessingResult:
    """Result of code preprocessing.

    Contains the processed code along with metadata about what
    transformations were applied.
    """
    # The preprocessed code
    code: str

    # Original code for reference
    original_code: str

    # Transformations applied
    decompiler_types_replaced: Dict[str, str] = field(default_factory=dict)
    calling_conventions_removed: List[str] = field(default_factory=list)
    enum_annotations_added: Dict[int, str] = field(default_factory=dict)
    constants_annotated: int = 0

    # Extracted metadata
    numeric_literals: Set[int] = field(default_factory=set)
    type_references: Set[str] = field(default_factory=set)

    @property
    def was_modified(self) -> bool:
        """Check if any transformations were applied."""
        return (
            bool(self.decompiler_types_replaced)
            or bool(self.calling_conventions_removed)
            or bool(self.enum_annotations_added)
            or self.constants_annotated > 0
        )

    def get_summary(self) -> str:
        """Get a human-readable summary of transformations."""
        parts = []
        if self.decompiler_types_replaced:
            parts.append(f"{len(self.decompiler_types_replaced)} type replacements")
        if self.calling_conventions_removed:
            parts.append(f"{len(self.calling_conventions_removed)} calling conventions removed")
        if self.enum_annotations_added:
            parts.append(f"{len(self.enum_annotations_added)} enum annotations")
        if self.constants_annotated:
            parts.append(f"{self.constants_annotated} constants annotated")
        return ", ".join(parts) if parts else "no changes"


class CodePreprocessor:
    """Pre-processes decompiled C++ code before LLM processing.

    Combines TypeResolver mappings, ConstantReplacer annotations, and
    regex-based cleanup to produce cleaner code for the LLM.

    Key transformations:
    1. Replace decompiler types (BOOL -> bool, _BOOL4 -> bool, etc.)
    2. Remove calling conventions (__thiscall, __cdecl, __stdcall)
    3. Annotate magic numbers with potential enum values
    4. Annotate known constants with type/value information

    Usage:
        preprocessor = CodePreprocessor(db_handler)
        result = preprocessor.preprocess(raw_code)
        # result.code contains the cleaned code
        # result.enum_annotations_added shows what was annotated
    """

    # Calling conventions to remove
    CALLING_CONVENTIONS = [
        '__thiscall',
        '__cdecl',
        '__stdcall',
        '__fastcall',
        '__userpurge',
        '__usercall',
        '__clrcall',
    ]

    # Decompiler type patterns (regex for flexible matching)
    DECOMPILER_TYPE_PATTERNS = {
        r'\b_BOOL4\b': 'bool',
        r'\b_BOOL1\b': 'bool',
        r'\b_BOOL2\b': 'bool',
        r'\bBOOL\b': 'bool',
        r'\b__int64\b': 'int64_t',
        r'\b__int32\b': 'int32_t',
        r'\b__int16\b': 'int16_t',
        r'\b__int8\b': 'int8_t',
        r'\bundefined4\b': 'uint32_t',
        r'\bundefined2\b': 'uint16_t',
        r'\bundefined1\b': 'uint8_t',
        r'\bundefined\b': 'uint8_t',
        r'\blonglong\b': 'int64_t',
        r'\bulonglong\b': 'uint64_t',
    }

    def __init__(
        self,
        db_handler: 'DatabaseHandler',
        type_resolver: Optional['TypeResolver'] = None,
        constant_replacer: Optional['ConstantReplacer'] = None,
    ):
        """Initialize the code preprocessor.

        Args:
            db_handler: Database handler for lookups.
            type_resolver: Optional TypeResolver instance. Created if not provided.
            constant_replacer: Optional ConstantReplacer. Created if not provided.
        """
        self.db = db_handler
        self._type_resolver = type_resolver
        self._constant_replacer = constant_replacer

        # Compile regex patterns for efficiency
        self._calling_conv_pattern = re.compile(
            r'\b(' + '|'.join(re.escape(cc) for cc in self.CALLING_CONVENTIONS) + r')\b'
        )
        self._type_patterns = {
            re.compile(pattern): replacement
            for pattern, replacement in self.DECOMPILER_TYPE_PATTERNS.items()
        }

        logger.debug("CodePreprocessor initialized")

    @property
    def type_resolver(self) -> 'TypeResolver':
        """Get or create TypeResolver instance."""
        if self._type_resolver is None:
            from .type_resolver import TypeResolver
            self._type_resolver = TypeResolver(self.db)
        return self._type_resolver

    @property
    def constant_replacer(self) -> 'ConstantReplacer':
        """Get or create ConstantReplacer instance."""
        if self._constant_replacer is None:
            from .constant_replacer import ConstantReplacer
            self._constant_replacer = ConstantReplacer(self.db)
        return self._constant_replacer

    def preprocess(
        self,
        code: str,
        replace_types: bool = True,
        remove_calling_conventions: bool = True,
        annotate_enums: bool = True,
        annotate_constants: bool = True,
    ) -> PreprocessingResult:
        """Pre-process decompiled code.

        Applies all configured transformations to clean up the code
        before sending to the LLM.

        Args:
            code: Raw decompiled C++ code.
            replace_types: Replace decompiler types with modern equivalents.
            remove_calling_conventions: Remove __thiscall, etc.
            annotate_enums: Add comments for magic numbers matching enum values.
            annotate_constants: Add comments for known constants.

        Returns:
            PreprocessingResult with processed code and metadata.
        """
        result = PreprocessingResult(
            code=code,
            original_code=code,
        )

        # Extract numeric literals first (before any modifications)
        result.numeric_literals = self._extract_numeric_literals(code)

        # 1. Remove calling conventions
        if remove_calling_conventions:
            result.code, conventions = self._remove_calling_conventions(result.code)
            result.calling_conventions_removed = conventions

        # 2. Replace decompiler types
        if replace_types:
            result.code, replacements = self._replace_decompiler_types(result.code)
            result.decompiler_types_replaced = replacements

        # 3. Annotate enum values
        if annotate_enums:
            result.code, annotations = self._annotate_enum_values(
                result.code, result.numeric_literals
            )
            result.enum_annotations_added = annotations

        # 4. Annotate constants
        if annotate_constants:
            original_len = len(result.code)
            result.code = self.constant_replacer.process_code(result.code)
            # Rough estimate of annotations added
            if len(result.code) > original_len:
                result.constants_annotated = (len(result.code) - original_len) // 20

        if result.was_modified:
            logger.debug(f"Preprocessing: {result.get_summary()}")

        return result

    def _remove_calling_conventions(self, code: str) -> Tuple[str, List[str]]:
        """Remove calling convention keywords from code.

        Args:
            code: C++ code to process.

        Returns:
            Tuple of (processed_code, list_of_removed_conventions).
        """
        removed = []

        def replacer(match):
            removed.append(match.group(1))
            return ''

        processed = self._calling_conv_pattern.sub(replacer, code)

        # Clean up any double spaces left behind
        processed = re.sub(r'  +', ' ', processed)

        return processed, removed

    def _replace_decompiler_types(self, code: str) -> Tuple[str, Dict[str, str]]:
        """Replace decompiler-specific types with modern C++ types.

        Args:
            code: C++ code to process.

        Returns:
            Tuple of (processed_code, dict_of_replacements).
        """
        replacements = {}

        for pattern, replacement in self._type_patterns.items():
            matches = pattern.findall(code)
            if matches:
                for match in matches:
                    if match not in replacements:
                        replacements[match] = replacement
                code = pattern.sub(replacement, code)

        return code, replacements

    def _extract_numeric_literals(self, code: str) -> Set[int]:
        """Extract numeric literals from code.

        Args:
            code: C++ code to analyze.

        Returns:
            Set of integer values found.
        """
        values: Set[int] = set()

        # Extract hex literals
        hex_pattern = r'0[xX][0-9a-fA-F]+'
        for match in re.findall(hex_pattern, code):
            try:
                values.add(int(match, 16))
            except ValueError:
                pass

        # Extract decimal literals (avoid parts of identifiers)
        dec_pattern = r'(?<![0-9a-zA-Z_])\d+(?![0-9a-zA-Z_])'
        for match in re.findall(dec_pattern, code):
            try:
                val = int(match)
                values.add(val)
            except ValueError:
                pass

        # Filter out very common non-enum values
        values -= {0, 1, -1}

        return values

    def _annotate_enum_values(
        self,
        code: str,
        numeric_literals: Set[int],
    ) -> Tuple[str, Dict[int, str]]:
        """Annotate numeric literals with matching enum values.

        Instead of replacing values, this adds inline comments to help
        the LLM understand potential enum values.

        Args:
            code: C++ code to process.
            numeric_literals: Set of numeric values to look up.

        Returns:
            Tuple of (annotated_code, dict_of_annotations).
        """
        if not numeric_literals:
            return code, {}

        annotations: Dict[int, str] = {}

        # Get enum values from TypeResolver
        for value in numeric_literals:
            enum_matches = self.type_resolver.find_enum_by_value(value)
            if enum_matches:
                # Use first match (most specific)
                ev = enum_matches[0]
                annotations[value] = f"{ev.enum_type}::{ev.name}"

        if not annotations:
            return code, {}

        # Annotate in code (add comments for hex literals)
        def annotate_hex(match):
            hex_str = match.group(0)
            try:
                val = int(hex_str, 16)
                if val in annotations:
                    return f"{hex_str} /* {annotations[val]} */"
            except ValueError:
                pass
            return hex_str

        # Annotate hex literals
        code = re.sub(r'0[xX][0-9a-fA-F]+', annotate_hex, code)

        # Annotate decimal literals (be more careful to avoid false positives)
        def annotate_decimal(match):
            dec_str = match.group(0)
            try:
                val = int(dec_str)
                if val in annotations and val > 2:  # Skip 0, 1, 2
                    return f"{dec_str} /* {annotations[val]} */"
            except ValueError:
                pass
            return dec_str

        # Only annotate decimal literals in specific contexts
        # (comparisons, switch cases, assignments)
        code = re.sub(
            r'(?<=[=<>!])\s*(\d+)(?!\d)',
            lambda m: annotate_decimal(m) if m.group(1).isdigit() else m.group(0),
            code
        )
        code = re.sub(
            r'(?<=case\s)(\d+)(?=:)',
            lambda m: annotate_decimal(m),
            code
        )

        return code, annotations

    def preprocess_for_prompt(
        self,
        code: str,
        parent_class: Optional[str] = None,
    ) -> str:
        """Pre-process code specifically for LLM prompt inclusion.

        This is a convenience method that applies all transformations
        and returns just the processed code string.

        Args:
            code: Raw decompiled C++ code.
            parent_class: Optional parent class name for context.

        Returns:
            Processed code ready for LLM prompt.
        """
        result = self.preprocess(code)
        return result.code

    def get_transformation_summary(self, code: str) -> str:
        """Get a summary of what transformations would be applied.

        Useful for debugging and understanding what the preprocessor does.

        Args:
            code: Code to analyze.

        Returns:
            Human-readable summary of transformations.
        """
        result = self.preprocess(code)
        lines = [
            "Code Preprocessing Summary:",
            f"  Original length: {len(result.original_code)} chars",
            f"  Processed length: {len(result.code)} chars",
        ]

        if result.decompiler_types_replaced:
            lines.append("  Type replacements:")
            for old, new in result.decompiler_types_replaced.items():
                lines.append(f"    {old} -> {new}")

        if result.calling_conventions_removed:
            lines.append(f"  Calling conventions removed: {result.calling_conventions_removed}")

        if result.enum_annotations_added:
            lines.append("  Enum annotations:")
            for val, enum in result.enum_annotations_added.items():
                lines.append(f"    {val} -> {enum}")

        if result.constants_annotated:
            lines.append(f"  Constants annotated: ~{result.constants_annotated}")

        return "\n".join(lines)
