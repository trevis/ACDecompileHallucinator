using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Parser;

public class MemberPaddingParserTests
{
    [Fact]
    public void ParseMemberDeclaration_ParsesBytePaddingWithoutName()
    {
        // Arrange
        var line = "_BYTE[4];";

        // Reset the padding counter by using reflection to access the private field
        // This ensures we have a predictable test result
        var memberParserType = typeof(MemberParser);
        var paddingCounterField = memberParserType.GetField("_paddingCounter", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        paddingCounterField?.SetValue(null, 0);

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("__padding", result.Name);
        Assert.Equal("_BYTE", result.TypeString);
        Assert.NotNull(result.TypeReference);
        Assert.True(result.TypeReference.IsArray);
        Assert.Equal(4, result.TypeReference.ArraySize);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesMultipleBytePaddingWithUniqueNames()
    {
        // Arrange
        var line1 = "_BYTE[4];";
        var line2 = "_BYTE[8];";

        // Reset the padding counter
        var memberParserType = typeof(MemberParser);
        var paddingCounterField = memberParserType.GetField("_paddingCounter", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        paddingCounterField?.SetValue(null, 0);

        // Act
        var result1 = MemberParser.ParseMemberDeclaration(line1);
        var result2 = MemberParser.ParseMemberDeclaration(line2);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.StartsWith("__padding", result1.Name);
        Assert.StartsWith("__padding", result2.Name);
        Assert.Equal("_BYTE", result1.TypeString);
        Assert.Equal("_BYTE", result2.TypeString);
        Assert.True(result1.TypeReference?.IsArray);
        Assert.True(result2.TypeReference?.IsArray);
        Assert.Equal(4, result1.TypeReference?.ArraySize);
        Assert.Equal(8, result2.TypeReference?.ArraySize);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesBytePaddingWithOffset()
    {
        // Arrange
        var line = "_BYTE[16]; /* 0x010 */";

        // Reset the padding counter
        var memberParserType = typeof(MemberParser);
        var paddingCounterField = memberParserType.GetField("_paddingCounter", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        paddingCounterField?.SetValue(null, 0);

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("__padding", result.Name);
        Assert.Equal("_BYTE", result.TypeString);
        Assert.Equal(16, result.Offset);
        Assert.NotNull(result.TypeReference);
        Assert.True(result.TypeReference.IsArray);
        Assert.Equal(16, result.TypeReference.ArraySize);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesBytePaddingWithHexOffset()
    {
        // Arrange
        var line = "_BYTE[32]; /* 0x020 */";

        // Reset the padding counter
        var memberParserType = typeof(MemberParser);
        var paddingCounterField = memberParserType.GetField("_paddingCounter", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        paddingCounterField?.SetValue(null, 0);

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("__padding", result.Name);
        Assert.Equal("_BYTE", result.TypeString);
        Assert.Equal(32, result.Offset);
        Assert.NotNull(result.TypeReference);
        Assert.True(result.TypeReference.IsArray);
        Assert.Equal(32, result.TypeReference.ArraySize);
    }

    [Fact]
    public void ParseMemberDeclaration_DoesNotAffectRegularMembers()
    {
        // Arrange
        var regularLine = "char buffer[256];";
        var paddingLine = "_BYTE[4];";

        // Reset the padding counter
        var memberParserType = typeof(MemberParser);
        var paddingCounterField = memberParserType.GetField("_paddingCounter", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        paddingCounterField?.SetValue(null, 0);

        // Act
        var regularResult = MemberParser.ParseMemberDeclaration(regularLine);
        var paddingResult = MemberParser.ParseMemberDeclaration(paddingLine);

        // Assert - regular member should still work normally
        Assert.NotNull(regularResult);
        Assert.Equal("buffer", regularResult.Name);
        Assert.NotNull(regularResult.TypeReference);
        Assert.True(regularResult.TypeReference.IsArray);
        Assert.Equal(256, regularResult.TypeReference.ArraySize);

        // Assert - padding member should work as expected
        Assert.NotNull(paddingResult);
        Assert.StartsWith("__padding", paddingResult.Name);
        Assert.Equal("_BYTE", paddingResult.TypeString);
        Assert.NotNull(paddingResult.TypeReference);
        Assert.True(paddingResult.TypeReference.IsArray);
        Assert.Equal(4, paddingResult.TypeReference.ArraySize);
    }

    [Fact]
    public void ParseMemberDeclaration_HandlesDifferentByteSizes()
    {
        // Arrange
        var line1 = "_BYTE[1];";
        var line2 = "_BYTE[2];";
        var line3 = "_BYTE[100];";

        // Reset the padding counter
        var memberParserType = typeof(MemberParser);
        var paddingCounterField = memberParserType.GetField("_paddingCounter", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        paddingCounterField?.SetValue(null, 0);

        // Act
        var result1 = MemberParser.ParseMemberDeclaration(line1);
        var result2 = MemberParser.ParseMemberDeclaration(line2);
        var result3 = MemberParser.ParseMemberDeclaration(line3);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.StartsWith("__padding", result1.Name);
        Assert.StartsWith("__padding", result2.Name);
        Assert.StartsWith("__padding", result3.Name);
        Assert.Equal(1, result1.TypeReference?.ArraySize);
        Assert.Equal(2, result2.TypeReference?.ArraySize);
        Assert.Equal(100, result3.TypeReference?.ArraySize);
    }
}
