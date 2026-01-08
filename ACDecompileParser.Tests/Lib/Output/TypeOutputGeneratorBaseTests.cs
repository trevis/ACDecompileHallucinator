using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace ACDecompileParser.Tests.Lib.Output;

public class TypeOutputGeneratorBaseTests
{
    [Fact]
    public void GetDependencies_UsesRelatedTypeBaseName_WhenAvailable()
    {
        // Arrange
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a base type
        var baseType = new TypeModel
        {
            BaseName = "BaseClass",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct BaseClass { int x; };"
        };
        
        // Create a derived type that inherits from the base type
        var derivedType = new TypeModel
        {
            BaseName = "DerivedClass",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct DerivedClass : BaseClass { int y; };"
        };
        
        // Insert types into the database first
        var baseTypeId = repository.InsertType(baseType);
        var derivedTypeId = repository.InsertType(derivedType);
        
        // Set up inheritance relationship with resolved RelatedType
        var inheritance = new TypeInheritance
        {
            RelatedTypeString = "BaseClass", // This is what gets stored in the database
            RelatedTypeId = baseTypeId, // This links to the base type in the database
            ParentTypeId = baseTypeId, // Link to the base type in the database
            DerivedTypeId = derivedTypeId  // Link to the derived type in the database
        };
        
        // Insert the inheritance relationship into the database
        context.TypeInheritances.Add(inheritance);
        context.SaveChanges();
        
        // Update the type in the database to include the inheritance relationship
        repository.UpdateType(derivedType);
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        Assert.NotNull(derivedType);
        var dependencies = outputGenerator.GetDependencies(derivedType);
        
        // Assert
        Assert.Contains("BaseClass.h", dependencies);
        Assert.Single(dependencies); // Should only have one dependency: BaseClass.h
    }
    
    [Fact]
    public void GetDependencies_UsesFallbackStringParsing_WhenRelatedTypeNotAvailable()
    {
        // Arrange
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a derived type with inheritance but no resolved RelatedType
        var derivedType = new TypeModel
        {
            BaseName = "DerivedClass",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct DerivedClass : SomeOtherClass { int y; };"
        };
        
        // Insert the derived type into the database first
        var derivedTypeId = repository.InsertType(derivedType);
        
        // Set up inheritance relationship without resolved RelatedType
        var inheritance = new TypeInheritance
        {
            RelatedTypeString = "SomeOtherClass",  // This is what gets stored in the database
            RelatedTypeId = null, // RelatedType is not resolved in the database
            ParentTypeId = 0, // No parent type in the database
            DerivedTypeId = derivedTypeId  // Link to the derived type in the database
        };
        
        // Insert the inheritance relationship into the database
        context.TypeInheritances.Add(inheritance);
        context.SaveChanges();
        
        // Update the type in the database to include the inheritance relationship
        repository.UpdateType(derivedType);
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Act
        Assert.NotNull(derivedType);
        var dependencies = outputGenerator.GetDependencies(derivedType);
        
        // Assert
        Assert.Contains("SomeOtherClass.h", dependencies);
        Assert.Single(dependencies); // Should only have one dependency: SomeOtherClass.h
    }
    
    [Fact]
    public void GetDependencies_UsesMemberTypeBaseName_WhenAvailable()
    {
        // Arrange
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ACDecompileParser.Shared.Lib.Storage.TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new ACDecompileParser.Shared.Lib.Storage.TypeContext(optionsBuilder.Options);
        using var repository = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);
        
        // Create a member type
        var memberType = new TypeModel
        {
            BaseName = "MemberType",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct MemberType { int x; };"
        };
        
        // Create a container type
        var containerType = new TypeModel
        {
            BaseName = "ContainerType",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct ContainerType { MemberType member; };"
        };
        
        // Insert types into the database
        var memberTypeId = repository.InsertType(memberType);
        repository.SaveChanges(); // Save to get the ID
        var containerTypeId = repository.InsertType(containerType);
        repository.SaveChanges(); // Save to get the ID
        
        // Create a struct member that references the member type
        var member = new StructMemberModel
        {
            Name = "member",
            TypeString = "MemberType",  // This is what gets stored in the database
            TypeReference = new TypeReference { Id = 1, TypeString = "MemberType" },    // This is the resolved relationship that should be used
            StructTypeId = containerTypeId,
            TypeReferenceId = 1 // This links to the member type in the database
        };
        
        repository.InsertStructMember(member);
        repository.SaveChanges(); // Save the struct member
        
        // Create an output generator
        var outputGenerator = new StructOutputGenerator(repository);
        
        // Get the container type from the repository to ensure members are loaded
        var containerTypeFromRepo = repository.GetTypeById(containerTypeId);
        Assert.NotNull(containerTypeFromRepo);
        
        // Act
        var dependencies = outputGenerator.GetDependencies(containerTypeFromRepo);
        
        // Assert
        Assert.Contains("MemberType.h", dependencies);
        Assert.Single(dependencies); // Should only have one dependency: MemberType.h
    }
}
