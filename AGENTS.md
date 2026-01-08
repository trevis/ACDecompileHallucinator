# ACDecompileHallucinator Agents & Architecture

This document describes the architecture and components of the ACDecompileHallucinator project, a system for parsing, analyzing, and browsing decompiled C++ type information.

## Project Overview

The solution is divided into three main projects:

1.  **ACDecompileParser.Shared**: The core library containing the domain model, database logic, and shared services.
2.  **ACDecompileParser**: A console application responsible for parsing raw C++ header files and populating the database.
3.  **ACTypeBrowser**: A Blazor Server web application for interactively browsing the parsed type hierarchy.

## 1. ACDecompileParser.Shared
*Location: `/ACDecompileParser.Shared`*

This library is the foundation of the system, consumed by both the Parser and the Browser.

### Core Domain Models (`Lib/Models`)
-   **Type Models**: `TypeModel`, `StructTypeModel`, `EnumTypeModel`, `UnionTypeModel`, `TypeDefModel`.
-   **Member Models**: `StructMemberModel`, `EnumMemberModel`, `FunctionParamModel`.
-   **Relationships**: `TypeInheritance`, `TypeReference` (handles pointers, const, etc.).

### Storage System (`Lib/Storage`)
-   **TypeContext**: EF Core DbContext for SQLite storage.
-   **TypeRepository**: Implements `ITypeRepository` to provide centralized data access (CRUD, lookups).

### Shared Services (`Lib/Services`)
-   **TypeResolutionService**:  Core engine for resolving type references, handling template arguments, and linking types across the database.
-   **TypeHierarchyService**: Manages the logical grouping of types (namespaces, file paths) for display and export.
-   **OffsetCalculationService**: specialized service for calculating memory offsets and object sizes based on architecture rules.

### Output Generation (`Lib/Output`)
-   **Code Generators**: `StructOutputGenerator`, `EnumOutputGenerator` implement `ICodeGenerator`.
-   **Code Tokens**: Generates structured `CodeToken` streams (tokens with semantic meaning like TypeName, Keyword) instead of raw strings, enabling rich UI rendering.

### Utilities (`Lib/Utilities`)
-   **ParsingUtilities**: Common text helpers.
-   **TypeParsingUtilities**: Advanced regex and string logic for breaking down complex C++ declarations.

---

## 2. ACDecompileParser
*Location: `/ACDecompileParser`*
*Type: Console Application*

The "Ingestion Engine". It scans decompiled files, parses the text, and writes to the shared database.

### Parsing System (`Lib/Parser`)
-   **SourceParser**: The orchestrator that manages the file scanning loop.
-   **TypeParser**: Determines if a block of text is a struct, enum, or typedef.
-   **StructParser / EnumParser**: specialized parsers for body content.
-   **MemberParser**: Extracts fields, methods, and parses the "Line Comments" (often containing offset info in decompiled code).
-   **FunctionParamParser**: Handles complex function pointer signatures.

### File Export (`Lib/Output`)
-   **FileOutputGenerator**: Uses the shared `TypeHierarchyService` to organize types into files and writes them to disk (generating the "clean" headers).
-   **TypeGroupProcessor**: Groups related types (like a struct and its vtable) to be written together.

---

## 3. ACTypeBrowser
*Location: `/ACTypeBrowser`*
*Type: Blazor Server Web App*

The "Visualization Layer". It connects to the SQLite database to provide a rich UI.

### Features
-   **Tree View**: Displays the hierarchy of types organized by Namespace/File (powered by `TypeHierarchyService`).
-   **Rich Type Detail**: Renders structs and enums with syntax highlighting and interactive links (clicking a member type navigates to that type).
-   **Search**: Real-time filtering of the type tree.

### Architecture
-   **Services**: Injects `ITypeRepository` and `TypeHierarchyService` from the Shared library.
-   **Rendering**: Uses the shared `ICodeGenerator`s to get token streams, which are then rendered as HTML components with click handlers for navigation.

---

## Cross-Cutting Systems

### Type Resolution Flow
1.  **Parse Phase** (ACDecompileParser): Raw strings (`"std::vector<int>*"`) are captured.
2.  **Resolution Phase** (Shared): `TypeResolutionService` runs to link these strings to actual `TypeModel` IDs in the DB, resolving typedefs and templates.

### Memory Layout Analysis
The `OffsetCalculationService` (Shared) is critical for verifying decompiled structs. It:
1.  Calculates the expected size/alignment of primitives.
2.  Simulates struct layout including padding and inheritance.
3.  Compares calculated offsets against "known" offsets (from decompiled comments) to identify discrepancies.

## Data Flow
```mermaid
graph TD
    Input[Decompiled Headers] -->|Read| Parser[ACDecompileParser]
    Parser -->|Parse & Resolve| DB[(One SQLite DB)]
    DB -->|Read| Browser[ACTypeBrowser]
    DB -->|Read| Exporter[FileOutputGenerator]
    Exporter -->|Write| Output[Clean Headers]
    
    subgraph Shared Library
    Models
    Services[TypeResolution / Hierarchy]
    end
    
    Parser -.-> Shared Library
    Browser -.-> Shared Library
```
