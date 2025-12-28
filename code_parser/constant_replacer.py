"""
Constant Replacer
================
Replaces raw values in code with matching named constants from the database.
"""
import re
import logging
from typing import Dict, List, Tuple
from .db_handler import DatabaseHandler

logger = logging.getLogger(__name__)

class ConstantReplacer:
    """Handles replacement of values with matching constants."""

    def __init__(self, db_handler: DatabaseHandler):
        """Initialize the constant replacer with database access.

        Loads all constants from the database into memory for efficient
        lookup during code processing.

        Args:
            db_handler: DatabaseHandler instance for accessing the constants
                table in the types database.
        """
        self.db = db_handler
        self.constants_map = self._load_constants()
        
    def _load_constants(self) -> Dict[str, Dict]:
        """Load constants from the database and organize by name.

        Retrieves all constants from the database and creates a lookup
        dictionary keyed by constant name for efficient access during
        code processing.

        Returns:
            Dictionary mapping constant names to their full record
            dictionaries containing name, value, type_id, and is_ldata fields.
        """
        constants = self.db.get_constants()
        mapped = {}
        for const in constants:
            # We map by Name now, not Value
            mapped[const['name']] = const
        return mapped

    def process_line(self, line: str) -> str:
        """Process a single line of code and annotate known constants.

        Scans the line for C identifiers that match known constants and
        adds inline comments with type and value information. Empty lines
        and comment-only lines are returned unchanged.

        Args:
            line: A single line of C/C++ code to process.

        Returns:
            The line with constant annotations added as inline comments,
            e.g., "PTRUE /* Type: 0x107B, Value: 1 */".
        """
        if not line.strip() or line.strip().startswith('//'):
            return line
            
        def replace_match(match):
            name = match.group(0)
            if name in self.constants_map:
                const = self.constants_map[name]
                
                # Format: NAME /* Type: ..., Value: ... */
                # Example: PTRUE /* Type: 0x107B, Value: 1 */
                
                parts = []
                if const.get('type_id'):
                    parts.append(f"Type: {const['type_id']}")
                
                # Only show Value if it's not an LDATA (LDATA is address based)
                if not const.get('is_ldata') and const.get('value'):
                    parts.append(f"Value: {const['value']}")
                
                comment_content = ", ".join(parts)
                if comment_content:
                    return f"{name} /* {comment_content} */"
            
            return name

        # Match C identifiers: letter/underscore followed by alphanumeric/underscore
        pattern = re.compile(r'\b[a-zA-Z_][a-zA-Z0-9_]*\b')
        return pattern.sub(replace_match, line)

    def process_code(self, code: str) -> str:
        """Process a block of code and annotate all known constants.

        Splits the code into lines and processes each line individually,
        adding constant annotations where applicable.

        Args:
            code: A multi-line string of C/C++ code to process.

        Returns:
            The processed code with constant annotations added throughout.
        """
        lines = code.split('\n')
        new_lines = [self.process_line(line) for line in lines]
        return '\n'.join(new_lines)
