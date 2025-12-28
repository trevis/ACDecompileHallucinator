"""Error Memory System for tracking and learning from modernization failures.

This module provides functionality to:
1. Record verification failures and their contexts
2. Extract pattern signatures from code for similarity matching
3. Find similar error patterns when processing new code
4. Generate warning text to inject into prompts

The goal is to learn from past mistakes and prevent repeating them.

Storage: Uses SQLite (error_patterns table in types.db) for persistence.
"""

from dataclasses import dataclass
from typing import List, Optional, Set, TYPE_CHECKING
import hashlib
import logging
import re
import sqlite3

if TYPE_CHECKING:
    from .db_handler import DatabaseHandler

logger = logging.getLogger(__name__)


# Error category constants (for backward compatibility and type hints)
class ErrorCategory:
    """Error category constants for categorizing transformation failures."""
    SYNTAX = "syntax"
    LOGIC = "logic"
    ARTIFACT = "artifact"
    ANTI_PATTERN = "anti-pattern"
    VERIFICATION = "verification"
    TIMEOUT = "timeout"
    UNKNOWN = "unknown"


@dataclass
class ErrorPattern:
    """Represents a recorded error pattern from a failed transformation."""

    id: int
    category: str  # 'syntax', 'logic', 'artifact', 'anti-pattern'
    pattern_hash: str
    pattern_signature: str
    original_snippet: str
    failed_output: Optional[str]
    correct_output: Optional[str]
    error_description: str
    method_name: Optional[str]
    class_name: Optional[str]
    occurrence_count: int
    first_seen_at: str
    last_seen_at: str


