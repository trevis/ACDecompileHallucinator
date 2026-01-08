using ACDecompileParser.Lib.Parser;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class TemplatePointerArrayTests
{
    [Fact]
    public void ParseMemberDeclaration_ParsesTemplatePointerArrayMember()
    {
        // Arrange - from the user's issue: template pointer array
        var line = "  HashSetData<UIElement *> *m_aInplaceBuckets[23];";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("m_aInplaceBuckets", result.Name);
        Assert.NotNull(result.TypeReference);
        Assert.True(result.TypeReference.IsArray);
        Assert.Equal(23, result.TypeReference.ArraySize);
        Assert.True(result.TypeReference.IsPointer);
    }
}
