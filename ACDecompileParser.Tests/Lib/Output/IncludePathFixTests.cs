using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace ACDecompileParser.Tests.Lib.Output;

public class IncludePathFixTests
{
    [Fact]
    public void GetDependencies_RemovesVTableSuffix_WhenTypeIsVTable()
    {
        // Arrange
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a base type
        var baseType = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct TestClass { int x; };"
        };
        
        // Create a vtable type that inherits from the base type
        var vtableType = new TypeModel
        {
            BaseName = "TestClass_vtbl",  // VTable name
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct TestClass_vtbl { void* vfunc; };"
        };
        
        // Insert types into the database first
        var baseTypeId = repository.InsertType(baseType);
        repository.SaveChanges(); // Save to get the ID
        var vtableTypeId = repository.InsertType(vtableType);
        repository.SaveChanges(); // Save to get the ID
        
        // Set up inheritance relationship with resolved RelatedType
        var inheritance = new TypeInheritance
        {
            RelatedTypeString = "TestClass",  // This is what gets stored in the database
            RelatedTypeId = baseTypeId, // This links to the base type in the database
            ParentTypeId = baseTypeId, // Link to the base type in the database
            DerivedTypeId = vtableTypeId  // Link to the vtable type in the database
        };
        
        // Insert the inheritance relationship into the database
        context.TypeInheritances.Add(inheritance);
        context.SaveChanges();
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        var dependencies = outputGenerator.GetDependencies(vtableType);
        
