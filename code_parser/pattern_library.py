"""
Pattern Library - Cross-session pattern storage and retrieval using claude-mem.

This module provides persistent storage and intelligent retrieval of successful
C++ code transformation patterns. It enables learning from successful
modernizations and reusing patterns across similar classes/methods.

Key Features:
- Pattern extraction from successful transformations
- Semantic pattern matching using code signatures
- Cross-session persistence via claude-mem MCP
- Confidence scoring based on verification history

Pattern Categories:
1. Structural Patterns:
   - Virtual method call transformations
   - Inheritance pattern modernization
   - Template usage patterns

2. Idiom Patterns:
   - BOOL to bool conversion
   - Null check patterns
   - Loop modernization (while->for, etc.)

3. Type Patterns:
   - SmartArray<T> to std::vector<T>
   - RefCount to shared_ptr
   - Raw pointers to smart pointers

4. Error Patterns (Anti-patterns):
   - Transformations that broke logic
   - Patterns to avoid
"""

from __future__ import annotations

import hashlib
import json
import logging
import re
from dataclasses import dataclass, field, asdict
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional, Set, Tuple, TYPE_CHECKING
from enum import Enum

if TYPE_CHECKING:
    from .db_handler import DatabaseHandler

logger = logging.getLogger(__name__)


class PatternCategory(str, Enum):
    """Categories for transformation patterns."""
    STRUCTURAL = "structural"     # Class structure, inheritance, virtuals
    IDIOM = "idiom"               # Code idioms (null checks, loops, etc.)
    TYPE = "type"                 # Type transformations
    ENUM = "enum"                 # Enum value replacements
    CONTROL_FLOW = "control_flow" # Control flow transformations
    MEMORY = "memory"             # Memory management patterns
    ERROR = "error"               # Anti-patterns (failed transformations)


class PatternConfidence(str, Enum):
    """Confidence levels for pattern matches."""
    HIGH = "HIGH"       # Exact or near-exact match
    MEDIUM = "MEDIUM"   # Similar structure/context
    LOW = "LOW"         # Weak similarity


@dataclass
class CodeSignature:
    """Signature for matching code patterns.

    A code signature captures the essential structural elements of a code
    fragment that determine which transformation patterns apply to it.
    """
    # Structural features
    has_virtual_calls: bool = False
    has_loops: bool = False
    has_conditionals: bool = False
    has_switch: bool = False
    has_pointer_arithmetic: bool = False
    has_casts: bool = False

    # Type features
    parameter_types: List[str] = field(default_factory=list)
    return_type: Optional[str] = None
    referenced_types: Set[str] = field(default_factory=set)
    uses_enums: bool = False

    # Context features
    parent_class: Optional[str] = None
    is_virtual: bool = False
    is_constructor: bool = False
    is_destructor: bool = False

    # Decompiler artifacts present
    has_thiscall: bool = False
    has_explicit_this: bool = False
    has_goto: bool = False
    has_labels: bool = False

    def to_hash(self) -> str:
        """Generate a hash for this signature for quick matching."""
        sig_dict = {
            'structural': (
                self.has_virtual_calls,
                self.has_loops,
                self.has_conditionals,
                self.has_switch,
                self.has_pointer_arithmetic,
            ),
            'types': tuple(sorted(self.parameter_types)),
            'return': self.return_type,
            'context': (
                self.is_virtual,
                self.is_constructor,
                self.is_destructor,
            ),
            'artifacts': (
                self.has_thiscall,
                self.has_explicit_this,
                self.has_goto,
            ),
        }
        sig_str = json.dumps(sig_dict, sort_keys=True)
        return hashlib.md5(sig_str.encode()).hexdigest()[:12]

    def similarity_score(self, other: 'CodeSignature') -> float:
        """Calculate similarity score between two signatures (0.0 to 1.0)."""
        matches = 0
        total = 0

        # Structural features (weight: 3)
        structural_attrs = [
            'has_virtual_calls', 'has_loops', 'has_conditionals',
            'has_switch', 'has_pointer_arithmetic', 'has_casts'
        ]
        for attr in structural_attrs:
            total += 3
            if getattr(self, attr) == getattr(other, attr):
                matches += 3

        # Context features (weight: 2)
        context_attrs = ['is_virtual', 'is_constructor', 'is_destructor']
        for attr in context_attrs:
            total += 2
            if getattr(self, attr) == getattr(other, attr):
                matches += 2

        # Type overlap (weight: 2)
        if self.return_type and other.return_type:
            total += 2
            if self.return_type == other.return_type:
                matches += 2

        # Artifact features (weight: 1)
        artifact_attrs = ['has_thiscall', 'has_explicit_this', 'has_goto']
        for attr in artifact_attrs:
            total += 1
            if getattr(self, attr) == getattr(other, attr):
                matches += 1

        return matches / total if total > 0 else 0.0


