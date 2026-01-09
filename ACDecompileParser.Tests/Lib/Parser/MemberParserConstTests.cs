using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Parser;

public class MemberParserConstTests
{
    [Fact]
    public void ParseMemberDeclaration_HandlesConstPointer()
    {
        // Arrange
        var line = "IUnknown *const m_pUnknown;";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("m_pUnknown", result.Name);
        // The parser logic for *const might require the * to be associated with the type.
        // If *const is stripped, it might be IUnknown* or IUnknown
        // But usually *const means the pointer itself is const, so the type should effectively constitute IUnknown*
        // However, MemberParser splits type and name.
        // If "const" is part of the type string, it needs to be handled.
        
        // Expected behavior: The parser should handle it and likely produce "IUnknown*" as type string
        // or at least "IUnknown" with IsPointer=true.
        // Let's assume we want "IUnknown *" or "IUnknown" with pointer depth 1.
        Assert.Contains("IUnknown", result.TypeString);
        Assert.True(result.TypeReference.IsPointer, "Should be identified as a pointer");
    }

    [Fact]
    public void ParseMemberDeclaration_HandlesConstBeforeType()
    {
        var line = "const int x;";
        var result = MemberParser.ParseMemberDeclaration(line);
        Assert.NotNull(result);
        Assert.Equal("x", result.Name);
        // Should simplify to int or const int
        Assert.EndsWith("int", result.TypeString); 
    }

    [Fact]
    public void ParseMemberDeclaration_HandlesConstInMiddle()
    {
        var line = "int const * x;"; // pointer to const int
        var result = MemberParser.ParseMemberDeclaration(line);
        Assert.NotNull(result);
        Assert.Equal("x", result.Name);
        Assert.True(result.TypeReference.IsPointer);
    }
}
