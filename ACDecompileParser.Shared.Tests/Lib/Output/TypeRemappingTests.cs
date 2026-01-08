using ACDecompileParser.Shared.Lib.Services;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class TypeRemappingTests
{
    private readonly TypeRemappingService _service = new();

    [Fact]
    public void Remap_SimpleType_ReplacesCorrectly()
    {
        // Arrange
        string input = "_BYTE";
        string expected = "byte";

        // Act
        string result = _service.RemapTypeString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Remap_MultiWord_ReplacesCorrectly()
    {
        // Arrange
        string input = "unsigned int";
        string expected = "uint32_t";

        // Act
        string result = _service.RemapTypeString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Remap_Pointer_ReplacesCorrectly()
    {
        // Arrange
        string input = "_BYTE*";
        string expected = "byte*";

        // Act
        string result = _service.RemapTypeString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Remap_Template_ReplacesCorrectly()
    {
        // Arrange
        string input = "ACArray<unsigned int>";
        string expected = "ACArray<uint32_t>";

        // Act
        string result = _service.RemapTypeString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Remap_MultipleTypes_ReplacesAll()
    {
        // Arrange
        string input = "Map<_DWORD, unsigned int>";
        string expected = "Map<uint32_t, uint32_t>";

        // Act
        string result = _service.RemapTypeString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Remap_PartialMatchSafety_DoesNotReplacePartialWords()
    {
        // Arrange
        string input = "_BYTES_RECEIVED";
        string expected = "_BYTES_RECEIVED"; // Should NOT become byteS_RECEIVED

        // Act
        string result = _service.RemapTypeString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Remap_OrderSafety_HandlesOverlappingPrefixes()
    {
        // Arrange
        // If "int" was replaced before "unsigned int", we might get "unsigned int32_t" (if int -> int32_t)
        // or other corruption. Tests that longest match wins.
        // Assuming we have mappings for "unsigned int" -> "uint32_t" and "int" -> "int32_t" (if configured)
        // Since default config has "unsigned int", let's test that specifically.

        string input = "unsigned int";
        string expected = "uint32_t";

        // Act
        string result = _service.RemapTypeString(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
