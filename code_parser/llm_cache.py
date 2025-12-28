import sqlite3
import logging
import threading
from pathlib import Path
from typing import Optional

logger = logging.getLogger("llm-cache")

class LLMCache:
    """Handles a separate SQLite database for caching LLM prompts and responses."""

    def __init__(self, db_path: Path):
        """Initialize the LLM cache database.

        Creates the parent directory if it doesn't exist and initializes
        the database schema. Thread-safe for parallel processing.

        Args:
            db_path: Path to the SQLite database file for caching.
        """
        self.db_path = Path(db_path)
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        self._lock = threading.Lock()
        self.init_db()
    
    def init_db(self):
        """Initialize the cache database with the required table structure.

        Creates the llm_cache table if it doesn't exist. The table stores
        prompts as primary keys with their corresponding responses and
        creation timestamps. Uses WAL mode for better concurrent access.
        """
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            # Enable WAL mode for better concurrent read/write performance
            cursor.execute('PRAGMA journal_mode=WAL')
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS llm_cache (
                    prompt TEXT PRIMARY KEY,
                    response TEXT NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')
            conn.commit()
    
    def get(self, prompt: str) -> Optional[str]:
        """Retrieve a cached response for the given prompt.

        Thread-safe for parallel processing.

        Args:
            prompt: The exact prompt string to look up in the cache.

        Returns:
            The cached response string if found, or None if no cache entry
            exists for the prompt or if a database error occurs.
        """
        with self._lock:
            try:
                with sqlite3.connect(self.db_path) as conn:
                    cursor = conn.cursor()
                    cursor.execute('SELECT response FROM llm_cache WHERE prompt = ?', (prompt,))
                    row = cursor.fetchone()
                    return row[0] if row else None
            except sqlite3.Error as e:
                logger.error(f"Cache read error: {e}")
                return None
            
    def set(self, prompt: str, response: str):
        """Store a prompt and its response in the cache.

        Thread-safe for parallel processing. Uses INSERT OR REPLACE to
        update existing entries if the same prompt is cached again.
        Empty responses are silently ignored.

        Args:
            prompt: The prompt string to use as the cache key.
            response: The LLM response to cache. If empty, the method
                returns without storing anything.
        """
        if not response:
            return

        with self._lock:
            try:
                with sqlite3.connect(self.db_path) as conn:
                    cursor = conn.cursor()
                    cursor.execute('''
                        INSERT OR REPLACE INTO llm_cache (prompt, response)
                        VALUES (?, ?)
                    ''', (prompt, response))
                    conn.commit()
            except sqlite3.Error as e:
                logger.error(f"Cache write error: {e}")
