"""
Claude Code engine implementation with skills integration.

This engine uses the Claude Code CLI to generate responses,
enabling Claude's advanced reasoning for C++ code modernization.

The engine can operate in two modes:
1. Direct mode: Sends prompts directly to Claude CLI
2. Skills mode: Uses project skills for structured modernization

Skills are loaded from .claude/skills/ and provide structured
prompts for specific tasks like header generation and method modernization.

Key features:
- ContextBuilder integration for rich type/enum context
- Verification with retry loop for quality assurance
- Skill-based prompt construction
"""

import json
import logging
import subprocess
import shutil
import sys
from pathlib import Path
from typing import Optional, Any, Dict, List, TYPE_CHECKING

from .base import (
    LLMEngine,
    EngineConfig,
    EngineError,
    EngineConnectionError,
    EngineTimeoutError,
    EngineResponseError,
    VerificationResult,
)

if TYPE_CHECKING:
    from code_parser.context_builder import ContextBuilder, ContextResult
    from code_parser.error_memory import ErrorMemory

logger = logging.getLogger(__name__)

# Default Claude Code configuration
DEFAULT_TIMEOUT = 300  # 5 minutes for complex code generation
SKILLS_DIR = Path(".claude/skills")


class ClaudeCodeEngine(LLMEngine):
    """Engine using Claude Code CLI.

    This engine invokes the Claude Code CLI to generate responses,
    leveraging Claude's advanced reasoning capabilities for code
    modernization tasks.

    Requirements:
        - Claude Code CLI installed (npm install -g @anthropic/claude-code)
        - Valid API key configured

    Configuration (via config.extra):
        project_root: Working directory for Claude Code (default: .)
        cli_timeout: Subprocess timeout in seconds (default: 300)
        print_mode: Use --print flag for immediate output (default: True)

    Example:
        from engines import get_engine, EngineConfig

        config = EngineConfig(
            timeout=300,
            extra={"project_root": "/path/to/project"}
        )
        engine = get_engine("claude-code", config=config)
        response = engine.generate("Modernize this code...")

    Note:
        Unlike LM Studio, Claude Code uses a conversation-based interface.
        Each generate() call starts a new conversation with the prompt.
    """

    def __init__(self, config: Optional[EngineConfig] = None, cache: Optional[Any] = None):
        """Initialize the Claude Code engine.

        Args:
            config: Engine configuration.
            cache: Optional cache instance for response caching.

        Raises:
            EngineError: If Claude Code CLI is not found.
        """
        super().__init__(config, cache)

        # Extract Claude Code specific config
        project_root = self.config.extra.get("project_root", ".")
        self.project_root = str(Path(project_root).resolve())
        self.cli_timeout = self.config.extra.get("cli_timeout", DEFAULT_TIMEOUT)
        self.print_mode = self.config.extra.get("print_mode", True)

        # Check if Claude Code CLI is available
        # On Windows, also try common variants (.exe, .cmd)
        self.cli_path = shutil.which("claude")
        if not self.cli_path and sys.platform == "win32":
            # Try explicit extensions on Windows
            for variant in ["claude.exe", "claude.cmd"]:
                self.cli_path = shutil.which(variant)
                if self.cli_path:
                    break

        if not self.cli_path:
            logger.warning(
                "Claude Code CLI not found. Install with: npm install -g @anthropic/claude-code"
            )
            # Don't raise error - allow engine to be registered but not used
            self._initialized = False
        else:
            # Normalize path for consistent handling
            self.cli_path = str(Path(self.cli_path).resolve())
            self._initialized = True
            logger.info(f"Claude Code CLI found at: {self.cli_path}")

        # Load available skills
        self.skills: Dict[str, str] = {}
        self._load_skills()

        # Error memory for learning from failures (optional, set via setter)
        self._error_memory: Optional["ErrorMemory"] = None

    @property
    def name(self) -> str:
        """Engine identifier."""
        return "claude-code"

    def generate(self, prompt: str) -> str:
        """Generate a response using Claude Code CLI.

        Calls the Claude Code CLI with the given prompt and returns
        the generated response.

        Args:
            prompt: The prompt to send to Claude.

        Returns:
            The generated response text.

        Raises:
            EngineError: If Claude Code CLI is not available.
            EngineTimeoutError: If the request times out.
            EngineConnectionError: If the CLI fails to execute.
            EngineResponseError: If the response cannot be parsed.
        """
        if not self._initialized:
            raise EngineError(
                "Claude Code CLI not available. Install with: npm install -g @anthropic/claude-code",
                engine_name=self.name
            )

        # Check cache first
        if self.cache:
            cached = self.cache.get(prompt)
            if cached:
                logger.debug("Using cached response")
                return cached

        try:
            # Windows has a command line limit of ~8191 characters
            # For long prompts, we pipe via stdin instead of command-line args
            MAX_CMDLINE_PROMPT = 6000  # Leave room for command and other args

            # Verify CLI path exists
            if not Path(self.cli_path).exists():
                raise EngineConnectionError(
                    f"Claude Code CLI path does not exist: {self.cli_path}",
                    engine_name=self.name
                )

            # Verify CWD exists
            cwd = self.project_root if Path(self.project_root).exists() else None

            # On Windows, npm-installed CLIs may need shell=True
            # Also handle .cmd/.bat scripts that Windows uses for npm binaries
            use_shell = sys.platform == "win32" and (
                self.cli_path.lower().endswith(('.cmd', '.bat')) or
                not self.cli_path.lower().endswith('.exe')
            )

            # Build command - use stdin for long prompts to avoid Windows limit
            if len(prompt) > MAX_CMDLINE_PROMPT:
                # Pipe prompt via stdin
                cmd = [self.cli_path, "-p"]
                if self.print_mode:
                    cmd.append("--print")
                logger.debug(f"Executing (stdin): {' '.join(cmd)}... ({len(prompt)} chars)")

                # Execute with stdin piping - use UTF-8 for Unicode support
                result = subprocess.run(
                    cmd,
                    input=prompt,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    cwd=cwd,
                    timeout=self.cli_timeout,
                    shell=use_shell,
                )
            else:
                # Short prompt - pass as argument
                cmd = [self.cli_path, "-p", prompt]
                if self.print_mode:
                    cmd.append("--print")
                logger.debug(f"Executing: {' '.join(cmd[:3])}...")

                # Execute Claude Code CLI - use UTF-8 for Unicode support
                result = subprocess.run(
                    cmd,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    cwd=cwd,
                    timeout=self.cli_timeout,
                    shell=use_shell,
                )

            if result.returncode != 0:
                error_msg = result.stderr.strip() if result.stderr else "Unknown error"
                raise EngineResponseError(
                    f"Claude Code CLI returned error: {error_msg}",
                    engine_name=self.name
                )

            response = result.stdout.strip()
            if not response:
                raise EngineResponseError(
                    "Claude Code returned empty response",
                    engine_name=self.name
                )

            # Extract code block if present
            response = self._extract_code_block(response)

            # Cache the response
            if self.cache and response:
                self.cache.set(prompt, response)

            logger.debug(f"Generated {len(response)} characters")
            return response

        except subprocess.TimeoutExpired:
            raise EngineTimeoutError(
                f"Claude Code timed out after {self.cli_timeout}s",
                engine_name=self.name
            )
        except FileNotFoundError as e:
            logger.error(f"FileNotFoundError executing CLI at {self.cli_path}: {e}")
            raise EngineConnectionError(
                f"Claude Code CLI not found at {self.cli_path}",
                engine_name=self.name
            )
        except subprocess.SubprocessError as e:
            raise EngineError(
                f"Failed to execute Claude Code: {e}",
                engine_name=self.name,
                cause=e
            )

    def _extract_code_block(self, response: str) -> str:
        """Extract code from markdown code blocks if present.

        Claude Code often wraps responses in markdown code blocks.
        This method extracts the content from within the blocks.

        Args:
            response: The raw response from Claude Code.

        Returns:
            The extracted code or original response if no blocks found.
        """
        import re

        # Try to find C++ code blocks first
        cpp_pattern = r'```(?:cpp|c\+\+|c)\n(.*?)```'
        matches = re.findall(cpp_pattern, response, re.DOTALL)
        if matches:
            return '\n\n'.join(matches)

        # Try generic code blocks
        generic_pattern = r'```\n?(.*?)```'
        matches = re.findall(generic_pattern, response, re.DOTALL)
        if matches:
            return '\n\n'.join(matches)

        # No code blocks found, return as-is
        return response

    def _load_skills(self) -> None:
        """Load skill prompts from the skills directory.

        Skills are markdown files in .claude/skills/{skill-name}/SKILL.md
        that provide structured instructions for specific tasks.
        """
        skills_path = Path(self.project_root) / SKILLS_DIR
        if not skills_path.exists():
            logger.debug(f"Skills directory not found: {skills_path}")
            return

        for skill_dir in skills_path.iterdir():
            if skill_dir.is_dir():
                skill_file = skill_dir / "SKILL.md"
                if skill_file.exists():
                    try:
                        content = skill_file.read_text(encoding="utf-8")
                        self.skills[skill_dir.name] = content
                        logger.debug(f"Loaded skill: {skill_dir.name}")
                    except Exception as e:
                        logger.warning(f"Failed to load skill {skill_dir.name}: {e}")

        logger.info(f"Loaded {len(self.skills)} skills: {list(self.skills.keys())}")

    def _build_skill_prompt(
        self,
        skill_name: str,
        context: Dict[str, Any]
    ) -> str:
        """Build a prompt by combining skill instructions with context.

        Args:
            skill_name: The name of the skill to use.
            context: Dictionary with context data (code, class_name, etc.)

        Returns:
            The combined prompt ready for Claude.

        Raises:
            ValueError: If the skill is not loaded.
        """
        if skill_name not in self.skills:
            raise ValueError(f"Skill not loaded: {skill_name}. Available: {list(self.skills.keys())}")

        skill_content = self.skills[skill_name]

        # Build context section
        context_parts = []
        for key, value in context.items():
            if isinstance(value, str):
                context_parts.append(f"## {key.replace('_', ' ').title()}\n```cpp\n{value}\n```")
            elif isinstance(value, list):
                context_parts.append(f"## {key.replace('_', ' ').title()}\n" + "\n".join(f"- {v}" for v in value))
            else:
                context_parts.append(f"## {key.replace('_', ' ').title()}\n{value}")

        context_section = "\n\n".join(context_parts)

        return f"{skill_content}\n\n# Context\n\n{context_section}"

    def modernize_class(
        self,
        class_name: str,
        raw_code: str,
        dependencies: Optional[List[str]] = None
    ) -> str:
        """Generate a modern C++ header for a decompiled class.

        Uses the 'modernize-class' skill to transform raw decompiled
        code into clean, modern C++ with proper formatting.

        Args:
            class_name: The name of the class to modernize.
            raw_code: The raw decompiled code.
            dependencies: Optional list of dependency class names.

        Returns:
            The modernized C++ header code.
        """
        context = {
            "class_name": class_name,
            "raw_code": raw_code,
        }
        if dependencies:
            context["dependencies"] = dependencies

        prompt = self._build_skill_prompt("modernize-class", context)
        return self.generate(prompt)

    def modernize_method(
        self,
        method_name: str,
        parent_class: str,
        raw_code: str,
        class_header: Optional[str] = None
    ) -> str:
        """Modernize a decompiled C++ method.

        Uses the 'modernize-method' skill to transform raw decompiled
        functions into clean, idiomatic C++17+.

        Args:
            method_name: The name of the method.
            parent_class: The parent class name.
            raw_code: The raw decompiled method code.
            class_header: Optional processed header for context.

        Returns:
            The modernized method implementation.
        """
        context = {
            "method_name": method_name,
            "parent_class": parent_class,
            "raw_code": raw_code,
        }
        if class_header:
            context["class_header"] = class_header

        prompt = self._build_skill_prompt("modernize-method", context)
        return self.generate(prompt)

    def analyze_dependencies(
        self,
        class_name: str,
        raw_code: str
    ) -> Dict[str, Any]:
        """Analyze class dependencies from decompiled code.

        Uses the 'analyze-deps' skill to identify dependencies
        and generate a dependency graph.

        Args:
            class_name: The name of the class to analyze.
            raw_code: The raw decompiled code.

        Returns:
            Dictionary with dependency analysis results.
        """
        context = {
            "class_name": class_name,
            "raw_code": raw_code,
        }

        prompt = self._build_skill_prompt("analyze-deps", context)
        response = self.generate(prompt)

        # Try to parse as JSON
        try:
            return json.loads(response)
        except json.JSONDecodeError:
            # Return as structured dict if not JSON
            return {"raw_analysis": response}

    def verify_logic(
        self,
        original_code: str,
        modernized_code: str,
        context: Optional[str] = None
    ) -> VerificationResult:
        """Verify that modernized code preserves original logic.

        Uses the 'verify-logic' skill to compare original and
        modernized code for semantic equivalence.

        Args:
            original_code: The original decompiled code.
            modernized_code: The modernized code to verify.
            context: Optional additional context.

        Returns:
            VerificationResult with equivalence status and details.
        """
        skill_context = {
            "original_code": original_code,
            "modernized_code": modernized_code,
        }
        if context:
            skill_context["additional_context"] = context

        prompt = self._build_skill_prompt("verify-logic", skill_context)
        response = self.generate(prompt)

        # Parse verification response
        try:
            result = json.loads(response)
            return VerificationResult(
                is_equivalent=result.get("equivalent", False),
                confidence=result.get("confidence", "LOW"),
                issues=result.get("issues", []),
                details=result.get("analysis", response)
            )
        except json.JSONDecodeError:
            # Fallback: check for keywords
            is_equiv = "equivalent" in response.lower() and "not equivalent" not in response.lower()
            return VerificationResult(
                is_equivalent=is_equiv,
                confidence="LOW",
                issues=[],
                details=response
            )

    def is_available(self) -> bool:
        """Check if Claude Code CLI is available and configured.

        Returns:
            True if Claude Code can be used, False otherwise.
        """
        if not self._initialized:
            return False

        try:
            # Try to get version
            result = subprocess.run(
                [self.cli_path, "--version"],
                capture_output=True,
                text=True,
                timeout=5,
            )
            return result.returncode == 0
        except Exception:
            return False

    def __repr__(self) -> str:
        """String representation."""
        status = "available" if self.is_available() else "not available"
        return f"ClaudeCodeEngine({status})"

    # ═══════════════════════════════════════════════════════════════════════════
    # ContextBuilder Integration
    # ═══════════════════════════════════════════════════════════════════════════

    def set_context_builder(self, context_builder: 'ContextBuilder') -> None:
        """Set the context builder for enhanced context gathering.

        When a ContextBuilder is set, modernize_method_with_context()
        can automatically gather type references, enum mappings, and
        parent class context.

        Args:
            context_builder: The ContextBuilder instance to use.
        """
        self._context_builder = context_builder
        logger.info("ContextBuilder attached to ClaudeCodeEngine")

    def set_error_memory(self, error_memory: "ErrorMemory") -> None:
        """Set the error memory for learning from failures.

        When an ErrorMemory is set, modernize_method_with_verification()
        will:
        1. Check for similar error patterns before generation
        2. Record failures with context for future reference
        3. Record successful retries as correct solutions

        Args:
            error_memory: The ErrorMemory instance to use.
        """
        self._error_memory = error_memory
        logger.info("ErrorMemory attached to ClaudeCodeEngine")

    def modernize_method_with_context(
        self,
        method_name: str,
        parent_class: str,
        raw_code: str,
        namespace: Optional[str] = None,
        context: Optional['ContextResult'] = None,
        extra_context: Optional[str] = None,
    ) -> str:
        """Modernize a method with rich context from ContextBuilder.

        This enhanced version uses ContextBuilder to gather comprehensive
        context including type references, enum mappings, and parent
        class definitions.

        Args:
            method_name: The name of the method.
            parent_class: The parent class name.
            raw_code: The raw decompiled method code.
            namespace: Optional namespace for the class.
            context: Pre-gathered context, or None to gather automatically.
            extra_context: Optional additional context (e.g., error warnings).

        Returns:
            The modernized method implementation.

        Raises:
            ValueError: If no ContextBuilder is set and context is None.
        """
        # Use provided context or gather it
        if context is None:
            if not hasattr(self, '_context_builder') or self._context_builder is None:
                # Fall back to basic modernize_method
                logger.warning(
                    "No ContextBuilder set, falling back to basic modernize_method"
                )
                return self.modernize_method(method_name, parent_class, raw_code)

            context = self._context_builder.gather_method_context(
                code=raw_code,
                parent_class=parent_class,
                namespace=namespace,
            )

        # Build enhanced prompt with all context
        prompt_parts = []

        # Add skill instructions
        if "modernize-method" in self.skills:
            prompt_parts.append(self.skills["modernize-method"])

        prompt_parts.append("# Context\n")

        # Add error memory warnings if available (EARLY in prompt for emphasis)
        if extra_context:
            prompt_parts.append(extra_context)
            prompt_parts.append("")

        # Add parent class context
        if context.parent_header:
            status = "modernized" if context.parent_is_processed else "raw"
            prompt_parts.append(
                f"## Parent Class ({status})\n"
                f"This method belongs to class: {parent_class}\n\n"
                f"```cpp\n{context.parent_header}\n```\n"
            )

        # Add referenced types
        if context.type_context_str:
            prompt_parts.append(
                f"## Referenced Types\n"
                f"```cpp\n{context.type_context_str}\n```\n"
            )

        # Add enum value reference
        if context.enum_context_str:
            prompt_parts.append(
                f"## {context.enum_context_str}\n\n"
                "IMPORTANT: Replace ALL numeric literals that appear in the "
                "enum reference above with their corresponding enum constants.\n"
            )

        # Use preprocessed code if available, otherwise fall back to raw_code
        # The preprocessed code has:
        # - Decompiler types replaced (BOOL -> bool, __int64 -> int64_t, etc.)
        # - Calling conventions removed (__thiscall, __cdecl, etc.)
        # - Magic numbers annotated with potential enum values
        code_to_modernize = context.preprocessed_code if context.preprocessed_code else raw_code

        # Add preprocessing summary if transformations were applied
        if context.preprocessing_summary:
            prompt_parts.append(
                f"## Preprocessing Applied\n"
                f"The following automatic transformations have been applied: {context.preprocessing_summary}\n"
            )

        # Add the code to modernize (preprocessed or raw)
        prompt_parts.append(
            f"## Code to Modernize\n"
            f"```cpp\n{code_to_modernize}\n```\n"
        )

        prompt = "\n".join(prompt_parts)
        return self.generate(prompt)

    def modernize_method_with_verification(
        self,
        method_name: str,
        parent_class: str,
        raw_code: str,
        namespace: Optional[str] = None,
        context: Optional['ContextResult'] = None,
        max_retries: int = 5,
    ) -> tuple[str, VerificationResult]:
        """Modernize a method with verification and retry loop.

        This method implements the verification pattern from the legacy
        FunctionProcessor, where the modernized code is verified for
        logic equivalence and regenerated if verification fails.

        If ErrorMemory is configured, this method will:
        1. Check for similar error patterns before first generation
        2. Record failures with context for future reference
        3. Record successful retries as correct solutions

        Args:
            method_name: The name of the method.
            parent_class: The parent class name.
            raw_code: The raw decompiled method code.
            namespace: Optional namespace for the class.
            context: Pre-gathered context, or None to gather automatically.
            max_retries: Maximum number of regeneration attempts.

        Returns:
            Tuple of (modernized_code, final_verification_result).
        """
        # Check error memory for relevant warnings
        error_warnings = ""
        if self._error_memory:
            error_warnings = self._error_memory.get_warnings_for_code(raw_code)
            if error_warnings:
                logger.info(
                    f"Found {error_warnings.count('Issue')} relevant error warnings "
                    f"for {parent_class}::{method_name}"
                )

        # Initial generation (with warnings if available)
        modernized = self.modernize_method_with_context(
            method_name=method_name,
            parent_class=parent_class,
            raw_code=raw_code,
            namespace=namespace,
            context=context,
            extra_context=error_warnings if error_warnings else None,
        )

        # Verification loop
        retry_count = 0
        verification = None
        last_failed_output = None

        while retry_count < max_retries:
            verification = self.verify_logic(raw_code, modernized)

            if verification.is_equivalent:
                if retry_count == 0:
                    logger.info(
                        f"Method {parent_class}::{method_name} verified on first attempt"
                    )
                else:
                    logger.info(
                        f"Method {parent_class}::{method_name} verified after "
                        f"{retry_count} retries"
                    )
                    # Record successful retry solution to error memory
                    if self._error_memory and last_failed_output:
                        self._error_memory.record_success_after_retry(
                            raw_code, modernized
                        )
                return modernized, verification

            # Verification failed - record to error memory
            last_failed_output = modernized
            if self._error_memory:
                category = self._error_memory.categorize_error(verification.details)
                self._error_memory.record_failure(
                    category=category,
                    original_code=raw_code,
                    failed_output=modernized,
                    error_description=verification.details,
                    method_name=method_name,
                    class_name=parent_class,
                )

            logger.warning(
                f"Method {parent_class}::{method_name} verification failed "
                f"(attempt {retry_count + 1}/{max_retries}): {verification.details}"
            )

            feedback_prompt = self._build_feedback_prompt(
                raw_code=raw_code,
                modernized_code=modernized,
                verification_feedback=verification.details,
            )

            modernized = self.generate(feedback_prompt)
            modernized = self._extract_code_block(modernized)
            retry_count += 1

        # Exhausted retries
        logger.error(
            f"Method {parent_class}::{method_name} failed verification after "
            f"{max_retries} attempts"
        )

        # Return with failure marker
        final_code = (
            f"// VERIFICATION FAILED after {max_retries} attempts: "
            f"{verification.details if verification else 'Unknown'}\n"
            f"{modernized}"
        )

        return final_code, verification or VerificationResult(
            is_equivalent=False,
            confidence="LOW",
            issues=["Exhausted retries"],
            details="Maximum retry count exceeded",
        )

    def _build_feedback_prompt(
        self,
        raw_code: str,
        modernized_code: str,
        verification_feedback: str,
    ) -> str:
        """Build a feedback prompt for regeneration after verification failure.

        Args:
            raw_code: The original decompiled code.
            modernized_code: The attempted modernization.
            verification_feedback: The feedback from verification.

        Returns:
            Prompt for regenerating the code.
        """
        return f"""Original function:
```cpp
{raw_code}
```

Attempted modernization:
```cpp
{modernized_code}
```

Verification feedback: {verification_feedback}

Please regenerate the function addressing the issues mentioned in the verification feedback.
Ensure that the logic remains identical while improving the code style and structure where possible.

Output ONLY the corrected function code, no explanations."""
