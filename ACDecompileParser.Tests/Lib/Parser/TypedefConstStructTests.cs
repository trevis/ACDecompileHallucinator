using ACDecompileParser.Lib.Parser;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class TypedefConstStructTests
{
    [Fact]
    public void Parse_TypedefConstStruct_ShouldBeTypedefNotStruct()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 109 */",
            "typedef const struct _s_CatchableType CatchableType;"
        };

        // Act
        var structs = TypeParser.ParseStructs(source);
        var typedefs = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Empty(structs);
        Assert.Single(typedefs);
        Assert.Equal("CatchableType", typedefs[0].Name);
        Assert.NotNull(typedefs[0].TypeReference);
        Assert.Equal("const struct _s_CatchableType", typedefs[0].TypeReference!.TypeString);
    }
}
