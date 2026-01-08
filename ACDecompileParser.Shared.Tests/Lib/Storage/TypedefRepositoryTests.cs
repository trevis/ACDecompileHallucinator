using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Storage;

public class TypedefRepositoryTests
{
    private DbContextOptions<TypeContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<TypeContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private TypeContext CreateContext()
    {
        return new TypeContext(CreateNewContextOptions());
    }

    [Fact]
    public void InsertTypeDef_ValidTypeDef_InsertsSuccessfully()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "flowqueueInterval_t",
            Namespace = string.Empty,
            Type = TypeType.Typedef,
            Source = "typedef unsigned __int16 flowqueueInterval_t;"
        };
        typeModel.StoredFullyQualifiedName = typeModel.FullyQualifiedName;

        var typeReference = new TypeReference
        {
            TypeString = "unsigned __int16",
            IsConst = false,
            IsPointer = false,
            IsReference = false,
            PointerDepth = 0
        };

        var typeDef = new TypeDefModel
        {
            Name = typeModel.BaseName,
            Namespace = typeModel.Namespace,
            TypeReference = typeReference,
            Source = "typedef unsigned __int16 flowqueueInterval_t;"
        };

        // Act
        repository.InsertType(typeModel);
        repository.SaveChanges();

        repository.InsertTypeReference(typeReference);
        repository.SaveChanges();

        typeDef.TypeReferenceId = typeReference.Id;

        repository.InsertTypeDef(typeDef);
        repository.SaveChanges();

        // Assert
        var savedTypeDef = repository.GetTypeDefById(typeDef.Id);
        Assert.NotNull(savedTypeDef);
        Assert.Equal("flowqueueInterval_t", savedTypeDef.Name);
        Assert.Equal("unsigned __int16", savedTypeDef.TypeReference?.TypeString);
    }

    [Fact]
    public void GetTypeDefByName_ExistingTypedef_ReturnsCorrectTypedef()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "RPC_IF_HANDLE",
            Namespace = string.Empty,
            Type = TypeType.Typedef,
            Source = "typedef void *RPC_IF_HANDLE;"
        };
        typeModel.StoredFullyQualifiedName = typeModel.FullyQualifiedName;

        var typeReference = new TypeReference
        {
            TypeString = "void*",
            IsConst = false,
            IsPointer = true,
            IsReference = false,
            PointerDepth = 1
        };

        var typeDef = new TypeDefModel
        {
            Name = typeModel.BaseName,
            Namespace = typeModel.Namespace,
            TypeReference = typeReference,
            Source = "typedef void *RPC_IF_HANDLE;"
        };

        repository.InsertType(typeModel);
        repository.SaveChanges();

        repository.InsertTypeReference(typeReference);
        repository.SaveChanges();

        typeDef.TypeReferenceId = typeReference.Id;

        repository.InsertTypeDef(typeDef);
        repository.SaveChanges();

        // Act
        var result = repository.GetTypeDefByName("RPC_IF_HANDLE");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("RPC_IF_HANDLE", result.Name);
        Assert.Equal("void*", result.TypeReference?.TypeString);
        Assert.True(result.TypeReference?.IsPointer);
        Assert.Equal(1, result.TypeReference?.PointerDepth);
    }

    [Fact]
    public void GetTypeDefByName_NonExistentTypedef_ReturnsNull()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        // Act
        var result = repository.GetTypeDefByName("NonExistentTypedef");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveTypeDefChain_ThreeLevelChain_ResolvesToBase()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        // Create base type (int)
        var intType = new TypeModel
        {
            BaseName = "int",
            Namespace = string.Empty,
            Type = TypeType.Primitive,
            Source = "int"
        };
        intType.StoredFullyQualifiedName = intType.FullyQualifiedName;
        repository.InsertType(intType);
        repository.SaveChanges();

        // Create typedef chain: A -> B -> C -> int
        // typedef int A;
        var typeA = new TypeModel
        {
            BaseName = "A",
            Namespace = string.Empty,
            Type = TypeType.Typedef,
            Source = "typedef int A;"
        };
        typeA.StoredFullyQualifiedName = typeA.FullyQualifiedName;

        var refA = new TypeReference
        {
            TypeString = "int",
            ReferencedTypeId = intType.Id
        };

        repository.InsertType(typeA);
        repository.SaveChanges();
        repository.InsertTypeReference(refA);
        repository.SaveChanges();

        var typeDefA = new TypeDefModel
        {
            Name = typeA.BaseName,
            Namespace = typeA.Namespace,
            TypeReferenceId = refA.Id,
            Source = "typedef int A;"
        };
        repository.InsertTypeDef(typeDefA);
        repository.SaveChanges();

        // typedef A B;
        var typeB = new TypeModel
        {
            BaseName = "B",
            Namespace = string.Empty,
            Type = TypeType.Typedef,
            Source = "typedef A B;"
        };
        typeB.StoredFullyQualifiedName = typeB.FullyQualifiedName;

        var refB = new TypeReference
        {
            TypeString = "A",
            ReferencedTypeId = typeA.Id
        };

        repository.InsertType(typeB);
        repository.SaveChanges();
        repository.InsertTypeReference(refB);
        repository.SaveChanges();

        var typeDefB = new TypeDefModel
        {
            Name = typeB.BaseName,
            Namespace = typeB.Namespace,
            TypeReferenceId = refB.Id,
            Source = "typedef A B;"
        };
        repository.InsertTypeDef(typeDefB);
        repository.SaveChanges();

        // typedef B C;
        var typeC = new TypeModel
        {
            BaseName = "C",
            Namespace = string.Empty,
            Type = TypeType.Typedef,
            Source = "typedef B C;"
        };
        typeC.StoredFullyQualifiedName = typeC.FullyQualifiedName;

        var refC = new TypeReference
        {
            TypeString = "B",
            ReferencedTypeId = typeB.Id
        };

        repository.InsertType(typeC);
        repository.SaveChanges();
        repository.InsertTypeReference(refC);
        repository.SaveChanges();

        var typeDefC = new TypeDefModel
        {
            Name = typeC.BaseName,
            Namespace = typeC.Namespace,
            TypeReferenceId = refC.Id,
            Source = "typedef B C;"
        };
        repository.InsertTypeDef(typeDefC);
        repository.SaveChanges();

        // Act
        var resolved = repository.ResolveTypeDefChain("C");

        // Assert
        Assert.NotNull(resolved);
        Assert.Equal(intType.Id, resolved.ReferencedTypeId);
    }

    [Fact]
    public void ResolveTypeDefChain_CircularTypedef_DetectsCircularity()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        // Create circular typedef: A -> B -> A
        // typedef B A;
        var typeA = new TypeModel
        {
            BaseName = "A",
            Namespace = string.Empty,
            Type = TypeType.Typedef,
            Source = "typedef B A;"
        };
        typeA.StoredFullyQualifiedName = typeA.FullyQualifiedName;
        repository.InsertType(typeA);
        repository.SaveChanges();

        // typedef A B;
        var typeB = new TypeModel
        {
            BaseName = "B",
            Namespace = string.Empty,
            Type = TypeType.Typedef,
            Source = "typedef A B;"
        };
        typeB.StoredFullyQualifiedName = typeB.FullyQualifiedName;
        repository.InsertType(typeB);
        repository.SaveChanges();

        // Setup circular references
        var refA = new TypeReference
        {
            TypeString = "B",
            ReferencedTypeId = typeB.Id
        };
        repository.InsertTypeReference(refA);
        repository.SaveChanges();

        var refB = new TypeReference
        {
            TypeString = "A",
            ReferencedTypeId = typeA.Id
        };
        repository.InsertTypeReference(refB);
        repository.SaveChanges();

        var typeDefA = new TypeDefModel
        {
            Name = typeA.BaseName,
            Namespace = typeA.Namespace,
            TypeReferenceId = refA.Id,
            Source = "typedef B A;"
        };
        repository.InsertTypeDef(typeDefA);
        repository.SaveChanges();

        var typeDefB = new TypeDefModel
        {
            Name = typeB.BaseName,
            Namespace = typeB.Namespace,
            TypeReferenceId = refB.Id,
            Source = "typedef A B;"
        };
        repository.InsertTypeDef(typeDefB);
        repository.SaveChanges();

        // Act
        var resolved = repository.ResolveTypeDefChain("A");

        // Assert - Should return null due to circular dependency
        Assert.Null(resolved);
    }

    [Fact]
    public void GetAllTypeDefs_MultipleTypedefs_ReturnsAll()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        // Create multiple typedefs
        for (int i = 0; i < 3; i++)
        {
            var typeModel = new TypeModel
            {
                BaseName = $"TypeDef{i}",
                Namespace = string.Empty,
                Type = TypeType.Typedef,
                Source = $"typedef int TypeDef{i};"
            };
            typeModel.StoredFullyQualifiedName = typeModel.FullyQualifiedName;

            var typeReference = new TypeReference
            {
                TypeString = "int"
            };

            repository.InsertType(typeModel);
            repository.SaveChanges();
            repository.InsertTypeReference(typeReference);
            repository.SaveChanges();

            var typeDef = new TypeDefModel
            {
                Name = typeModel.BaseName,
                Namespace = typeModel.Namespace,
                TypeReferenceId = typeReference.Id,
                Source = $"typedef int TypeDef{i};"
            };
            repository.InsertTypeDef(typeDef);
            repository.SaveChanges();
        }

        // Act
        var allTypeDefs = repository.GetAllTypeDefs();

        // Assert
        Assert.Equal(3, allTypeDefs.Count);
    }
}
