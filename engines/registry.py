"""
Engine registry for plugin discovery and instantiation.

Provides a central registry where engine classes are registered and
can be instantiated by name. Supports both built-in and custom engines.
"""

from typing import Dict, Type, Optional, Any, List
import logging

from .base import LLMEngine, EngineConfig, EngineError

logger = logging.getLogger(__name__)

# Global registry mapping engine names to their classes
_registry: Dict[str, Type[LLMEngine]] = {}


def register_engine(name: str, engine_class: Type[LLMEngine]) -> None:
    """Register an engine class by name.

    Args:
        name: Unique identifier for the engine (e.g., "lm-studio").
        engine_class: The engine class to register.

    Raises:
        ValueError: If an engine with this name is already registered.
        TypeError: If engine_class is not a subclass of LLMEngine.
    """
    if not isinstance(name, str) or not name:
        raise ValueError("Engine name must be a non-empty string")

    if not isinstance(engine_class, type) or not issubclass(engine_class, LLMEngine):
        raise TypeError(f"engine_class must be a subclass of LLMEngine, got {type(engine_class)}")

    if name in _registry:
        logger.warning(f"Overwriting existing engine registration: {name}")

    _registry[name] = engine_class
    logger.debug(f"Registered engine: {name} -> {engine_class.__name__}")


def unregister_engine(name: str) -> bool:
    """Remove an engine from the registry.

    Args:
        name: The engine name to unregister.

    Returns:
        True if the engine was removed, False if it wasn't registered.
    """
    if name in _registry:
        del _registry[name]
        logger.debug(f"Unregistered engine: {name}")
        return True
    return False


def get_engine(
    name: str,
    config: Optional[EngineConfig] = None,
    cache: Optional[Any] = None,
    **kwargs: Any
) -> LLMEngine:
    """Get an engine instance by name.

    Args:
        name: The registered engine name.
        config: Optional engine configuration.
        cache: Optional cache instance for response caching.
        **kwargs: Additional keyword arguments passed to the engine constructor.
            These are merged into config.extra if config is provided.

    Returns:
        An instance of the requested engine.

    Raises:
        ValueError: If no engine is registered with the given name.
        EngineError: If engine instantiation fails.
    """
    if name not in _registry:
        available = ", ".join(_registry.keys()) if _registry else "none"
        raise ValueError(f"Unknown engine: '{name}'. Available engines: {available}")

    engine_class = _registry[name]

    # Merge kwargs into config.extra
    if config and kwargs:
        config.extra.update(kwargs)
    elif kwargs and not config:
        config = EngineConfig(extra=kwargs)

    try:
        engine = engine_class(config=config, cache=cache)
        logger.info(f"Created engine instance: {name}")
        return engine
    except Exception as e:
        raise EngineError(
            f"Failed to instantiate engine '{name}': {e}",
            engine_name=name,
            cause=e
        )


def list_engines() -> List[str]:
    """List all registered engine names.

    Returns:
        A sorted list of registered engine names.
    """
    return sorted(_registry.keys())


def is_registered(name: str) -> bool:
    """Check if an engine is registered.

    Args:
        name: The engine name to check.

    Returns:
        True if the engine is registered, False otherwise.
    """
    return name in _registry


def get_engine_class(name: str) -> Optional[Type[LLMEngine]]:
    """Get the engine class without instantiating it.

    Args:
        name: The registered engine name.

    Returns:
        The engine class, or None if not registered.
    """
    return _registry.get(name)


def _auto_register() -> None:
    """Auto-register built-in engines.

    Called when the module is imported to register default engines.
    """
    try:
        from .lm_studio import LMStudioEngine
        register_engine("lm-studio", LMStudioEngine)
    except ImportError as e:
        logger.debug(f"LM Studio engine not available: {e}")

    # Claude Code engine
    try:
        from .claude_code import ClaudeCodeEngine
        register_engine("claude-code", ClaudeCodeEngine)
    except ImportError as e:
        logger.debug(f"Claude Code engine not available: {e}")


# Auto-register engines on module import
_auto_register()
