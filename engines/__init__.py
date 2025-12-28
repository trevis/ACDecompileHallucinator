"""
Engine abstraction layer for LLM backends.

This package provides a pluggable architecture for different LLM backends,
allowing the pipeline to use either LM Studio (local) or other providers.

Usage:
    from engines import get_engine, list_engines

    engine = get_engine("lm-studio", base_url="http://localhost:1234/v1")
    response = engine.generate(prompt)
"""

from .base import LLMEngine, VerificationResult, EngineError, EngineConfig
from .registry import register_engine, get_engine, list_engines

__all__ = [
    # Base classes
    "LLMEngine",
    "VerificationResult",
    "EngineError",
    "EngineConfig",
    # Registry functions
    "register_engine",
    "get_engine",
    "list_engines",
]
