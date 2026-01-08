using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Shared.Tests.Lib.Storage;

public class TypeRepositoryTests
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
    public void InsertType_AddsTypeToDatabase()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "TestNamespace",
            Type = TypeType.Class
        };

        // Act
        var id = repository.InsertType(typeModel);
        repository.SaveChanges(); // Save changes to commit the insert

        // Assert
        Assert.True(id > 0);

        var retrievedType = repository.GetTypeById(id);
        Assert.NotNull(retrievedType);
        Assert.Equal("TestClass", retrievedType.BaseName);
        Assert.Equal("TestNamespace", retrievedType.Namespace);
        Assert.Equal(TypeType.Class, retrievedType.Type);
    }

    [Fact]
    public void GetTypeById_RetrievesTypeWithRelationships()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "TestNamespace",
            Type = TypeType.Class,
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument
                {
                    Position = 0,
                    TypeString = "int",
                    TypeReferenceId = null
                }
            },
            BaseTypes = new List<TypeInheritance>
            {
                new TypeInheritance
                {
                    RelatedTypeString = "BaseClass"
                }
            }
        };

        var id = repository.InsertType(typeModel);
        repository.SaveChanges(); // Save changes to commit the insert

        // Act
        var retrievedType = repository.GetTypeById(id);

        // Assert
        Assert.NotNull(retrievedType);
        Assert.Equal("TestClass", retrievedType.BaseName);
        Assert.Single(retrievedType.TemplateArguments);
        Assert.Equal("int", retrievedType.TemplateArguments[0].TypeString);
        Assert.Single(retrievedType.BaseTypes);
        Assert.Equal("BaseClass", retrievedType.BaseTypes[0].RelatedTypeString);
    }

    [Fact]
    public void UpdateType_UpdatesExistingType()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "TestNamespace",
            Type = TypeType.Class
        };

        var id = repository.InsertType(typeModel);
        repository.SaveChanges(); // Save changes to commit the insert

        // Modify the type
        typeModel.BaseName = "UpdatedClass";
        typeModel.Namespace = "UpdatedNamespace";

        // Act
        repository.UpdateType(typeModel);
        repository.SaveChanges(); // Save changes to commit the update

        // Assert
        var updatedType = repository.GetTypeById(id);
        Assert.NotNull(updatedType);
        Assert.Equal("UpdatedClass", updatedType.BaseName);
        Assert.Equal("UpdatedNamespace", updatedType.Namespace);
    }

    [Fact]
    public void DeleteType_RemovesTypeFromDatabase()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "TestNamespace",
            Type = TypeType.Class
        };

        var id = repository.InsertType(typeModel);
        repository.SaveChanges(); // Save changes to commit the insert

        // Act
        repository.DeleteType(id);
        repository.SaveChanges(); // Save changes to commit the delete

        // Assert
        var retrievedType = repository.GetTypeById(id);
        Assert.Null(retrievedType);
    }

    [Fact]
    public void GetAllTypes_ReturnsAllTypes()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var type1 = new TypeModel
        {
            BaseName = "ClassA",
            Namespace = "NS1",
            Type = TypeType.Class
        };

        var type2 = new TypeModel
        {
            BaseName = "ClassB",
            Namespace = "NS2",
            Type = TypeType.Struct
        };

        repository.InsertType(type1);
        repository.InsertType(type2);
        repository.SaveChanges(); // Save changes to commit the inserts

        // Act
        var allTypes = repository.GetAllTypes();

        // Assert
        Assert.Equal(2, allTypes.Count);
        Assert.Contains(allTypes, t => t.BaseName == "ClassA");
        Assert.Contains(allTypes, t => t.BaseName == "ClassB");
    }

    [Fact]
    public void GetTypesByNamespace_ReturnsTypesInNamespace()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var type1 = new TypeModel
        {
            BaseName = "ClassA",
            Namespace = "TestNS",
            Type = TypeType.Class
        };

        var type2 = new TypeModel
        {
            BaseName = "ClassB",
            Namespace = "OtherNS",
            Type = TypeType.Struct
        };

        repository.InsertType(type1);
        repository.InsertType(type2);
        repository.SaveChanges(); // Save changes to commit the inserts

        // Act
        var types = repository.GetTypesByNamespace("TestNS");

        // Assert
        Assert.Single(types);
        Assert.Equal("ClassA", types[0].BaseName);
        Assert.Equal("TestNS", types[0].Namespace);
    }

    [Fact]
    public void GetTypesByTypeType_ReturnsTypesOfSpecificType()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var type1 = new TypeModel
        {
            BaseName = "ClassA",
            Namespace = "NS1",
            Type = TypeType.Class
        };

        var type2 = new TypeModel
        {
            BaseName = "StructB",
            Namespace = "NS2",
            Type = TypeType.Struct
        };

        repository.InsertType(type1);
        repository.InsertType(type2);
        repository.SaveChanges(); // Save changes to commit the inserts

        // Act
        var classTypes = repository.GetTypesByTypeType(TypeType.Class);

        // Assert
        Assert.Single(classTypes);
        Assert.Equal("ClassA", classTypes[0].BaseName);
        Assert.Equal(TypeType.Class, classTypes[0].Type);
    }

    [Fact]
    public void GetTypeByFullyQualifiedName_ReturnsCorrectType()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "TestNS",
            Type = TypeType.Class
        };
        // Ensure the stored fully qualified name is set correctly
        typeModel.StoredFullyQualifiedName = typeModel.FullyQualifiedName;

        repository.InsertType(typeModel);
        repository.SaveChanges(); // Save changes to commit the insert

        // Act
        var retrievedType = repository.GetTypeByFullyQualifiedName("TestNS::TestClass");

        // Assert
        Assert.NotNull(retrievedType);
        Assert.Equal("TestClass", retrievedType.BaseName);
        Assert.Equal("TestNS", retrievedType.Namespace);
    }

    [Fact]
    public void SearchTypes_FindsMatchingTypes()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var type1 = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "TestNS",
            Type = TypeType.Class
        };

        var type2 = new TypeModel
        {
            BaseName = "OtherClass",
            Namespace = "OtherNS",
            Type = TypeType.Struct
        };

        repository.InsertType(type1);
        repository.InsertType(type2);
        repository.SaveChanges(); // Save changes to commit the inserts

        // Act
        var searchResults = repository.SearchTypes("Test");

        // Assert
        Assert.Single(searchResults);
        Assert.Equal("TestClass", searchResults[0].BaseName);
    }

    [Fact]
    public void InsertEnumMember_AddsEnumMemberToDatabase()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "TestEnum",
            Namespace = "TestNS",
            Type = TypeType.Enum
        };

        var typeId = repository.InsertType(typeModel);
        repository.SaveChanges(); // Save changes to commit the insert

        var enumMember = new EnumMemberModel
        {
            Name = "ValueA",
            Value = "0",
            EnumTypeId = typeId
        };

        // Act
        var memberId = repository.InsertEnumMember(enumMember);
        repository.SaveChanges(); // Save changes to commit the enum member insert

        // Assert
        Assert.True(memberId > 0);

        var members = repository.GetEnumMembers(typeId);
        Assert.Single(members);
        Assert.Equal("ValueA", members[0].Name);
        Assert.Equal("0", members[0].Value);
    }

    [Fact]
    public void InsertStructMember_AddsStructMemberToDatabase()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var typeModel = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "TestNS",
            Type = TypeType.Struct
        };

        var typeId = repository.InsertType(typeModel);
        repository.SaveChanges(); // Save changes to commit the type insert
        Assert.True(typeId > 0); // Verify type was inserted successfully

        var structMember = new StructMemberModel
        {
            Name = "member1",
            TypeString = "int",
            Offset = 0,
            StructTypeId = typeId
        };

        // Act
        var memberId = repository.InsertStructMember(structMember);
        repository.SaveChanges(); // Save changes to commit the struct member insert

        // Assert
        Assert.True(memberId > 0);

        // Query using the same repository instance
        var members = repository.GetStructMembers(typeId);
        Assert.Single(members);
        Assert.Equal("member1", members[0].Name);
        Assert.Equal("int", members[0].TypeString);
        Assert.Equal(0, members[0].Offset);
    }

    [Fact]
    public void InsertFunctionParameter_AddsFunctionParameterToDatabase()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        var functionSignature = new FunctionSignatureModel
        {
            Name = "funcPtrSig",
            ReturnTypeReference = new TypeReference { TypeString = "int" }
        };

        var signatureId = repository.InsertFunctionSignature(functionSignature);
        repository.SaveChanges();

        var functionParam = new FunctionParamModel
        {
            Name = "param1",
            ParameterType = "int",
            Position = 0,
            ParentFunctionSignatureId = signatureId
        };

        // Act
        var paramId = repository.InsertFunctionParameter(functionParam);
        repository.SaveChanges(); // Save changes to commit the function parameter insert

        // Assert
        Assert.True(paramId > 0);

        var paramsList = repository.GetFunctionParameters(signatureId);
        Assert.Single(paramsList);
        Assert.Equal("param1", paramsList[0].Name);
        Assert.Equal("int", paramsList[0].ParameterType);
    }
}