class ErrorMemory:
    """Tracks and learns from modernization failures.

    This class provides the core functionality for the Error Memory feature:
    - Recording failures with their context
    - Extracting code pattern signatures for similarity matching
    - Finding similar patterns when processing new code
    - Generating warning prompts based on past failures

    Storage is in SQLite (error_patterns table in types.db).

    Example:
        from code_parser import DatabaseHandler, get_error_memory

        db = DatabaseHandler("mcp-sources/types.db")
        memory = get_error_memory(db)

        # Check for warnings before processing
        warnings = memory.get_warnings_for_code(raw_code)

        # Record a failure
        memory.record_failure(
            category="logic",
            original_code=raw_code,
            failed_output=modernized,
            error_description="Control flow changed",
            method_name="TakeDamage",
            class_name="Player",
        )

        # Record successful retry
        memory.record_success_after_retry(raw_code, working_code)

    Attributes:
        db: The database handler for persistence.
    """

    # Error categories
    CATEGORY_SYNTAX = "syntax"
    CATEGORY_LOGIC = "logic"
    CATEGORY_ARTIFACT = "artifact"
    CATEGORY_ANTI_PATTERN = "anti-pattern"

    def __init__(self, db: "DatabaseHandler"):
        """Initialize ErrorMemory with a database handler.

        Args:
            db: DatabaseHandler instance for persistence.
        """
        self.db = db
        self._ensure_table_exists()

    def _ensure_table_exists(self) -> None:
        """Create the error_patterns table if it doesn't exist."""
        with sqlite3.connect(self.db.db_path) as conn:
            cursor = conn.cursor()

            cursor.execute('''
                CREATE TABLE IF NOT EXISTS error_patterns (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    category TEXT NOT NULL,
                    pattern_hash TEXT NOT NULL,
                    pattern_signature TEXT,
                    original_snippet TEXT NOT NULL,
                    failed_output TEXT,
                    correct_output TEXT,
                    error_description TEXT NOT NULL,
                    method_name TEXT,
                    class_name TEXT,
                    occurrence_count INTEGER DEFAULT 1,
                    first_seen_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    last_seen_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(pattern_hash)
                )
            ''')

            cursor.execute(
                'CREATE INDEX IF NOT EXISTS idx_error_category '
                'ON error_patterns(category)'
            )
            cursor.execute(
                'CREATE INDEX IF NOT EXISTS idx_error_signature '
                'ON error_patterns(pattern_signature)'
            )

            conn.commit()

    def extract_pattern_signature(self, code: str) -> Set[str]:
        """Extract identifying patterns from code for similarity matching.

        Analyzes the code to identify key patterns that might be associated
        with specific types of errors. These patterns are used to find
        similar code when processing new functions.

        Args:
            code: The C++ code to analyze.

        Returns:
            Set of pattern identifiers found in the code.
        """
        patterns: Set[str] = set()

        # Decompiler artifacts
        if "__thiscall" in code:
            patterns.add("decompiler:thiscall")
        if "__cdecl" in code:
            patterns.add("decompiler:cdecl")
        if "__stdcall" in code:
            patterns.add("decompiler:stdcall")
        if "__fastcall" in code:
            patterns.add("decompiler:fastcall")
        if "__userpurge" in code or "__usercall" in code:
            patterns.add("decompiler:usercall")
        if "this->" in code:
            patterns.add("decompiler:explicit_this")

        # Vtable patterns (common source of errors)
        if re.search(r'\*\*\s*\(\s*void\s*\(', code):
            patterns.add("pattern:vtable_call")
        if re.search(r'\*\s*\(\s*\*\s*\w+\s*\)', code):
            patterns.add("pattern:vtable_indirect")
        if "vtable" in code.lower():
            patterns.add("pattern:vtable_ref")

        # Control flow patterns
        if "switch" in code:
            patterns.add("flow:switch")
        if re.search(r'if\s*\([^)]*==\s*\d+', code):
            patterns.add("flow:magic_number_compare")
        if re.search(r'while\s*\(\s*1\s*\)', code) or re.search(r'for\s*\(\s*;\s*;\s*\)', code):
            patterns.add("flow:infinite_loop")
        if "goto " in code:
            patterns.add("flow:goto")

        # Memory patterns
        if re.search(r'if\s*\(\s*\w+\s*\)', code):
            patterns.add("pattern:null_check")
        if re.search(r'if\s*\(\s*!\s*\w+\s*\)', code):
            patterns.add("pattern:null_check_negated")
        if "delete " in code or "free(" in code:
            patterns.add("memory:delete")
        if "new " in code or "malloc" in code:
            patterns.add("memory:alloc")

        # Bitwise operations (often source of logic errors)
        if re.search(r'\|=', code):
            patterns.add("pattern:bitwise_or_assign")
        if re.search(r'\&=', code):
            patterns.add("pattern:bitwise_and_assign")
        if re.search(r'<<|>>', code):
            patterns.add("pattern:bitshift")

        # Cast patterns
        if re.search(r'\(\s*\w+\s*\*\s*\)', code):
            patterns.add("pattern:pointer_cast")
        if "reinterpret_cast" in code or "static_cast" in code:
            patterns.add("pattern:cpp_cast")

        # Destructor patterns
        if re.search(r'~\w+\s*\(', code):
            patterns.add("pattern:destructor")

        # Virtual method patterns
        if "virtual" in code:
            patterns.add("pattern:virtual")
        if "override" in code:
            patterns.add("pattern:override")

        return patterns

    def _compute_pattern_hash(self, code: str) -> str:
        """Compute a hash for the code pattern.

        Normalizes the code before hashing to group similar patterns.

        Args:
            code: The code to hash.

        Returns:
            SHA256 hash of the normalized code.
        """
        # Normalize: remove whitespace variations, normalize identifiers
        normalized = re.sub(r'\s+', ' ', code.strip())
        # Remove specific variable names to group similar patterns
        normalized = re.sub(r'\b(v\d+|a\d+|result)\b', 'VAR', normalized)
        return hashlib.sha256(normalized.encode()).hexdigest()[:32]

    def record_failure(
        self,
        category: str,
        original_code: str,
        failed_output: str,
        error_description: str,
        method_name: Optional[str] = None,
        class_name: Optional[str] = None,
    ) -> int:
        """Record a verification failure for future reference.

        Args:
            category: Error category (syntax, logic, artifact, anti-pattern).
            original_code: The original decompiled code.
            failed_output: The LLM's failed transformation.
            error_description: Description of why it failed.
            method_name: Optional method name for context.
            class_name: Optional class name for context.

        Returns:
            The ID of the created/updated error pattern.
        """
        pattern_hash = self._compute_pattern_hash(original_code)
        pattern_signature = ",".join(sorted(self.extract_pattern_signature(original_code)))

        with sqlite3.connect(self.db.db_path) as conn:
            cursor = conn.cursor()

            # Try to update existing pattern
            cursor.execute('''
                UPDATE error_patterns
                SET occurrence_count = occurrence_count + 1,
                    last_seen_at = CURRENT_TIMESTAMP,
                    failed_output = ?,
                    error_description = ?
                WHERE pattern_hash = ?
            ''', (failed_output, error_description, pattern_hash))

            if cursor.rowcount == 0:
                # Insert new pattern
                cursor.execute('''
                    INSERT INTO error_patterns (
                        category, pattern_hash, pattern_signature,
                        original_snippet, failed_output, error_description,
                        method_name, class_name
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                ''', (
                    category, pattern_hash, pattern_signature,
                    original_code, failed_output, error_description,
                    method_name, class_name
                ))
                pattern_id = cursor.lastrowid
            else:
                cursor.execute(
                    'SELECT id FROM error_patterns WHERE pattern_hash = ?',
                    (pattern_hash,)
                )
                pattern_id = cursor.fetchone()[0]

            conn.commit()

            logger.info(
                f"Recorded error pattern {pattern_id} ({category}): "
                f"{error_description[:50]}..."
            )

            return pattern_id

    def record_success_after_retry(
        self,
        original_code: str,
        correct_output: str,
    ) -> bool:
        """Record the correct solution after a successful retry.

        Updates an existing error pattern with the working solution,
        which can be used to guide future transformations.

        Args:
            original_code: The original code (used to find the pattern).
            correct_output: The successful transformation.

        Returns:
            True if an existing pattern was updated, False otherwise.
        """
        pattern_hash = self._compute_pattern_hash(original_code)

        with sqlite3.connect(self.db.db_path) as conn:
            cursor = conn.cursor()

            cursor.execute('''
                UPDATE error_patterns
                SET correct_output = ?,
                    last_seen_at = CURRENT_TIMESTAMP
                WHERE pattern_hash = ?
            ''', (correct_output, pattern_hash))

            updated = cursor.rowcount > 0
            conn.commit()

            if updated:
                logger.info(
                    f"Updated error pattern with correct solution for hash {pattern_hash[:8]}..."
                )

            return updated

    def find_matching_patterns(
        self,
        code: str,
        limit: int = 5,
    ) -> List[ErrorPattern]:
        """Find error patterns that might apply to the given code.

        Uses pattern signature matching to find similar code patterns
        that have caused errors in the past.

        Args:
            code: The code to analyze.
            limit: Maximum number of patterns to return.

        Returns:
            List of matching ErrorPattern objects, ranked by relevance.
        """
        signatures = self.extract_pattern_signature(code)
        if not signatures:
            return []

        with sqlite3.connect(self.db.db_path) as conn:
            conn.row_factory = sqlite3.Row
            cursor = conn.cursor()

            # Get all patterns and score them by signature overlap
            cursor.execute('SELECT * FROM error_patterns ORDER BY occurrence_count DESC')

            scored_patterns: List[tuple] = []
            for row in cursor.fetchall():
                stored_sigs = set(row['pattern_signature'].split(',')) if row['pattern_signature'] else set()
                overlap = len(signatures & stored_sigs)

                if overlap > 0:
                    # Score: overlap count + recency bonus + occurrence bonus
                    score = overlap * 10 + row['occurrence_count']
                    scored_patterns.append((score, ErrorPattern(
                        id=row['id'],
                        category=row['category'],
                        pattern_hash=row['pattern_hash'],
                        pattern_signature=row['pattern_signature'] or '',
                        original_snippet=row['original_snippet'],
                        failed_output=row['failed_output'],
                        correct_output=row['correct_output'],
                        error_description=row['error_description'],
                        method_name=row['method_name'],
                        class_name=row['class_name'],
                        occurrence_count=row['occurrence_count'],
                        first_seen_at=row['first_seen_at'],
                        last_seen_at=row['last_seen_at'],
                    )))

            # Sort by score descending and return top N
            scored_patterns.sort(key=lambda x: x[0], reverse=True)
            return [p for _, p in scored_patterns[:limit]]

    def get_warnings_for_code(self, code: str, max_warnings: int = 3) -> str:
        """Generate warning text to inject into prompts.

        Finds error patterns similar to the given code and generates
        a formatted warning section to include in the LLM prompt.

        Args:
            code: The code being processed.
            max_warnings: Maximum number of warnings to include.

        Returns:
            Formatted warning text, or empty string if no relevant warnings.
        """
        patterns = self.find_matching_patterns(code, limit=max_warnings)

        if not patterns:
            return ""

        lines = [
            "## Known Issues Warning",
            "",
            "Previous transformations of similar code encountered these issues. "
            "Pay special attention to avoid repeating these mistakes:",
            "",
        ]

        for i, pattern in enumerate(patterns, 1):
            lines.append(f"### Issue {i}: {pattern.category.upper()}")
            lines.append(f"**Problem**: {pattern.error_description}")

            if pattern.correct_output:
                # Show a snippet of the correct approach
                correct_preview = pattern.correct_output[:200]
                if len(pattern.correct_output) > 200:
                    correct_preview += "..."
                lines.append("**Correct approach**:")
                lines.append("```cpp")
                lines.append(correct_preview)
                lines.append("```")

            lines.append("")

        return "\n".join(lines)

    def categorize_error(self, error_description: str) -> str:
        """Attempt to categorize an error based on its description.

        Args:
            error_description: The error/verification feedback.

        Returns:
            One of the CATEGORY_* constants.
        """
        desc_lower = error_description.lower()

        # Syntax errors
        syntax_keywords = [
            "syntax", "parse", "semicolon", "bracket", "brace",
            "unexpected token", "invalid", "compile"
        ]
        if any(kw in desc_lower for kw in syntax_keywords):
            return self.CATEGORY_SYNTAX

        # Decompiler artifact issues
        artifact_keywords = [
            "__thiscall", "__cdecl", "this->", "decompiler",
            "artifact", "__userpurge", "__usercall"
        ]
        if any(kw in desc_lower for kw in artifact_keywords):
            return self.CATEGORY_ARTIFACT

        # Anti-patterns
        anti_pattern_keywords = [
            "don't remove", "preserve", "keep", "should not",
            "must maintain", "required"
        ]
        if any(kw in desc_lower for kw in anti_pattern_keywords):
            return self.CATEGORY_ANTI_PATTERN

        # Default to logic (most common)
        return self.CATEGORY_LOGIC

    def get_stats(self) -> dict:
        """Get statistics about recorded error patterns.

        Returns:
            Dictionary with counts by category and total.
        """
        with sqlite3.connect(self.db.db_path) as conn:
            cursor = conn.cursor()

            cursor.execute('''
                SELECT category, COUNT(*) as count, SUM(occurrence_count) as total_occurrences
                FROM error_patterns
                GROUP BY category
            ''')

            stats = {
                "by_category": {},
                "total_patterns": 0,
                "total_occurrences": 0,
                "with_correct_solution": 0,
            }

            for row in cursor.fetchall():
                stats["by_category"][row[0]] = {
                    "patterns": row[1],
                    "occurrences": row[2],
                }
                stats["total_patterns"] += row[1]
                stats["total_occurrences"] += row[2]

            cursor.execute(
                'SELECT COUNT(*) FROM error_patterns WHERE correct_output IS NOT NULL'
            )
            stats["with_correct_solution"] = cursor.fetchone()[0]

            return stats


def get_error_memory(db: "DatabaseHandler") -> ErrorMemory:
    """Factory function to get an ErrorMemory instance.

    Args:
        db: DatabaseHandler instance.

    Returns:
        ErrorMemory instance.
    """
    return ErrorMemory(db)
