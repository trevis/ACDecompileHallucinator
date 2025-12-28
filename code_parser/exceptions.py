"""
Exception Hierarchy for ACDecompile Hallucinator
=================================================

Domain-specific exceptions that provide rich context for debugging
and enable granular error handling throughout the codebase.

Exception Categories:
- ACDecompileError: Base exception for all domain errors
- ParsingError: Header/source parsing failures
- DatabaseError: Database operations failures
- LLMError: LLM API and processing failures
- FileIOError: File read/write failures
- ConfigurationError: Configuration and dependency issues
- ProcessingError: Code processing pipeline failures
"""

from typing import Any, Dict, Optional


class ACDecompileError(Exception):
    """
    Base exception for all ACDecompile Hallucinator errors.

    All domain-specific exceptions inherit from this class to enable
    catching all ACDecompile errors with a single except clause.
    """

    def __init__(self, message: str, context: Optional[Dict[str, Any]] = None):
        """
        Initialize the exception.

        Args:
            message: Human-readable error description
            context: Additional contextual information (file, line, type name, etc.)
        """
        self.message = message
        self.context = context or {}
        super().__init__(self.format_message())

    def format_message(self) -> str:
        """Format message with context information."""
        msg = self.message
        if self.context:
            ctx_str = " | ".join(f"{k}={v}" for k, v in self.context.items())
            msg = f"{msg} [{ctx_str}]"
        return msg


# =============================================================================
# Parsing Errors
# =============================================================================

class ParsingError(ACDecompileError):
    """Base class for all parsing-related errors."""
    pass


class HeaderParsingError(ParsingError):
    """
    Raised when header file parsing fails.

    Use case: Struct/enum parsing, definition extraction, structural validation

    Context attributes:
    - file: Path to header file
    - line_number: Line where parsing failed
    - line_content: The problematic line
    - expected: What was expected
    """
    pass


class SourceParsingError(ParsingError):
    """
    Raised when source file parsing fails.

    Use case: Method/function parsing, offset mapping, signature extraction

    Context attributes:
    - file: Path to source file
    - line_number: Line where parsing failed
    - method_name: Name of method being parsed (if known)
    - offset: Hex offset (if available)
    """
    pass


class MethodSignatureError(ParsingError):
    """
    Raised when method signature parsing fails.

    Use case: Invalid function signature, missing parenthesis, malformed declaration

    Context attributes:
    - signature: The signature being parsed
    - method_name: Method name (if extractable)
    - issue: Specific issue (e.g., "No opening parenthesis found")
    """
    pass


class EncodingError(ParsingError):
    """
    Raised when file encoding detection/conversion fails.

    Use case: File read failures with multiple encoding attempts

    Context attributes:
    - file: Path to file
    - attempted_encodings: List of encodings tried (utf-8, latin-1, cp1252)
    - original_error: The underlying encoding error
    """
    pass


class InvalidStructureError(ParsingError):
    """
    Raised when parsed structure is semantically invalid.

    Use case: Type dependencies missing, circular inheritance, invalid namespaces

    Context attributes:
    - element_type: Type of element (struct, enum, method)
    - element_name: Name of the invalid element
    - reason: Why it's invalid
    """
    pass


# =============================================================================
# Database Errors
# =============================================================================

class DatabaseError(ACDecompileError):
    """Base class for all database-related errors."""
    pass


class DatabaseConnectionError(DatabaseError):
    """
    Raised when database connection fails.

    Use case: Database file missing, locked, corrupted, or permission denied

    Context attributes:
    - db_path: Path to database file
    - operation: Operation that failed (open, connect, etc.)
    - original_error: The underlying sqlite3 error
    """
    pass


class DatabaseQueryError(DatabaseError):
    """
    Raised when a SQL query fails.

    Use case: Syntax errors, table not found, column mismatch

    Context attributes:
    - query: The SQL query that failed
    - params: Query parameters
    - table: Table name (if applicable)
    - original_error: The underlying sqlite3 error
    """
    pass


class DatabaseConstraintError(DatabaseError):
    """
    Raised when database constraint is violated.

    Use case: UNIQUE constraint failure, NOT NULL violation, referential integrity

    Context attributes:
    - constraint: Type of constraint (UNIQUE, NOT NULL, FOREIGN KEY)
    - table: Table name
    - record: Record being inserted/updated
    - conflict_value: The value that caused conflict
    """
    pass


class DatabaseMigrationError(DatabaseError):
    """
    Raised when schema migration fails.

    Use case: ALTER TABLE fails (column already exists, incompatible change)

    Context attributes:
    - migration: Migration being applied
    - target_version: Schema version being upgraded to
    - original_error: The underlying sqlite3 error
    """
    pass


# =============================================================================
# LLM Errors
# =============================================================================

class LLMError(ACDecompileError):
    """Base class for all LLM-related errors."""
    pass


class LLMConnectionError(LLMError):
    """
    Raised when LLM server connection fails.

    Use case: LM Studio not running, connection timeout, network error

    Context attributes:
    - server_url: URL of LLM server (default: http://localhost:1234)
    - timeout: Request timeout in seconds
    - original_error: The underlying network/connection error
    """
    pass


class LLMResponseError(LLMError):
    """
    Raised when LLM response is invalid or malformed.

    Use case: JSON parsing failure, missing fields, invalid structure

    Context attributes:
    - response: The raw response from LLM
    - expected_format: What format was expected
    - parse_error: Why parsing failed
    - prompt_snippet: First 200 chars of the prompt sent
    """
    pass


