using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;

namespace ACDecompileParser.Tests.Lib.Parser;

public class EnumBitmaskTests
{
    [Fact]
    public void Parse_WithBitmaskKeyword_SetsIsBitmaskFlag()
    {
        // Arrange
        var source = "enum __bitmask MyBitmask { Flag1 = 1, Flag2 = 2 };";
        var enumModel = new EnumTypeModel();

        // Act
        EnumParser.ParseName(enumModel, source);
        EnumParser.ParseMembers(enumModel, source);

        // Assert
        Assert.True(enumModel.IsBitmask);
        Assert.Equal("MyBitmask", enumModel.Name);
        Assert.Equal(2, enumModel.Members.Count);
    }

    [Fact]
    public void Parse_WithoutBitmaskKeyword_FlagIsFalse()
    {
        // Arrange
        var source = "enum MyEnum { Value1, Value2 };";
        var enumModel = new EnumTypeModel();

        // Act
        EnumParser.ParseName(enumModel, source);

        // Assert
        Assert.False(enumModel.IsBitmask);
        Assert.Equal("MyEnum", enumModel.Name);
    }

    [Fact]
    public void SaveToDatabase_PersistsBitmaskFlag()
    {
        // Arrange
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 100 */",
                "enum __bitmask StoredBitmask { A = 1 };"
            }
        };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new ACDecompileParser.Shared.Lib.Storage.TypeRepository(context);

        // Act
        parser.SaveToDatabase(repo);

        // Assert
        var type = repo.GetAllTypes().Single();
        Assert.Equal("StoredBitmask", type.BaseName);
        Assert.True(type.IsBitmask);
    }

    [Fact]
    public void Generator_WithBitmaskFlag_OutputsKeyword()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "GeneratedBitmask",
            Type = TypeType.Enum,
            IsBitmask = true
        };
        var generator = new EnumOutputGenerator();

        // Act
        var tokens = generator.Generate(typeModel).ToList();

        // Assert
        var tokenString = string.Join("", tokens.Select(t => t.Text));
        Assert.Contains("enum __bitmask GeneratedBitmask", tokenString);
    }
}
