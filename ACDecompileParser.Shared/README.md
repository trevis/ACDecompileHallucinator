# ACDecompileParser.Shared

This project contains shared components for the ACDecompileParser system, including data models and EF Core database infrastructure.

## Overview

The ACDecompileParser.Shared project provides:

- Data models for types, enums, structs, and their relationships
- EF Core database context and entity configurations
- Repository pattern implementation for data access
- Type reference resolution logic

## Architecture

### Models
- `TypeModel`: Represents parsed C++ types (structs, enums, classes, etc.)
- `EnumMemberModel`: Represents enum members with names and values
- `StructMemberModel`: Represents struct members with offsets and function pointer information
- `FunctionParamModel`: Represents function parameters for function pointers
- `TypeTemplateArgument`: Represents template parameters
- `TypeInheritance`: Represents inheritance relationships
- `TypeReference`: Represents type references with modifiers

### Storage
- `TypeContext`: EF Core DbContext with configured entities
- `TypeRepository`: Implementation of ITypeRepository interface
- `ITypeRepository`: Interface defining data access operations

## Usage

To use this shared library in another project:

1. Add a project reference to ACDecompileParser.Shared
2. Register the TypeContext with your DI container:

```csharp
services.AddDbContext<TypeContext>(options =>
    options.UseSqlite(connectionString)); // or your preferred database provider
services.AddScoped<ITypeRepository, TypeRepository>();
```

## Database Schema

The database schema is automatically managed by EF Core migrations. Key tables include:

- Types: Stores type definitions
- TypeReferences: Stores type references with modifiers
- EnumMembers: Stores enum member definitions
- StructMembers: Stores struct member definitions
- FunctionParameters: Stores function parameter definitions
- TypeTemplateArguments: Stores template argument relationships
- TypeInheritances: Stores inheritance relationships