        // Assert - Should include TestClass.h, not TestClass_vtbl.h
        Assert.Contains("TestClass.h", dependencies);
        Assert.DoesNotContain("TestClass_vtbl.h", dependencies);
        Assert.Single(dependencies); // Should only have one dependency: TestClass.h
    }
    
    [Fact]
    public void GetDependencies_RemovesTemplateArguments_WhenTypeHasTemplates()
    {
        // Arrange
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a base type
        var baseType = new TypeModel
        {
            BaseName = "AutoGrowHashTable",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct AutoGrowHashTable { int x; };"
        };
        
        // Create a templated type that inherits from the base type
        var templatedType = new TypeModel
        {
            BaseName = "AutoGrowHashTable",  // Base name without template args
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct AutoGrowHashTable<AsyncContext,AsyncCache::CCallbackHandler *> { void* data; };",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 0, TypeString = "AsyncContext" },
                new TypeTemplateArgument { Position = 1, TypeString = "AsyncCache::CCallbackHandler *" }
            }
        };
        
        // Insert types into the database first
        var baseTypeId = repository.InsertType(baseType);
        repository.SaveChanges(); // Save to get the ID
        var templatedTypeId = repository.InsertType(templatedType);
        repository.SaveChanges(); // Save to get the ID
        
        // Set up inheritance relationship with resolved RelatedType
        var inheritance = new TypeInheritance
        {
            RelatedTypeString = "AutoGrowHashTable<AsyncContext,AsyncCache::CCallbackHandler *>",  // Template type string
            RelatedTypeId = baseTypeId, // This links to the base type in the database
            ParentTypeId = baseTypeId, // Link to the base type in the database
            DerivedTypeId = templatedTypeId  // Link to the templated type in the database
        };
        
        // Insert the inheritance relationship into the database
        context.TypeInheritances.Add(inheritance);
        context.SaveChanges();
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        var dependencies = outputGenerator.GetDependencies(templatedType);
        
        // Assert - Should include AutoGrowHashTable.h, not AutoGrowHashTable<...>.h
        Assert.Contains("AutoGrowHashTable.h", dependencies);
        Assert.DoesNotContain("AutoGrowHashTable<AsyncContext,AsyncCache::CCallbackHandler *.h", dependencies);
        Assert.Single(dependencies); // Should only have one dependency: AutoGrowHashTable.h
    }
    
    [Fact]
    public void GetDependencies_RemovesTemplateArgumentsForMemberTypes_WhenMemberHasTemplates()
    {
        // Arrange
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a member type with template arguments
        var memberType = new TypeModel
        {
            BaseName = "AutoGrowHashTable",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct AutoGrowHashTable { int x; };"
        };
        
        // Create a container type
        var containerType = new TypeModel
        {
            BaseName = "ContainerType",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct ContainerType { AutoGrowHashTable<int, float> member; };"
        };
        
        // Insert types into the database
        var memberTypeId = repository.InsertType(memberType);
        repository.SaveChanges(); // Save to get the ID
        var containerTypeId = repository.InsertType(containerType);
        repository.SaveChanges(); // Save to get the ID
        
        // Create a struct member that references the templated member type
        var member = new StructMemberModel
        {
            Name = "member",
            TypeString = "AutoGrowHashTable<int, float>",  // Template type string
            TypeReference = new TypeReference { Id = 1, TypeString = "AutoGrowHashTable<int, float>" },    // This is the resolved relationship that should be used
            StructTypeId = containerTypeId,
            TypeReferenceId = 1  // This links to the member type in the database
        };
        
        repository.InsertStructMember(member);
        repository.SaveChanges(); // Save the struct member
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        var dependencies = outputGenerator.GetDependencies(containerType);
        
        // Assert - Should include AutoGrowHashTable.h, not AutoGrowHashTable<...>.h
        Assert.Contains("AutoGrowHashTable.h", dependencies);
        Assert.DoesNotContain("AutoGrowHashTable<int, float>.h", dependencies);
        Assert.Single(dependencies); // Should only have one dependency: AutoGrowHashTable.h
    }
    
    [Fact]
    public void GetDependencies_HandlesVTableMemberTypes_WhenMemberIsVTable()
    {
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a vtable member type
        var vtableMemberType = new TypeModel
        {
            BaseName = "SomeClass_vtbl",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct SomeClass_vtbl { void* vfunc; };"
        };
        
        // Create a container type
        var containerType = new TypeModel
        {
            BaseName = "ContainerType",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct ContainerType { SomeClass_vtbl member; };"
        };
        
        // Insert types into the database
        var vtableMemberTypeId = repository.InsertType(vtableMemberType);
        repository.SaveChanges(); // Save to get the ID
        var containerTypeId = repository.InsertType(containerType);
        repository.SaveChanges(); // Save to get the ID
        
        // Create a struct member that references the vtable member type
        var member = new StructMemberModel
        {
            Name = "member",
            TypeString = "SomeClass_vtbl",  // VTable type string
            TypeReference = new TypeReference { Id = 1, TypeString = "SomeClass_vtbl" },    // This is the resolved relationship that should be used
            StructTypeId = containerTypeId,
            TypeReferenceId = 1 // This links to the member type in the database
        };
        
        repository.InsertStructMember(member);
        repository.SaveChanges(); // Save the struct member
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        var dependencies = outputGenerator.GetDependencies(containerType);
        
        // Assert - Should include SomeClass.h, not SomeClass_vtbl.h
        Assert.Contains("SomeClass.h", dependencies);
        Assert.DoesNotContain("SomeClass_vtbl.h", dependencies);
        Assert.Single(dependencies); // Should only have one dependency: SomeClass.h
    }
    
    [Fact]
    public void GetDependencies_ExcludesPrimitiveTypes_WhenMemberIsPrimitive()
    {
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a container type
        var containerType = new TypeModel
        {
            BaseName = "ContainerType",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct ContainerType { int member; };"
        };
        
        // Insert the container type into the database
        var containerTypeId = repository.InsertType(containerType);
        repository.SaveChanges(); // Save to get the ID
        
        // Create a struct member that references a primitive type
        var member = new StructMemberModel
        {
            Name = "member",
            TypeString = "int",  // Primitive type string
            TypeReference = new TypeReference { Id = 1, TypeString = "int" },    // No resolved relationship for primitive types
            StructTypeId = containerTypeId,
            TypeReferenceId = 1 // No type ID for primitive types
        };
        
        repository.InsertStructMember(member);
        repository.SaveChanges(); // Save the struct member
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        var dependencies = outputGenerator.GetDependencies(containerType);
        
        // Assert - Should not include primitive types in dependencies
        Assert.Empty(dependencies); // Should have no dependencies since int is a primitive
    }
    
    [Fact]
    public void GetDependencies_ExcludesComplexPrimitiveTypes_WhenMemberIsComplexPrimitive()
    {
        // Arrange
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a container type
        var containerType = new TypeModel
        {
            BaseName = "ContainerType",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct ContainerType { unsigned int member; };"
        };
        
        // Insert the container type into the database
        var containerTypeId = repository.InsertType(containerType);
        repository.SaveChanges(); // Save to get the ID
        
        // Create a struct member that references a complex primitive type
        var member = new StructMemberModel
        {
            Name = "member",
            TypeString = "unsigned int",  // Complex primitive type string
            TypeReference = new TypeReference { Id = 1, TypeString = "unsigned int" },    // No resolved relationship for primitive types
            StructTypeId = containerTypeId,
            TypeReferenceId = 1 // No type ID for primitive types
        };
        
        repository.InsertStructMember(member);
        repository.SaveChanges(); // Save the struct member
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        var dependencies = outputGenerator.GetDependencies(containerType);
        
        // Assert - Should not include complex primitive types in dependencies
        Assert.Empty(dependencies); // Should have no dependencies since unsigned int is a primitive
    }
    
    [Fact]
    public void GetDependencies_ExcludesPointerToPrimitiveTypes_WhenMemberIsPointerToPrimitive()
    {
        // Arrange
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a container type
        var containerType = new TypeModel
        {
            BaseName = "ContainerType",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct ContainerType { int* member; };"
        };
        
        // Insert the container type into the database
        var containerTypeId = repository.InsertType(containerType);
        repository.SaveChanges(); // Save to get the ID
        
        // Create a struct member that references a pointer to primitive type
        var member = new StructMemberModel
        {
            Name = "member",
            TypeString = "int*",  // Pointer to primitive type string
            TypeReference = new TypeReference { Id = 1, TypeString = "int*" },    // No resolved relationship for primitive types
            StructTypeId = containerTypeId,
            TypeReferenceId = 1 // No type ID for primitive types
        };
        
        repository.InsertStructMember(member);
        repository.SaveChanges(); // Save the struct member
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        var dependencies = outputGenerator.GetDependencies(containerType);
        
        // Assert - Should not include pointer to primitive types in dependencies
        Assert.Empty(dependencies); // Should have no dependencies since int* is a pointer to primitive
    }
}