@dataclass
class TransformationPattern:
    """A reusable code transformation pattern.

    Captures both the before/after code and the structural transformation
    rules that can be applied to similar code.
    """
    # Identity
    id: str                           # Unique pattern ID
    name: str                         # Human-readable name
    category: PatternCategory

    # Pattern content
    before_signature: CodeSignature   # Signature of input code
    before_template: str              # Template/example of input code
    after_template: str               # Template/example of output code

    # Transformation rules (extracted from before->after)
    transformations: List[str] = field(default_factory=list)

    # Metadata
    source_class: Optional[str] = None    # Class this was extracted from
    source_method: Optional[str] = None   # Method this was extracted from
    created_at: str = ""
    last_used_at: str = ""

    # Success metrics
    success_count: int = 0
    failure_count: int = 0

    # Tags for searchability
    tags: List[str] = field(default_factory=list)

    @property
    def success_rate(self) -> float:
        """Calculate success rate as a percentage."""
        total = self.success_count + self.failure_count
        return (self.success_count / total * 100) if total > 0 else 0.0

    @property
    def confidence(self) -> PatternConfidence:
        """Determine confidence level based on success history."""
        if self.success_count >= 5 and self.success_rate >= 90:
            return PatternConfidence.HIGH
        elif self.success_count >= 2 and self.success_rate >= 70:
            return PatternConfidence.MEDIUM
        else:
            return PatternConfidence.LOW

    def to_dict(self) -> Dict[str, Any]:
        """Serialize pattern to dictionary for storage."""
        # Convert signature to dict, handling non-JSON-serializable Set
        sig_dict = asdict(self.before_signature)
        sig_dict['referenced_types'] = list(sig_dict.get('referenced_types', set()))

        return {
            'id': self.id,
            'name': self.name,
            'category': self.category.value,
            'before_signature': sig_dict,
            'before_template': self.before_template,
            'after_template': self.after_template,
            'transformations': self.transformations,
            'source_class': self.source_class,
            'source_method': self.source_method,
            'created_at': self.created_at,
            'last_used_at': self.last_used_at,
            'success_count': self.success_count,
            'failure_count': self.failure_count,
            'tags': self.tags,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'TransformationPattern':
        """Deserialize pattern from dictionary."""
        sig_data = data.get('before_signature', {})
        signature = CodeSignature(
            has_virtual_calls=sig_data.get('has_virtual_calls', False),
            has_loops=sig_data.get('has_loops', False),
            has_conditionals=sig_data.get('has_conditionals', False),
            has_switch=sig_data.get('has_switch', False),
            has_pointer_arithmetic=sig_data.get('has_pointer_arithmetic', False),
            has_casts=sig_data.get('has_casts', False),
            parameter_types=sig_data.get('parameter_types', []),
            return_type=sig_data.get('return_type'),
            referenced_types=set(sig_data.get('referenced_types', [])),
            uses_enums=sig_data.get('uses_enums', False),
            parent_class=sig_data.get('parent_class'),
            is_virtual=sig_data.get('is_virtual', False),
            is_constructor=sig_data.get('is_constructor', False),
            is_destructor=sig_data.get('is_destructor', False),
            has_thiscall=sig_data.get('has_thiscall', False),
            has_explicit_this=sig_data.get('has_explicit_this', False),
            has_goto=sig_data.get('has_goto', False),
            has_labels=sig_data.get('has_labels', False),
        )

        return cls(
            id=data['id'],
            name=data['name'],
            category=PatternCategory(data.get('category', 'idiom')),
            before_signature=signature,
            before_template=data.get('before_template', ''),
            after_template=data.get('after_template', ''),
            transformations=data.get('transformations', []),
            source_class=data.get('source_class'),
            source_method=data.get('source_method'),
            created_at=data.get('created_at', ''),
            last_used_at=data.get('last_used_at', ''),
            success_count=data.get('success_count', 0),
            failure_count=data.get('failure_count', 0),
            tags=data.get('tags', []),
        )


class SignatureExtractor:
    """Extracts code signatures from C++ code fragments."""

    # Regex patterns for signature extraction
    PATTERNS = {
        'virtual_call': re.compile(r'\bvirtual\b|->[\w_]+\('),
        'loop': re.compile(r'\b(for|while|do)\s*\('),
        'conditional': re.compile(r'\bif\s*\('),
        'switch': re.compile(r'\bswitch\s*\('),
        'pointer_arith': re.compile(r'[+\-]\s*\d+\s*\]|\[\s*[\w_]+\s*[+\-]'),
        'cast': re.compile(r'\(\s*\w+\s*\*?\s*\)'),
        'thiscall': re.compile(r'__thiscall'),
        'explicit_this': re.compile(r'\bthis\s*->'),
        'goto': re.compile(r'\bgoto\s+\w+'),
        'label': re.compile(r'^[\w_]+:\s*$', re.MULTILINE),
        'enum_usage': re.compile(r'::\w+\s*[=!<>]=?\s*\d+|\d+\s*[=!<>]=?\s*::\w+'),
    }

    # Return type pattern
    RETURN_TYPE_PATTERN = re.compile(
        r'^(?:static\s+)?(?:virtual\s+)?(\w+(?:\s*\*)?)\s+(?:__\w+\s+)?[\w_]+::[\w_]+\s*\(',
        re.MULTILINE
    )

    # Parameter type pattern
    PARAM_TYPE_PATTERN = re.compile(
        r'(?:const\s+)?(\w+)(?:\s*[*&])?\s+\w+(?:\s*,|\s*\))',
    )

    def extract(self, code: str, context: Optional[Dict[str, Any]] = None) -> CodeSignature:
        """Extract a code signature from C++ code.

        Args:
            code: The C++ code to analyze.
            context: Optional additional context (parent_class, is_virtual, etc.)

        Returns:
            CodeSignature capturing the code's structural features.
        """
        context = context or {}

        sig = CodeSignature(
            has_virtual_calls=bool(self.PATTERNS['virtual_call'].search(code)),
            has_loops=bool(self.PATTERNS['loop'].search(code)),
            has_conditionals=bool(self.PATTERNS['conditional'].search(code)),
            has_switch=bool(self.PATTERNS['switch'].search(code)),
            has_pointer_arithmetic=bool(self.PATTERNS['pointer_arith'].search(code)),
            has_casts=bool(self.PATTERNS['cast'].search(code)),
            has_thiscall=bool(self.PATTERNS['thiscall'].search(code)),
            has_explicit_this=bool(self.PATTERNS['explicit_this'].search(code)),
            has_goto=bool(self.PATTERNS['goto'].search(code)),
            has_labels=bool(self.PATTERNS['label'].search(code)),
            uses_enums=bool(self.PATTERNS['enum_usage'].search(code)),
            parent_class=context.get('parent_class'),
            is_virtual=context.get('is_virtual', False),
            is_constructor=context.get('is_constructor', False),
            is_destructor=context.get('is_destructor', False),
        )

        # Extract return type
        return_match = self.RETURN_TYPE_PATTERN.search(code)
        if return_match:
            sig.return_type = return_match.group(1).strip()

        # Extract parameter types
        param_matches = self.PARAM_TYPE_PATTERN.findall(code)
        sig.parameter_types = [
            t for t in param_matches
            if t.lower() not in ('const', 'int', 'unsigned', 'char', 'void')
        ]

        return sig


class PatternMatcher:
    """Matches code against stored transformation patterns."""

    def __init__(self, min_similarity: float = 0.6):
        """Initialize the pattern matcher.

        Args:
            min_similarity: Minimum similarity score (0.0-1.0) for a match.
        """
        self.min_similarity = min_similarity
        self.extractor = SignatureExtractor()

    def find_matches(
        self,
        code: str,
        patterns: List[TransformationPattern],
        context: Optional[Dict[str, Any]] = None,
        limit: int = 5,
    ) -> List[Tuple[TransformationPattern, float]]:
        """Find patterns matching the given code.

        Args:
            code: The code to find patterns for.
            patterns: List of available patterns to search.
            context: Optional context for signature extraction.
            limit: Maximum number of matches to return.

        Returns:
            List of (pattern, similarity_score) tuples, sorted by score descending.
        """
        if not patterns:
            return []

        # Extract signature from input code
        input_sig = self.extractor.extract(code, context)

        # Score all patterns
        scored = []
        for pattern in patterns:
            score = input_sig.similarity_score(pattern.before_signature)

            # Boost score for same parent class
            if context and context.get('parent_class') == pattern.source_class:
                score = min(1.0, score * 1.2)

            # Boost score for high-success patterns
            if pattern.success_rate >= 90:
                score = min(1.0, score * 1.1)

            if score >= self.min_similarity:
                scored.append((pattern, score))

        # Sort by score descending and limit
        scored.sort(key=lambda x: x[1], reverse=True)
        return scored[:limit]


class TransformationExtractor:
    """Extracts transformation rules from before/after code pairs."""

    # Common transformation patterns to detect
    TRANSFORMATION_PATTERNS = [
        # Calling convention removal
        (r'__thiscall\s+', '', 'Remove __thiscall'),
        (r'__cdecl\s+', '', 'Remove __cdecl'),
        (r'__stdcall\s+', '', 'Remove __stdcall'),

        # Explicit this removal
        (r'this\s*->\s*', '', 'Remove explicit this->'),

        # Type modernization
        (r'\bBOOL\b', 'bool', 'Replace BOOL with bool'),
        (r'\bNULL\b', 'nullptr', 'Replace NULL with nullptr'),
        (r'\bunsigned int\b', 'uint32_t', 'Use fixed-width integers'),

        # Loop modernization
        (r'while\s*\(\s*1\s*\)', 'for (;;)', 'Use for(;;) for infinite loops'),

        # Smart pointer patterns
        (r'new\s+(\w+)', r'std::make_unique<\1>', 'Use make_unique'),
    ]

    def extract_transformations(
        self,
        before: str,
        after: str,
    ) -> List[str]:
        """Extract transformation rules from a before/after pair.

        Args:
            before: The original code.
            after: The modernized code.

        Returns:
            List of transformation descriptions that were applied.
        """
        transformations = []

        for pattern, replacement, description in self.TRANSFORMATION_PATTERNS:
            before_has = bool(re.search(pattern, before))
            after_has = bool(re.search(pattern, after))

            if before_has and not after_has:
                transformations.append(description)

        # Detect enum replacement
        numeric_before = set(re.findall(r'\b\d+\b', before))
        enum_after = set(re.findall(r'\w+::\w+', after))
        if len(numeric_before) > len(set(re.findall(r'\b\d+\b', after))) and enum_after:
            transformations.append('Replace magic numbers with enum constants')

        # Detect variable renaming
        vars_before = set(re.findall(r'\b[v]\d+\b', before))  # IDA-style vars
        if vars_before and not any(re.search(r'\b[v]\d+\b', after) for _ in [1]):
            transformations.append('Rename decompiler-generated variables')

        return transformations


class PatternLibrary:
    """Main interface for the pattern library.

    Provides methods to:
    - Store successful transformation patterns
    - Query patterns for similar code
    - Update pattern success metrics
    - Persist patterns across sessions (via local SQLite or claude-mem)

    Example usage:
        library = PatternLibrary(db_handler)

        # Store a successful pattern
        pattern = library.create_pattern_from_transformation(
            before_code=original,
            after_code=modernized,
            name="BOOL return to bool",
            category=PatternCategory.TYPE,
        )
        library.save_pattern(pattern)

        # Find matching patterns for new code
        matches = library.find_patterns(new_code, context={'parent_class': 'Player'})
        for pattern, score in matches:
            print(f"Pattern: {pattern.name} (score: {score:.2f})")
    """

    def __init__(
        self,
        db: Optional['DatabaseHandler'] = None,
        patterns_file: Optional[Path] = None,
    ):
        """Initialize the pattern library.

        Args:
            db: Optional database handler for storing patterns.
            patterns_file: Optional path to JSON file for pattern storage.
                If neither db nor patterns_file is provided, uses in-memory storage.
        """
        self.db = db
        self.patterns_file = patterns_file or Path("patterns.json")
        self._patterns: Dict[str, TransformationPattern] = {}
        self._loaded = False

        self.extractor = SignatureExtractor()
        self.matcher = PatternMatcher()
        self.transform_extractor = TransformationExtractor()

        # Load patterns on init
        self._load_patterns()

    def _load_patterns(self) -> None:
        """Load patterns from storage."""
        if self._loaded:
            return

        if self.patterns_file.exists():
            try:
                with open(self.patterns_file, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    for pattern_data in data.get('patterns', []):
                        pattern = TransformationPattern.from_dict(pattern_data)
                        self._patterns[pattern.id] = pattern
                logger.info(f"Loaded {len(self._patterns)} patterns from {self.patterns_file}")
            except Exception as e:
                logger.warning(f"Failed to load patterns: {e}")

        self._loaded = True

    def _save_patterns(self) -> None:
        """Save patterns to storage."""
        try:
            data = {
                'version': '1.0',
                'updated_at': datetime.now().isoformat(),
                'patterns': [p.to_dict() for p in self._patterns.values()],
            }
            with open(self.patterns_file, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
            logger.debug(f"Saved {len(self._patterns)} patterns to {self.patterns_file}")
        except Exception as e:
            logger.error(f"Failed to save patterns: {e}")

    def create_pattern_from_transformation(
        self,
        before_code: str,
        after_code: str,
        name: str,
        category: PatternCategory = PatternCategory.IDIOM,
        source_class: Optional[str] = None,
        source_method: Optional[str] = None,
        tags: Optional[List[str]] = None,
        context: Optional[Dict[str, Any]] = None,
    ) -> TransformationPattern:
        """Create a new pattern from a successful transformation.

        Args:
            before_code: The original decompiled code.
            after_code: The modernized code.
            name: Human-readable name for the pattern.
            category: Pattern category.
            source_class: Class this pattern was extracted from.
            source_method: Method this pattern was extracted from.
            tags: Optional tags for searchability.
            context: Optional context for signature extraction.

        Returns:
            The created TransformationPattern.
        """
        # Extract signature
        signature = self.extractor.extract(before_code, context)

        # Extract transformation rules
        transformations = self.transform_extractor.extract_transformations(
            before_code, after_code
        )

        # Generate unique ID
        sig_hash = signature.to_hash()
        pattern_id = f"pat_{category.value[:3]}_{sig_hash}"

        pattern = TransformationPattern(
            id=pattern_id,
            name=name,
            category=category,
            before_signature=signature,
            before_template=before_code,
            after_template=after_code,
            transformations=transformations,
            source_class=source_class,
            source_method=source_method,
            created_at=datetime.now().isoformat(),
            last_used_at=datetime.now().isoformat(),
            success_count=1,  # Initial success (came from verified transformation)
            failure_count=0,
            tags=tags or [],
        )

        return pattern

    def save_pattern(self, pattern: TransformationPattern) -> None:
        """Save a pattern to the library.

        Args:
            pattern: The pattern to save.
        """
        self._patterns[pattern.id] = pattern
        self._save_patterns()
        logger.info(f"Saved pattern: {pattern.name} ({pattern.id})")

    def get_pattern(self, pattern_id: str) -> Optional[TransformationPattern]:
        """Get a pattern by ID.

        Args:
            pattern_id: The pattern's unique identifier.

        Returns:
            The pattern if found, None otherwise.
        """
        return self._patterns.get(pattern_id)

    def find_patterns(
        self,
        code: str,
        context: Optional[Dict[str, Any]] = None,
        category: Optional[PatternCategory] = None,
        limit: int = 5,
    ) -> List[Tuple[TransformationPattern, float]]:
        """Find patterns matching the given code.

        Args:
            code: The code to find patterns for.
            context: Optional context (parent_class, is_virtual, etc.)
            category: Optional category filter.
            limit: Maximum matches to return.

        Returns:
            List of (pattern, score) tuples sorted by score.
        """
        patterns = list(self._patterns.values())

        # Filter by category if specified
        if category:
            patterns = [p for p in patterns if p.category == category]

        return self.matcher.find_matches(code, patterns, context, limit)

    def record_success(self, pattern_id: str) -> None:
        """Record a successful use of a pattern.

        Args:
            pattern_id: The pattern's unique identifier.
        """
        pattern = self._patterns.get(pattern_id)
        if pattern:
            pattern.success_count += 1
            pattern.last_used_at = datetime.now().isoformat()
            self._save_patterns()

    def record_failure(self, pattern_id: str) -> None:
        """Record a failed use of a pattern.

        Args:
            pattern_id: The pattern's unique identifier.
        """
        pattern = self._patterns.get(pattern_id)
        if pattern:
            pattern.failure_count += 1
            pattern.last_used_at = datetime.now().isoformat()
            self._save_patterns()

    def get_patterns_by_category(
        self,
        category: PatternCategory,
    ) -> List[TransformationPattern]:
        """Get all patterns in a category.

        Args:
            category: The category to filter by.

        Returns:
            List of patterns in that category.
        """
        return [p for p in self._patterns.values() if p.category == category]

    def get_high_confidence_patterns(self) -> List[TransformationPattern]:
        """Get all patterns with HIGH confidence.

        Returns:
            List of high-confidence patterns.
        """
        return [p for p in self._patterns.values() if p.confidence == PatternConfidence.HIGH]

    def format_patterns_for_prompt(
        self,
        patterns: List[Tuple[TransformationPattern, float]],
        max_examples: int = 3,
    ) -> str:
        """Format matched patterns as context for LLM prompt.

        Args:
            patterns: List of (pattern, score) tuples.
            max_examples: Maximum examples to include.

        Returns:
            Formatted string for prompt injection.
        """
        if not patterns:
            return ""

        lines = ["## Similar Transformation Patterns", ""]

        for pattern, score in patterns[:max_examples]:
            lines.append(f"### Pattern: {pattern.name}")
            lines.append(f"Confidence: {pattern.confidence.value} (Success Rate: {pattern.success_rate:.0f}%)")
            lines.append("")

            if pattern.transformations:
                lines.append("Transformations applied:")
                for t in pattern.transformations:
                    lines.append(f"  - {t}")
                lines.append("")

            lines.append("Example input:")
            lines.append("```cpp")
            # Truncate long templates
            template = pattern.before_template
            if len(template) > 500:
                template = template[:500] + "\n// ... (truncated)"
            lines.append(template)
            lines.append("```")
            lines.append("")

            lines.append("Example output:")
            lines.append("```cpp")
            template = pattern.after_template
            if len(template) > 500:
                template = template[:500] + "\n// ... (truncated)"
            lines.append(template)
            lines.append("```")
            lines.append("")

        return "\n".join(lines)

    def get_stats(self) -> Dict[str, Any]:
        """Get statistics about the pattern library.

        Returns:
            Dictionary with library statistics.
        """
        by_category = {}
        for cat in PatternCategory:
            patterns = self.get_patterns_by_category(cat)
            by_category[cat.value] = len(patterns)

        high_conf = len(self.get_high_confidence_patterns())

        total_success = sum(p.success_count for p in self._patterns.values())
        total_failure = sum(p.failure_count for p in self._patterns.values())

        return {
            'total_patterns': len(self._patterns),
            'by_category': by_category,
            'high_confidence_count': high_conf,
            'total_applications': total_success + total_failure,
            'overall_success_rate': (
                total_success / (total_success + total_failure) * 100
                if (total_success + total_failure) > 0 else 0
            ),
        }


# Factory function
def get_pattern_library(
    db: Optional['DatabaseHandler'] = None,
    patterns_dir: Optional[Path] = None,
) -> PatternLibrary:
    """Get a PatternLibrary instance.

    Args:
        db: Optional database handler.
        patterns_dir: Optional directory for pattern storage.

    Returns:
        Configured PatternLibrary instance.
    """
    if patterns_dir:
        patterns_file = patterns_dir / "transformation_patterns.json"
    else:
        patterns_file = Path("mcp-sources") / "transformation_patterns.json"

    return PatternLibrary(db=db, patterns_file=patterns_file)
