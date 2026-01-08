using System.Collections.Generic;
using System.Linq;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class EnumOutputGeneratorTests
{
    [Fact]
    public void Generate_BasicEnum_ProducesCorrectTokens()
    {
        // Arrange
        var repository = new TestTypeRepository();
        var enumType = new TypeModel
        {
            Id = 1,
            BaseName = "TestEnum",
            Namespace = string.Empty,
            Type = TypeType.Enum,
            Source = "enum TestEnum { VALUE1, VALUE2 };"
        };

        var members = new List<EnumMemberModel>
        {
            new EnumMemberModel { Name = "VALUE1", Value = string.Empty, EnumTypeId = 1 },
            new EnumMemberModel { Name = "VALUE2", Value = string.Empty, EnumTypeId = 1 }
        };

        repository.AddEnumMembers(1, members);

        var generator = new EnumOutputGenerator(repository);

        // Act
        var tokens = generator.Generate(enumType).ToList();

        // Assert
        // Check for comment
        Assert.Contains(tokens, t => t.Type == TokenType.Comment && t.Text.Contains("Reconstructed"));

        // Check for enum keyword
        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "enum");

        // Check for enum name with reference
        Assert.Contains(tokens, t => t.Type == TokenType.TypeName && t.Text == "TestEnum" && t.ReferenceId == "1");

        // Check for member names
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Text == "VALUE1");
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Text == "VALUE2");

        // Check for punctuation
        Assert.Contains(tokens, t => t.Type == TokenType.Punctuation && t.Text == "{");
        Assert.Contains(tokens, t => t.Type == TokenType.Punctuation && t.Text == "};");
    }

    [Fact]
    public void Generate_EnumWithNamespace_IncludesNamespaceInOutput()
    {
        // Arrange
        var repository = new TestTypeRepository();
        var enumType = new TypeModel
        {
            Id = 2,
            BaseName = "MyEnum",
            Namespace = "AC1Modern",
            Type = TypeType.Enum,
            Source = "enum AC1Modern::MyEnum { VALUE1 };"
        };

        var members = new List<EnumMemberModel>
        {
            new EnumMemberModel { Name = "VALUE1", Value = string.Empty, EnumTypeId = 2 }
        };

        repository.AddEnumMembers(2, members);

        var generator = new EnumOutputGenerator(repository);

        // Act
        var tokens = generator.Generate(enumType).ToList();

        // Assert
        // Check for namespace
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Text == "AC1Modern::");

        // Check for enum name
        Assert.Contains(tokens, t => t.Type == TokenType.TypeName && t.Text == "MyEnum");
    }

    [Fact]
    public void Generate_EnumWithHexValues_ProducesNumberLiterals()
    {
        // Arrange
        var repository = new TestTypeRepository();
        var enumType = new TypeModel
        {
            Id = 3,
            BaseName = "CLUSAGE",
            Namespace = string.Empty,
            Type = TypeType.Enum,
            Source = "enum CLUSAGE { D3DDECLUSAGE_POSITION = 0x0, D3DDECLUSAGE_BLENDWEIGHT = 0x1 };"
        };

        var members = new List<EnumMemberModel>
        {
            new EnumMemberModel { Name = "D3DDECLUSAGE_POSITION", Value = "0x0", EnumTypeId = 3 },
            new EnumMemberModel { Name = "D3DDECLUSAGE_BLENDWEIGHT", Value = "0x1", EnumTypeId = 3 }
        };

        repository.AddEnumMembers(3, members);

        var generator = new EnumOutputGenerator(repository);

        // Act
        var tokens = generator.Generate(enumType).ToList();

        // Assert
        // Check for hex values as number literals
        Assert.Contains(tokens, t => t.Type == TokenType.NumberLiteral && t.Text == "0x0");
        Assert.Contains(tokens, t => t.Type == TokenType.NumberLiteral && t.Text == "0x1");

        // Check for assignment operator
        var assignmentTokens = tokens.Where(t => t.Type == TokenType.Punctuation && t.Text == " = ").ToList();
        Assert.Equal(2, assignmentTokens.Count);
    }

    [Fact]
    public void Generate_EnumWithoutRepository_HandlesGracefully()
    {
        // Arrange
        var enumType = new TypeModel
        {
            Id = 4,
            BaseName = "TestEnum",
            Namespace = string.Empty,
            Type = TypeType.Enum,
            Source = "enum TestEnum { VALUE1 };"
        };

        var generator = new EnumOutputGenerator(null);

        // Act
        var tokens = generator.Generate(enumType).ToList();

        // Assert
        // Should still generate basic structure even without repository
        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "enum");
        Assert.Contains(tokens, t => t.Type == TokenType.TypeName && t.Text == "TestEnum");
        Assert.Contains(tokens, t => t.Type == TokenType.Punctuation && t.Text == "{");
        Assert.Contains(tokens, t => t.Type == TokenType.Punctuation && t.Text == "};");
    }

    [Fact]
    public void Generate_EnumMembers_LastMemberHasNoComma()
    {
        // Arrange
        var repository = new TestTypeRepository();
        var enumType = new TypeModel
        {
            Id = 5,
            BaseName = "TestEnum",
            Namespace = string.Empty,
            Type = TypeType.Enum,
            Source = "enum TestEnum { VALUE1, VALUE2, VALUE3 };"
        };

        var members = new List<EnumMemberModel>
        {
            new EnumMemberModel { Name = "VALUE1", Value = "0", EnumTypeId = 5 },
            new EnumMemberModel { Name = "VALUE2", Value = "1", EnumTypeId = 5 },
            new EnumMemberModel { Name = "VALUE3", Value = "2", EnumTypeId = 5 }
        };

        repository.AddEnumMembers(5, members);

        var generator = new EnumOutputGenerator(repository);

        // Act
        var tokens = generator.Generate(enumType).ToList();

        // Assert
        // Find all commas
        var commas = tokens.Where(t => t.Type == TokenType.Punctuation && t.Text == ",").ToList();
        
        // Should have 2 commas (after VALUE1 and VALUE2, but not after VALUE3)
        Assert.Equal(2, commas.Count);

        // Verify the last identifier before }; is VALUE3 without a comma
        var closingBraceIndex = tokens.FindIndex(t => t.Type == TokenType.Punctuation && t.Text == "};");
        var tokensBeforeClosing = tokens.Take(closingBraceIndex).Reverse().ToList();
        var lastIdentifier = tokensBeforeClosing.First(t => t.Type == TokenType.Identifier);
        Assert.Equal("VALUE3", lastIdentifier.Text);
    }
}