class LLMProcessingError(LLMError):
    """
    Raised when LLM processing fails during modernization.

    Use case: Generation timeout, token limit exceeded, model error

    Context attributes:
    - prompt_type: Type of prompt (header, method, verification)
    - class_name: Class being processed (if applicable)
    - method_name: Method being processed (if applicable)
    - phase: Processing phase (analysis, generation, verification)
    - original_error: The underlying LLM API error
    """
    pass


class LLMVerificationError(LLMError):
    """
    Raised when LLM verification of modernized code fails.

    Use case: Modernized code doesn't preserve original logic

    Context attributes:
    - verification_result: The verification response
    - original_code: Original decompiled code snippet
    - modernized_code: The modernized code that failed verification
    - reason: Why verification failed
    """
    pass


class LLMCacheError(LLMError):
    """
    Raised when LLM response cache operation fails.

    Use case: Cache database corruption, write failure, read failure

    Context attributes:
    - cache_path: Path to cache database
    - operation: Operation that failed (get, set)
    - prompt_hash: Hash of the prompt (for identifying cached item)
    - original_error: The underlying sqlite3 error
    """
    pass


# =============================================================================
# File I/O Errors
# =============================================================================

class FileIOError(ACDecompileError):
    """Base class for all file I/O errors."""
    pass


class FileNotFoundError(FileIOError):
    """
    Raised when a required file is missing.

    Use case: Input files (acclient.h, acclient.c), config files, offset mapping file

    Context attributes:
    - file_path: Path to missing file
    - file_type: Type of file (header, source, constants, lines mapping)
    - required: Whether the file is required (bool)
    """
    pass


class FileWriteError(FileIOError):
    """
    Raised when file write operations fail.

    Use case: Permission denied, disk full, invalid path

    Context attributes:
    - file_path: Path where write was attempted
    - operation: Type of operation (create, append, overwrite)
    - reason: Why write failed (permission, disk space, etc.)
    """
    pass


class FileEncodingError(FileIOError):
    """
    Raised when file encoding issues occur.

    Use case: File contains invalid bytes for specified encoding

    Context attributes:
    - file_path: Path to file
    - detected_encoding: Encoding that was detected/used
    - fallback_used: Whether fallback was applied
    - bytes_affected: Number of bytes that couldn't be decoded
    """
    pass


class PathError(FileIOError):
    """
    Raised when path operations fail.

    Use case: Invalid path, circular directory, permissions

    Context attributes:
    - path: The problematic path
    - operation: Operation that failed (mkdir, resolve, etc.)
    - reason: Why operation failed
    """
    pass


# =============================================================================
# Configuration Errors
# =============================================================================

class ConfigurationError(ACDecompileError):
    """Base class for all configuration-related errors."""
    pass


class MissingConfigurationError(ConfigurationError):
    """
    Raised when required configuration is missing.

    Use case: LLM client not set, required environment variable missing

    Context attributes:
    - component: Component that needs configuration (e.g., "LLM client")
    - setting: Name of the setting (e.g., "server_url")
    - how_to_fix: Instructions for fixing the issue
    """
    pass


class InvalidConfigurationError(ConfigurationError):
    """
    Raised when configuration value is invalid.

    Use case: Invalid server URL, invalid temperature value, malformed settings

    Context attributes:
    - setting: Name of the setting
    - value: The invalid value
    - expected: What was expected
    - reason: Why it's invalid
    """
    pass


class DependencyMissingError(ConfigurationError):
    """
    Raised when required Python dependency is missing.

    Use case: openai module not installed, missing optional dependency

    Context attributes:
    - module: Name of the missing module
    - install_command: Command to install (e.g., "pip install openai")
    - description: What the module is used for
    """
    pass


# =============================================================================
# Processing Errors
# =============================================================================

class ProcessingError(ACDecompileError):
    """Base class for errors during code processing pipeline."""
    pass


class DependencyResolutionError(ProcessingError):
    """
    Raised when type/class dependencies cannot be resolved.

    Use case: Circular dependencies, missing base class, type not found

    Context attributes:
    - type_name: Type that couldn't be resolved
    - dependency: The unresolved dependency
    - dependency_chain: List of types in the dependency chain
    - missing_type: Name of the type that's actually missing
    """
    pass


class TypeResolutionError(ProcessingError):
    """
    Raised when a type reference cannot be resolved.

    Use case: Type referenced in method signature not found in database

    Context attributes:
    - type_name: Name of the type being looked up
    - context: Where it was encountered (method name, file, etc.)
    - available_types: List of similar types (for suggestions)
    """
    pass


class CodeGenerationError(ProcessingError):
    """
    Raised when code generation fails.

    Use case: Header generation fails, method assembly fails

    Context attributes:
    - component: What was being generated (header, source, etc.)
    - class_name: Class being processed
    - method_name: Method being processed (if applicable)
    - phase: Which phase failed
    - original_error: The underlying error
    """
    pass


class ValidationError(ProcessingError):
    """
    Raised when generated code fails validation.

    Use case: Syntax errors in generated code, invalid C++ structure

    Context attributes:
    - item_type: Type of item being validated (header, method, class)
    - item_name: Name of the item
    - errors: List of validation errors found
    - suggestion: Recommended fix (if available)
    """
    pass
