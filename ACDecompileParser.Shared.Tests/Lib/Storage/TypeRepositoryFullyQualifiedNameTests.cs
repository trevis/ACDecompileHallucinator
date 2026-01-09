using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Shared.Tests.Lib.Storage;

public class TypeRepositoryFullyQualifiedNameTests
{
    private readonly DbContextOptions<TypeContext> _options;

    public TypeRepositoryFullyQualifiedNameTests()
    {
        _options = new DbContextOptionsBuilder<TypeContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public void InsertType_StoresFullyQualifiedName()
    {
        // Arrange
        using var context = new TypeContext(_options);
        using var repository = new SqlTypeRepository(context);

        var type = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "MyNamespace",
            Type = TypeType.Struct
        };

        // Act
        var id = repository.InsertType(type);
        repository.SaveChanges(); // Save changes to commit the insert

        // Assert
        Assert.True(id > 0);

        // Verify the stored fully qualified name
        var retrievedType = repository.GetTypeById(id);
        Assert.NotNull(retrievedType);
        Assert.Equal("MyNamespace::TestClass", retrievedType.StoredFullyQualifiedName);
        Assert.Equal("MyNamespace::TestClass", retrievedType.FullyQualifiedName);
    }

    [Fact]
    public void GetTypeByFullyQualifiedName_FindsType()
    {
        // Arrange
        using var context = new TypeContext(_options);
        using var repository = new SqlTypeRepository(context);

        var type = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "MyNamespace",
            Type = TypeType.Struct
        };
        // Set the stored fully qualified name before inserting
        type.StoredFullyQualifiedName = type.FullyQualifiedName;

        var id = repository.InsertType(type);
        repository.SaveChanges(); // Save changes to commit the insert
        Assert.True(id > 0);

        // Act
        var retrievedType = repository.GetTypeByFullyQualifiedName("MyNamespace::TestClass");

        // Assert
        Assert.NotNull(retrievedType);
        Assert.Equal("TestClass", retrievedType.BaseName);
        Assert.Equal("MyNamespace", retrievedType.Namespace);
        Assert.Equal("MyNamespace::TestClass", retrievedType.StoredFullyQualifiedName);
    }

    [Fact]
    public void UpdateType_UpdatesFullyQualifiedName()
    {
        // Arrange
        using var context = new TypeContext(_options);
        using var repository = new SqlTypeRepository(context);

        var type = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "MyNamespace",
            Type = TypeType.Struct
        };
        var id = repository.InsertType(type);
        repository.SaveChanges(); // Save changes to commit the insert
        Assert.True(id > 0);

        // Update the type
        type.Namespace = "NewNamespace";
        type.BaseName = "NewTestClass";

        // Act
        repository.UpdateType(type);
        repository.SaveChanges(); // Save changes to commit the update

        // Assert
        var retrievedType = repository.GetTypeById(id);
        Assert.NotNull(retrievedType);
        Assert.Equal("NewTestClass", retrievedType.BaseName);
        Assert.Equal("NewNamespace", retrievedType.Namespace);
        Assert.Equal("NewNamespace::NewTestClass", retrievedType.StoredFullyQualifiedName);
    }

    [Fact]
    public void SearchTypes_FindsByFullyQualifiedName()
    {
        // Arrange
        using var context = new TypeContext(_options);
        using var repository = new SqlTypeRepository(context);

        var type = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "MyNamespace",
            Type = TypeType.Struct
        };
        // Ensure the stored fully qualified name is set
        type.StoredFullyQualifiedName = type.FullyQualifiedName;

        var id = repository.InsertType(type);
        repository.SaveChanges(); // Save changes to commit the insert
        Assert.True(id > 0);

        // Act
        var results = repository.SearchTypes("MyNamespace::TestClass");

        // Assert
        Assert.NotEmpty(results);
        var foundType = results.FirstOrDefault(t => t.Id == id);
        Assert.NotNull(foundType);
        Assert.Equal("MyNamespace::TestClass", foundType.StoredFullyQualifiedName);
    }

    [Fact]
    public void InsertType_WithTemplateArguments_StoresFullyQualifiedName()
    {
        // Arrange
        using var context = new TypeContext(_options);
        using var repository = new SqlTypeRepository(context);

        var type = new TypeModel
        {
            BaseName = "SmartArray",
            Namespace = "Container",
            Type = TypeType.Struct
        };

        // Add a template argument
        type.TemplateArguments = new List<TypeTemplateArgument>
        {
            new TypeTemplateArgument
            {
                Position = 0,
                TypeString = "int"
            }
        };

        // Act
        var id = repository.InsertType(type);
        repository.SaveChanges(); // Save changes to commit the insert

        // Assert
        Assert.True(id > 0);

        // Verify the stored fully qualified name
        var retrievedType = repository.GetTypeById(id);
        Assert.NotNull(retrievedType);
        // The fully qualified name includes template arguments
        Assert.Equal("Container::SmartArray<int>", retrievedType.FullyQualifiedName);
        // The stored fully qualified name should match the computed one
        Assert.Equal(retrievedType.FullyQualifiedName, retrievedType.StoredFullyQualifiedName);
    }
}
