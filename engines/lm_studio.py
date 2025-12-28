"""
LM Studio engine implementation.

This engine uses the OpenAI-compatible API provided by LM Studio
to generate responses from a locally-running LLM.
"""

import logging
import re
from typing import Optional, Any

from .base import (
    LLMEngine,
    EngineConfig,
    EngineError,
    EngineConnectionError,
    EngineTimeoutError,
    EngineResponseError,
    VerificationResult,
)

logger = logging.getLogger(__name__)

# Default LM Studio configuration
DEFAULT_BASE_URL = "http://localhost:1234/v1"
DEFAULT_MODEL = "local-model"
DEFAULT_API_KEY = "lm-studio"


class LMStudioEngine(LLMEngine):
    """Engine using local LM Studio API.

    LM Studio provides an OpenAI-compatible API for running local LLMs.
    This engine wraps that API with caching and error handling.

    Configuration (via config.extra):
        base_url: LM Studio API URL (default: http://localhost:1234/v1)
        model: Model identifier (default: local-model)
        api_key: API key (default: lm-studio)

    Example:
        from engines import get_engine, EngineConfig

        config = EngineConfig(
            temperature=0.2,
            extra={"base_url": "http://localhost:1234/v1"}
        )
        engine = get_engine("lm-studio", config=config)
        response = engine.generate("Modernize this code...")
    """

    def __init__(self, config: Optional[EngineConfig] = None, cache: Optional[Any] = None):
        """Initialize the LM Studio engine.

        Args:
            config: Engine configuration.
            cache: Optional LLMCache instance for response caching.

        Raises:
            EngineError: If the OpenAI library is not installed.
        """
        super().__init__(config, cache)

        # Extract LM Studio specific config
        self.base_url = self.config.extra.get("base_url", DEFAULT_BASE_URL)
        self.model = self.config.extra.get("model", DEFAULT_MODEL)
        self.api_key = self.config.extra.get("api_key", DEFAULT_API_KEY)

        # Initialize OpenAI client
        try:
            from openai import OpenAI
            self.client = OpenAI(base_url=self.base_url, api_key=self.api_key)
            self._initialized = True
        except ImportError:
            raise EngineError(
                "OpenAI library required. Install with: pip install openai",
                engine_name=self.name
            )

    @property
    def name(self) -> str:
        """Engine identifier."""
        return "lm-studio"

    def generate(self, prompt: str) -> str:
        """Generate a response from the local LLM.

        Checks cache first, then calls the LM Studio API if needed.

        Args:
            prompt: The prompt to send to the LLM.

        Returns:
            The generated response text.

        Raises:
            EngineConnectionError: If LM Studio is not running.
            EngineTimeoutError: If the request times out.
            EngineResponseError: If the response is empty or invalid.
        """
        # Check cache first
        if self.cache:
            cached = self.cache.get(prompt)
            if cached:
                logger.info("Cache hit! Using stored response.")
                return cached

        try:
            response = self.client.chat.completions.create(
                model=self.model,
                messages=[
                    {"role": "system", "content": self.config.system_message},
                    {"role": "user", "content": prompt}
                ],
                temperature=self.config.temperature,
                max_tokens=self.config.max_tokens,
                timeout=self.config.timeout
            )

            result = response.choices[0].message.content or ""

            # Log token usage if available
            if hasattr(response, "usage") and response.usage:
                u = response.usage
                logger.debug(
                    f"Tokens: {u.prompt_tokens} prompt + {u.completion_tokens} completion = {u.total_tokens} total"
                )

            # Validate response
            if not result.strip():
                raise EngineResponseError(
                    "LLM returned empty response",
                    engine_name=self.name
                )

            # Store in cache
            if self.cache:
                self.cache.set(prompt, result)

            return result

        except ImportError:
            raise EngineError(
                "OpenAI library required. Install with: pip install openai",
                engine_name=self.name
            )
        except Exception as e:
            error_str = str(e).lower()

            # Categorize the error
            if "timeout" in error_str:
                raise EngineTimeoutError(
                    f"Request timed out after {self.config.timeout}s",
                    engine_name=self.name,
                    cause=e
                )
            elif "connection" in error_str or "refused" in error_str:
                raise EngineConnectionError(
                    f"Cannot connect to LM Studio at {self.base_url}. Is it running?",
                    engine_name=self.name,
                    cause=e
                )
            else:
                raise EngineError(
                    f"LLM request failed: {e}",
                    engine_name=self.name,
                    cause=e
                )

    def is_available(self) -> bool:
        """Check if LM Studio is running and accessible.

        Returns:
            True if LM Studio responds to requests, False otherwise.
        """
        try:
            # Try to list models as a health check
            self.client.models.list()
            return True
        except Exception:
            return False

    def __call__(self, prompt: str) -> str:
        """Allow using engine as callable for backwards compatibility."""
        return self.generate(prompt)


def clean_llm_output(text: str) -> str:
    """Remove common LLM output artifacts.

    This is a utility function that can be used by any engine
    to clean up markdown formatting and code blocks.

    Args:
        text: Raw LLM output text.

    Returns:
        Cleaned text with markdown artifacts removed.
    """
    if not text:
        return ""

    # Remove markdown code blocks
    text = re.sub(r'^```(?:cpp|c\+\+|c|\w+)?\s*\n?', '', text, flags=re.M | re.I)
    text = re.sub(r'\n?```$', '', text, flags=re.M)

    # Remove markdown formatting
    text = re.sub(r'\*\*([^*]+)\*\*', r'\1', text)
    text = re.sub(r'__([^_]+)__', r'\1', text)

    return text.strip()
