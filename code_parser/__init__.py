"""
Code Parser module - A tool for parsing C++ header files and extracting struct definitions.
"""

from .header_parser import HeaderParser
from .type_writer import TypeWriter
from .struct import Struct
from .enum import Enum
from .method import Method
from .db_handler import DatabaseHandler
from .dependency_analyzer import DependencyAnalyzer
from .class_header_generator import ClassHeaderGenerator
from .function_processor import FunctionProcessor
from .class_assembler import ClassAssembler
from .constants_parser import ConstantsParser
from .constant_replacer import ConstantReplacer
from .llm_cache import LLMCache
from .type_resolver import TypeResolver
from .skill_loader import SkillLoader, get_skill_loader
from .context_builder import ContextBuilder, ContextResult, EnumValueMapping
from .code_preprocessor import CodePreprocessor, PreprocessingResult
from .error_memory import ErrorMemory, ErrorPattern, ErrorCategory, get_error_memory
from .pattern_library import PatternLibrary, PatternCategory, get_pattern_library
# NOTE: LLMClient and LLMProcessor have been removed.
# Use engines.get_engine("lm-studio") or engines.get_engine("claude-code") instead.
# The LLMProcessor class is now in llm_process.py (CLI entry point).
from .exceptions import (
    ACDecompileError,
    ParsingError, HeaderParsingError, SourceParsingError,
    MethodSignatureError, EncodingError, InvalidStructureError,
    DatabaseError, DatabaseConnectionError, DatabaseQueryError,
    DatabaseConstraintError, DatabaseMigrationError,
    LLMError, LLMConnectionError, LLMResponseError,
    LLMProcessingError, LLMVerificationError, LLMCacheError,
    FileIOError, FileNotFoundError, FileWriteError,
    FileEncodingError, PathError,
    ConfigurationError, MissingConfigurationError,
    InvalidConfigurationError, DependencyMissingError,
    ProcessingError, DependencyResolutionError,
    TypeResolutionError, CodeGenerationError, ValidationError
)

__all__ = [
    'HeaderParser', 'TypeWriter', 'Struct', 'Enum', 'Method',
    'DatabaseHandler', 'DependencyAnalyzer', 'ClassHeaderGenerator',
    'FunctionProcessor', 'ClassAssembler', 'ConstantsParser',
    'ConstantReplacer', 'LLMCache', 'TypeResolver',
    'SkillLoader', 'get_skill_loader',
    'ContextBuilder', 'ContextResult', 'EnumValueMapping',
    'CodePreprocessor', 'PreprocessingResult',
    'ErrorMemory', 'ErrorPattern', 'ErrorCategory', 'get_error_memory',
    'PatternLibrary', 'PatternCategory', 'get_pattern_library',
    # Exceptions
    'ACDecompileError',
    'ParsingError', 'HeaderParsingError', 'SourceParsingError',
    'MethodSignatureError', 'EncodingError', 'InvalidStructureError',
    'DatabaseError', 'DatabaseConnectionError', 'DatabaseQueryError',
    'DatabaseConstraintError', 'DatabaseMigrationError',
    'LLMError', 'LLMConnectionError', 'LLMResponseError',
    'LLMProcessingError', 'LLMVerificationError', 'LLMCacheError',
    'FileIOError', 'FileNotFoundError', 'FileWriteError',
    'FileEncodingError', 'PathError',
    'ConfigurationError', 'MissingConfigurationError',
    'InvalidConfigurationError', 'DependencyMissingError',
    'ProcessingError', 'DependencyResolutionError',
    'TypeResolutionError', 'CodeGenerationError', 'ValidationError'
]
