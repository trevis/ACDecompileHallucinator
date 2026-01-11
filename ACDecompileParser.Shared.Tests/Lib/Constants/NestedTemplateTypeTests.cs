using ACDecompileParser.Shared.Lib.Constants;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

/// <summary>
/// Tests for nested types within template instantiations.
/// </summary>
public class NestedTemplateTypeTests
{
    [Theory]
    [InlineData("QTIsaac<8,unsigned long>::randctx", $"{CSharpBindingsGenerator.NAMESPACE}.QTIsaac__uint.randctx")]
    [InlineData("DArray<int>::iterator", $"{CSharpBindingsGenerator.NAMESPACE}.DArray__int.iterator")]
    [InlineData("Map<string,int>::Node", $"{CSharpBindingsGenerator.NAMESPACE}.Map___string__int.Node")]
    [InlineData("Outer<T>::Inner::Deep", $"{CSharpBindingsGenerator.NAMESPACE}.Outer___T.Inner.Deep")]
    public void MapType_NestedTypeWithinTemplateInstantiation_PreservesNestedTypeSuffix(string cppType,
        string expectedCsType)
    {
        // Act
        var result = PrimitiveTypeMappings.MapType(cppType);

        // Assert
        Assert.Equal(expectedCsType, result);
    }

    [Fact]
    public void MapType_TemplateWithoutNestedType_ReturnsJustTemplateType()
    {
        // Arrange
        const string cppType = "QTIsaac<8,unsigned long>";

        // Act
        var result = PrimitiveTypeMappings.MapType(cppType);

        // Assert
        Assert.Equal($"{CSharpBindingsGenerator.NAMESPACE}.QTIsaac__uint", result);
    }

    [Fact]
    public void MapType_NestedTypePointer_PreservesNestedTypeSuffix()
    {
        // Arrange
        const string cppType = "QTIsaac<8,unsigned long>::randctx*";

        // Act
        var result = PrimitiveTypeMappings.MapType(cppType);

        // Assert
        Assert.Equal($"{CSharpBindingsGenerator.NAMESPACE}.QTIsaac__uint.randctx*", result);
    }
}
