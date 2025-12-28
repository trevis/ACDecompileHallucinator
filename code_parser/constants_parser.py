"""
Constants Parser
===============
Parses constants and debug type definitions from acclient.txt
"""
import re
import logging
from typing import Dict, List, Optional
from pathlib import Path
from .db_handler import DatabaseHandler

logger = logging.getLogger(__name__)

class ConstantsParser:
    """Parses constants and type definitions from debug dump files"""
    
    def __init__(self, db_handler: DatabaseHandler):
        """Initialize the ConstantsParser with a database handler.

        Args:
            db_handler: DatabaseHandler instance for storing parsed constants
                and type definitions.
        """
        self.db = db_handler

        # Regex patterns
        # (0004E4) S_CONSTANT: Type:             0x107B, Value: 1, PTRUE
        self.constant_pattern = re.compile(
            r'\(.*\) S_CONSTANT:\s+Type:\s+(0x[0-9A-Fa-f]+),\s+Value:\s+(\d+),\s+(\w+)'
        )
        
        # (0004C0) S_LDATA32: [0003:0000D04C], Type:             0x1192, Vector3_DownVector__
        self.ldata_pattern = re.compile(
            r'\(.*\) S_LDATA32:\s+\[([0-9A-Fa-f:]+)\],\s+Type:\s+(0x[0-9A-Fa-f]+),\s+(\w+)'
        )
        
        # 0x107a : Length = 26, Leaf = 0x1009 LF_MFUNCTION
        self.type_def_pattern = re.compile(
            r'^(0x[0-9A-Fa-f]+)\s+:\s+Length\s+=\s+(\d+),\s+Leaf\s+=\s+(0x[0-9A-Fa-f]+)\s+(.*)'
        )

    def parse_file(self, file_path: str):
        """Parse a debug dump file and store results in the database.

        Parses constants (S_CONSTANT), local data (S_LDATA32), and type
        definitions from a debug dump file. Results are stored in the
        database via the db_handler.

        Args:
            file_path: Path to the debug dump file to parse.
        """
        path = Path(file_path)
        if not path.exists():
            logger.error(f"Constants file not found: {path}")
            return

        logger.info(f"Parsing constants from: {path}")
        
        with open(path, 'r', encoding='utf-8', errors='replace') as f:
            content = f.readlines()

        current_type_id = None
        current_type_data = []

        for line in content:
            line = line.strip()
            
            # Parse Constants
            const_match = self.constant_pattern.search(line)
            if const_match:
                type_id, value, name = const_match.groups()
                self.db.store_constant(name, value, type_id, is_ldata=False)
                continue

            # Parse LDATA32
            ldata_match = self.ldata_pattern.search(line)
            if ldata_match:
                addr, type_id, name = ldata_match.groups()
                self.db.store_constant(name, "0", type_id, is_ldata=True, address=addr)
                continue

            # Parse Type Definitions
            # If line starts with hex ID, it's a new type def
            type_match = self.type_def_pattern.match(line)
            if type_match:
                # Save previous type context if needed (not implementing full type reconstruction yet)
                current_type_id, length, leaf, desc = type_match.groups()
                self.db.store_debug_type(current_type_id, int(length), leaf, desc, line)
            elif current_type_id and line and not line.startswith('('):
                # Continuation of type definition
                # We can append this to the raw_data of the current type in DB if we want full fidelity
                # For now, we mainly care about capturing the ID
                pass
                
        logger.info("Constants parsing complete")
