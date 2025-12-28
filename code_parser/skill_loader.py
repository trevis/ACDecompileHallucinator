"""
Skill Loader - Extracts and formats skill instructions for LLM prompts.

This module reads skill definitions from .claude/skills/ and formats them
for injection into LLM prompts. This allows both Claude Code and LM Studio
engines to benefit from the same skill instructions.

Skills are markdown files with structured sections that define:
- Purpose and workflow
- Strict rules and constraints
- Examples with input/output pairs
- Critical instructions (like enum replacement)
"""

import re
from pathlib import Path
from typing import Optional, Dict, List
from dataclasses import dataclass


@dataclass
class SkillSection:
    """A parsed section from a skill file."""
    title: str
    content: str
    level: int  # Heading level (1-6)


@dataclass
class ParsedSkill:
    """A fully parsed skill with extracted sections."""
    name: str
    description: str
    rules: List[str]
    critical_sections: List[SkillSection]
    examples: List[Dict[str, str]]  # List of {input, output, context?}
    raw_content: str


class SkillLoader:
    """Loads and parses skill files for prompt injection.

    Skills are stored in .claude/skills/{skill-name}/SKILL.md format.
    This loader extracts key sections and formats them for LLM consumption.

    Attributes:
        skills_dir: Path to the .claude/skills directory.
        _cache: Dictionary cache of parsed skills.
    """

    def __init__(self, project_root: Optional[Path] = None):
        """Initialize the skill loader.

        Args:
            project_root: Root directory of the project. If None, attempts
                to find it by looking for .claude directory.
        """
        if project_root is None:
            # Try to find project root by looking for .claude directory
            current = Path.cwd()
            while current != current.parent:
                if (current / ".claude" / "skills").exists():
                    project_root = current
                    break
                current = current.parent
            else:
                project_root = Path.cwd()

        self.skills_dir = Path(project_root) / ".claude" / "skills"
        self._cache: Dict[str, ParsedSkill] = {}

    def list_skills(self) -> List[str]:
        """List all available skill names.

        Returns:
            List of skill directory names.
        """
        if not self.skills_dir.exists():
            return []

        return [
            d.name for d in self.skills_dir.iterdir()
            if d.is_dir() and (d / "SKILL.md").exists()
        ]

    def load_skill(self, skill_name: str) -> Optional[ParsedSkill]:
        """Load and parse a skill by name.

        Args:
            skill_name: Name of the skill directory (e.g., 'modernize-method').

        Returns:
            ParsedSkill object or None if skill not found.
        """
        if skill_name in self._cache:
            return self._cache[skill_name]

        skill_path = self.skills_dir / skill_name / "SKILL.md"
        if not skill_path.exists():
            return None

        content = skill_path.read_text(encoding='utf-8')
        parsed = self._parse_skill(skill_name, content)
        self._cache[skill_name] = parsed
        return parsed

    def _parse_skill(self, name: str, content: str) -> ParsedSkill:
        """Parse a skill markdown file into structured data.

        Args:
            name: Skill name.
            content: Raw markdown content.

        Returns:
            ParsedSkill with extracted sections.
        """
        sections = self._extract_sections(content)

        # Extract description (first paragraph or Purpose section)
        description = ""
        for section in sections:
            if section.title.lower() in ('purpose', 'description', 'overview'):
                description = section.content.strip()
                break
        if not description:
            # Use first paragraph
            first_para = content.split('\n\n')[0]
            if not first_para.startswith('#'):
                description = first_para.strip()

        # Extract rules
        rules = []
        for section in sections:
            if 'rule' in section.title.lower():
                # Parse bullet points
                for line in section.content.split('\n'):
                    line = line.strip()
                    if line.startswith('- ') or line.startswith('* '):
                        rules.append(line[2:].strip())

        # Find critical sections (marked with CRITICAL, IMPORTANT, etc.)
        critical_sections = []
        for section in sections:
            if any(word in section.title.upper() for word in ['CRITICAL', 'IMPORTANT', 'MUST']):
                critical_sections.append(section)
            elif any(word in section.content.upper()[:100] for word in ['CRITICAL', 'IMPORTANT', 'MUST']):
                critical_sections.append(section)

        # Extract examples
        examples = self._extract_examples(content)

        return ParsedSkill(
            name=name,
            description=description,
            rules=rules,
            critical_sections=critical_sections,
            examples=examples,
            raw_content=content
        )

    def _extract_sections(self, content: str) -> List[SkillSection]:
        """Extract markdown sections from content.

        Args:
            content: Raw markdown content.

        Returns:
            List of SkillSection objects.
        """
        sections = []
        current_title = ""
        current_level = 0
        current_content = []

        for line in content.split('\n'):
            heading_match = re.match(r'^(#{1,6})\s+(.+)$', line)
            if heading_match:
                # Save previous section
                if current_title:
                    sections.append(SkillSection(
                        title=current_title,
                        content='\n'.join(current_content).strip(),
                        level=current_level
                    ))

                current_level = len(heading_match.group(1))
                current_title = heading_match.group(2)
                current_content = []
            else:
                current_content.append(line)

        # Save last section
        if current_title:
            sections.append(SkillSection(
                title=current_title,
                content='\n'.join(current_content).strip(),
                level=current_level
            ))

        return sections

    def _extract_examples(self, content: str) -> List[Dict[str, str]]:
        """Extract input/output examples from content.

        Args:
            content: Raw markdown content.

        Returns:
            List of example dictionaries with 'input' and 'output' keys.
        """
        examples = []

        # Look for code blocks after "Input" and "Output" headers
        # Pattern: ### Input\n```cpp\n...\n```\n### Output\n```cpp\n...\n```
        example_pattern = re.compile(
            r'###?\s*Input\s*\n```(?:cpp)?\n(.*?)```\s*'
            r'(?:###?\s*(?:Context|Referenced).*?```(?:cpp)?\n(.*?)```\s*)?'
            r'###?\s*Output\s*\n```(?:cpp)?\n(.*?)```',
            re.DOTALL | re.IGNORECASE
        )

        for match in example_pattern.finditer(content):
            example = {
                'input': match.group(1).strip(),
                'output': match.group(3).strip()
            }
            if match.group(2):
                example['context'] = match.group(2).strip()
            examples.append(example)

        return examples

    def format_for_prompt(self, skill_name: str,
                          sections: Optional[List[str]] = None) -> str:
        """Format a skill's content for inclusion in an LLM prompt.

        Args:
            skill_name: Name of the skill to format.
            sections: Optional list of section names to include. If None,
                includes rules and critical sections.

        Returns:
            Formatted string ready for prompt injection.
        """
        skill = self.load_skill(skill_name)
        if not skill:
            return ""

        parts = []

        # Add description
        if skill.description:
            parts.append(f"# {skill_name.replace('-', ' ').title()}")
            parts.append(skill.description)
            parts.append("")

        # Add rules
        if skill.rules:
            parts.append("## Rules")
            for rule in skill.rules:
                parts.append(f"- {rule}")
            parts.append("")

        # Add critical sections
        for section in skill.critical_sections:
            parts.append(f"## {section.title}")
            parts.append(section.content)
            parts.append("")

        return '\n'.join(parts)

    def get_enum_instructions(self) -> str:
        """Get enum replacement instructions from the modernize-method skill.

        Returns:
            Formatted enum replacement instructions for prompt injection.
        """
        skill = self.load_skill('modernize-method')
        if not skill:
            return ""

        # Find the enum section
        for section in skill.critical_sections:
            if 'enum' in section.title.lower():
                return f"""## {section.title}

{section.content}
"""

        return ""

    def get_transformation_rules(self) -> str:
        """Get code transformation rules from relevant skills.

        Returns:
            Formatted transformation rules for prompt injection.
        """
        skill = self.load_skill('modernize-method')
        if not skill:
            return ""

        rules_text = []
        if skill.rules:
            rules_text.append("## Transformation Rules\n")
            for rule in skill.rules:
                rules_text.append(f"- {rule}")

        return '\n'.join(rules_text)


def get_skill_loader(project_root: Optional[Path] = None) -> SkillLoader:
    """Factory function to get a SkillLoader instance.

    Args:
        project_root: Optional project root path.

    Returns:
        Configured SkillLoader instance.
    """
    return SkillLoader(project_root)
