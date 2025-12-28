import logging
import re
from typing import List, Dict
from .struct import Struct
from .enum import Enum
from .method import Method
from .offset_mapper import OffsetMapper

logger = logging.getLogger(__name__)

class SourceParser:
    """Parses C++ source files and extracts globals / methods"""
    
    def __init__(self, source_file: str, offset_mapper: OffsetMapper):
        """Initialize the SourceParser with a source file and offset mapper.

        Args:
            source_file: Path to the C++ source file to parse.
            offset_mapper: OffsetMapper instance for resolving binary offsets
                to source file names.
        """
        self.source_file = source_file
        self.methods: Dict[str, Method] = {}
        self.offset_mapper: OffsetMapper = offset_mapper
        # Statistics counters
        self.stats = {
            'global_methods_found': 0,
            'global_methods_ignored': 0,
            'class_methods_found': 0,
            'class_methods_ignored': 0,
        }
    
    def read_file_safely(self) -> str:
        """Read the source file with encoding fallback.

        Attempts to read the file using multiple encodings in order:
        utf-8, latin-1, cp1252. Falls back to utf-8 with replacement
        characters if all encodings fail.

        Returns:
            The file contents as a decoded string.
        """
        with open(self.source_file, 'rb') as f:
            raw = f.read()
        for encoding in ['utf-8', 'latin-1', 'cp1252']:
            try:
                return raw.decode(encoding)
            except UnicodeDecodeError:
                continue
        return raw.decode('utf-8', errors='replace')
    
    def parse(self):
        """Parse the source file and extract method definitions.

        Reads the source file and extracts all function/method definitions.
        Each method is identified by its offset marker (//----- OFFSET).
        Results are stored in the methods dictionary and statistics are
        updated for both global and class methods.
        """
        content = self.read_file_safely()
        lines = content.splitlines()
        i = 0
        found_funcs = 0

        while i < len(lines):
            line = lines[i]
            
            # Skip blank lines
            if re.match(r"^\s*$", line):
                i += 1
                continue

            if line.startswith('//----- '):
                m = Method()
                (i, func_name, func) = m.parse(line, lines, i)
                if func:
                    m.file = self.offset_mapper.get_filename("0x" + func.offset)
                    self.methods[func.full_name] = func
                    found_funcs = found_funcs + 1
                    if func.is_global:
                        self.stats['global_methods_found'] = self.stats['global_methods_found'] + 1
                        if func.is_ignored:
                            self.stats['global_methods_ignored'] = self.stats['global_methods_ignored'] + 1

                    else:
                        self.stats['class_methods_found'] = self.stats['class_methods_found'] + 1
                        if func.is_ignored:
                            self.stats['class_methods_ignored'] = self.stats['class_methods_ignored'] + 1
            else:
                pass
            
            i += 1
        logger.info(f"Found {found_funcs} functions")

    def print_stats(self):
        """Print statistics about parsed methods to the logger.

        Logs counts of global and class methods found during parsing,
        including how many of each were ignored.
        """
        logger.info(f"Global Methods found: {self.stats['global_methods_found'] - self.stats['global_methods_ignored']} (ignored: {self.stats['global_methods_ignored']})")
        logger.info(f"Class Methods found: {self.stats['class_methods_found'] - self.stats['class_methods_ignored']} (ignored: {self.stats['class_methods_ignored']})")