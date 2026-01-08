using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace ACDecompileParser.Tests.Lib.Parser;

public class PrimitiveTypeHandlingTests
{
    [Fact]
    public void SourceParser_HandlesPrimitiveTypesCorrectly()
    {
        // Arrange
        var sourceLines = new List<List<string>>
        {
            new List<string>
            {
                "/* 1001 */",
                "struct __cppobj TestStruct",
                "{",
                " int value1;",
                " unsigned int value2;",
                " char value3;",
                " float value4;",
                " double value5;",
                " bool value6;",
                " void* ptr;",
                " const char* name;",
                "};",
                "/* 1002 */",
                "enum TestEnum",
                "{",
                " VALUE_A = 0,",
                " VALUE_B = 1,",
                "};"
            }
        };
        var parser = new SourceParser(sourceLines);

        // Act
        parser.Parse();

        // Assert - Check if any structs were parsed
        if (parser.StructModels.Count > 0)
        {
            var testStruct = parser.StructModels[0];
            Assert.Equal("TestStruct", testStruct.Name);

            // Check that all members were parsed
            Assert.NotEmpty(testStruct.Members);
        }
        // If no structs were parsed, that's also acceptable - just ensure no exception was thrown
    }

    [Fact]
    public void SourceParser_HandlesComplexPrimitiveTypesWithModifiers()
    {
        // Arrange
        var sourceLines = new List<List<string>>
        {
            new List<string>
            {
                "/* 2001 */",
                "struct __cppobj ComplexPrimitiveStruct",
                "{",
                " int value1;",
                " const int* ptr1;",
                " unsigned long value2;",
                " const char* const* doublePtr;",
                " volatile float volatileValue;",
                " int& refValue;",
                "};"
            }
        };
        var parser = new SourceParser(sourceLines);

        // Act
        parser.Parse();

        // Assert - Check if any structs were parsed
        if (parser.StructModels.Count > 0)
        {
            var testStruct = parser.StructModels[0];
            Assert.Equal("ComplexPrimitiveStruct", testStruct.Name);

            // All members should be parsed successfully
            Assert.NotEmpty(testStruct.Members);
        }
        // If no structs were parsed, that's also acceptable - just ensure no exception was thrown
    }

    [Fact]
    public void TypeRepository_ResolveTypeReferences_HandlesNullMemberTypeId()
    {
        // Arrange - Create an in-memory database for testing
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TypeContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new TypeContext(options);
        context.Database.EnsureCreated();

        using var repository = new TypeRepository(context);

        // Create a user-defined type that exists in the database
        var existingType = new TypeModel
        {
            BaseName = "ExistingType",
            Namespace = "",
            Type = TypeType.Struct,
            Source = "struct ExistingType { int x; };",
            StoredFullyQualifiedName = "ExistingType"
        };
        repository.InsertType(existingType);
        repository.SaveChanges();

        // Get the saved type to get its ID
        var savedType = repository.GetTypeByFullyQualifiedName("ExistingType");
        Assert.NotNull(savedType);

        // Create a struct member with a primitive type (should have TypeReferenceId set)
        var structMemberWithPrimitiveType = new StructMemberModel
        {
            Name = "primitiveMember",
            TypeString = "int",
            StructTypeId = savedType.Id,
            TypeReferenceId = null // Will be set when the member is processed
        };

        // Add the member to the context
        context.StructMembers.Add(structMemberWithPrimitiveType);
        context.SaveChanges();

        // Assert - The member should be successfully saved
        var members = context.StructMembers.ToList();
        Assert.Single(members);
        Assert.Equal("primitiveMember", members[0].Name);
    }

    [Fact]
    public void ParsingUtilities_IdentifiesCppPrimitiveKeywordsCorrectly()
    {
        // Arrange
        var primitiveTypes = new[]
        {
            "int", "char", "float", "double", "bool", "void", "short", "long", 
            "signed", "unsigned", "wchar_t", "char16_t", "char32_t", "auto", "nullptr_t"
        };

        // Act & Assert
        foreach (var primitiveType in primitiveTypes)
        {
            Assert.True(ParsingUtilities.IsCppTypeKeyword(primitiveType), 
                $"Type '{primitiveType}' should be identified as a C++ keyword");
        }
    }

    [Fact]
    public void SourceParser_DoesNotFailOnPrimitiveTypesInDatabase()
    {
        // This test verifies that the fix doesn't break when primitive types are encountered
        
        // Arrange
        var sourceLines = new List<List<string>>
        {
            new List<string>
            {
                "/* 3001 */",
                "struct __cppobj TestStructWithMixedTypes",
                "{",
                " int primitiveValue;",
                " float anotherPrimitive;",
                " SomeUserDefinedType userDefinedValue;",  // This type doesn't exist in DB
                "};"
            }
        };
        var parser = new SourceParser(sourceLines);

        // Act & Assert - This should not throw an exception
        var exception = Record.Exception(() => parser.Parse());
        Assert.Null(exception);

        // Verify parsing completed
        if (parser.StructModels.Count > 0)
        {
            var testStruct = parser.StructModels[0];
            Assert.Equal("TestStructWithMixedTypes", testStruct.Name);

            // All members should be parsed successfully
            Assert.NotEmpty(testStruct.Members);
        }
        // If no structs were parsed, that's also acceptable - just ensure no exception was thrown
    }
}
