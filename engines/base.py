"""
Abstract base class for LLM engines.

This module defines the interface that all LLM backends must implement,
ensuring consistent behavior across different providers.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Optional, Dict, Any, Tuple
from pathlib import Path
import logging

logger = logging.getLogger(__name__)


class EngineError(Exception):
    """Base exception for engine-related errors."""

    def __init__(self, message: str, engine_name: str = "unknown", cause: Optional[Exception] = None):
        super().__init__(message)
        self.engine_name = engine_name
        self.cause = cause

    def __str__(self) -> str:
        base = f"[{self.engine_name}] {super().__str__()}"
        if self.cause:
            base += f" (caused by: {self.cause})"
        return base


class EngineConnectionError(EngineError):
    """Raised when the engine cannot connect to its backend."""
    pass


class EngineTimeoutError(EngineError):
    """Raised when an engine request times out."""
    pass


class EngineResponseError(EngineError):
    """Raised when the engine returns an invalid or empty response."""
    pass


@dataclass
class VerificationResult:
    """Result of logic verification between original and modernized code.

    Attributes:
        is_equivalent: Whether the modernized code preserves original logic.
        confidence: Confidence level of the verification ("high", "medium", "low").
        reason: Explanation of the verification result.
    """
    is_equivalent: bool
    confidence: str = "medium"  # "high", "medium", "low"
    reason: str = ""

    def __bool__(self) -> bool:
        """Allow using the result directly in boolean contexts."""
        return self.is_equivalent


@dataclass
class EngineConfig:
    """Configuration for LLM engines.

    Contains both shared settings and engine-specific options.

    Attributes:
        temperature: Sampling temperature (0.0-1.0). Lower = more deterministic.
        max_tokens: Maximum tokens to generate.
        timeout: Request timeout in seconds.
        cache_enabled: Whether to use response caching.
        system_message: System prompt for the LLM.
        extra: Engine-specific configuration options.
    """
    temperature: float = 0.2
    max_tokens: int = 131072
    timeout: int = 300
    cache_enabled: bool = True
    system_message: str = "You are a C++ modernization expert. Output ONLY clean code, no explanations."
    extra: Dict[str, Any] = field(default_factory=dict)


class LLMEngine(ABC):
    """Abstract base class for LLM backends.

    All engine implementations must inherit from this class and implement
    the abstract methods. The engine provides the core LLM interaction
    capabilities used by the code modernization pipeline.

    Example:
        class MyEngine(LLMEngine):
            @property
            def name(self) -> str:
                return "my-engine"

            def generate(self, prompt: str) -> str:
                # Implementation here
                return response
    """

    def __init__(self, config: Optional[EngineConfig] = None, cache: Optional[Any] = None):
        """Initialize the engine.

        Args:
            config: Engine configuration. Uses defaults if not provided.
            cache: Optional cache instance (typically LLMCache from code_parser).
        """
        self.config = config or EngineConfig()
        self.cache = cache
        self._initialized = False

    @property
    @abstractmethod
    def name(self) -> str:
        """Engine identifier for logging and state tracking.

        Returns:
            A unique string identifying this engine (e.g., "lm-studio").
        """
        pass

    @abstractmethod
    def generate(self, prompt: str) -> str:
        """Generate a response from the LLM.

        This is the core method that all engines must implement.
        It should handle caching internally if self.cache is available.

        Args:
            prompt: The prompt to send to the LLM.

        Returns:
            The generated response text.

        Raises:
            EngineError: If generation fails.
            EngineTimeoutError: If the request times out.
            EngineConnectionError: If the backend is unreachable.
        """
        pass

    def verify_logic(self, original: str, modernized: str) -> VerificationResult:
        """Verify equivalence between original and modernized code.

        Default implementation uses generate() with a verification prompt.
        Engines can override this for custom verification logic.

        Args:
            original: The original decompiled code.
            modernized: The modernized version of the code.

        Returns:
            VerificationResult indicating equivalence and confidence.
        """
        import json
        import re

        prompt = self._build_verification_prompt(original, modernized)
        response = self.generate(prompt)

        # Parse JSON response
        try:
            json_match = re.search(r'\{.*\}', response, re.DOTALL)
            if json_match:
                data = json.loads(json_match.group(0))
                return VerificationResult(
                    is_equivalent=data.get("equivalent", False),
                    confidence=data.get("confidence", "medium"),
                    reason=data.get("reason", "")
                )
        except json.JSONDecodeError:
            logger.warning(f"Failed to parse verification response: {response[:100]}...")

        return VerificationResult(
            is_equivalent=False,
            confidence="low",
            reason=f"Failed to parse verification response"
        )

    def _build_verification_prompt(self, original: str, modernized: str) -> str:
        """Build the verification prompt.

        This prompt is used to verify that modernized code preserves
        the original logic.
        """
        return f'''You are a senior code reviewer. Compare the ORIGINAL decompiled function with the MODERNIZED version.

ORIGINAL:
```cpp
{original}
```

MODERNIZED:
```cpp
{modernized}
```

TASK: Determine if the MODERNIZED version preserves core logic and semantics.

ALLOWED changes:
- Variable renaming for readability
- Type updates (int -> bool, int -> uint32_t)
- Structure changes (early returns, guard clauses)
- Safety checks (null checks)
- Removing decompiler artifacts (__thiscall, explicit this pointer)
- Using enum names instead of magic values

NOT ALLOWED:
- Changing arithmetic operations
- Changing control flow logic
- Adding or removing functionality
- Changing side effects

RESPONSE: Return ONLY a JSON object:
{{"equivalent": true/false, "confidence": "high/medium/low", "reason": "explanation if not equivalent"}}'''

    def is_available(self) -> bool:
        """Check if the engine backend is available.

        Returns:
            True if the engine can accept requests, False otherwise.
        """
        return True

    def cleanup(self) -> None:
        """Clean up any resources held by the engine.

        Called when the engine is no longer needed.
        """
        pass

    def __enter__(self) -> "LLMEngine":
        """Support context manager usage."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        """Clean up on context exit."""
        self.cleanup()

    def __repr__(self) -> str:
        return f"<{self.__class__.__name__}(name='{self.name}')>"
