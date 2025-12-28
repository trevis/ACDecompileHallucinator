# Database Schema (types.db)

## Tables

### types
| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER | Primary key |
| kind | TEXT | Type kind (struct, enum, typedef) |
| name | TEXT | Type name |
| code | TEXT | Raw decompiled code |
| namespace | TEXT | Namespace (e.g., "Turbine") |
| parent | TEXT | Base class name |
| is_template | INTEGER | 1 if template instantiation |

### methods
| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER | Primary key |
| name | TEXT | Method name |
| definition | TEXT | Full method body |
| parent | TEXT | Parent class name |
| offset | TEXT | Hex offset in binary |
| is_virtual | INTEGER | 1 if virtual method |

### processed_types
| Column | Type | Description |
|--------|------|-------------|
| name | TEXT | Type name (primary key) |
| processed_header | TEXT | Generated header code |
| processed_at | TEXT | ISO timestamp |
| engine_used | TEXT | LLM engine used |

### processed_methods
| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER | Primary key |
| parent | TEXT | Parent class |
| name | TEXT | Method name |
| processed_code | TEXT | Modernized code |
| confidence | TEXT | HIGH/MEDIUM/LOW |
| engine_used | TEXT | LLM engine used |

### constants
| Column | Type | Description |
|--------|------|-------------|
| name | TEXT | Constant name |
| value | TEXT | Value (hex or decimal) |
| type | TEXT | Type name |

## Common Queries

```python
# Get unprocessed types
db.get_unprocessed_types()

# Get methods for a class
db.get_methods_for_class("PlayerModule")

# Check if type is processed
db.is_type_processed("SomeClass")

# Store processed type
db.store_processed_type(name, header, engine="lm-studio")
```
