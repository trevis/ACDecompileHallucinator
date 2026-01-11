using ACDecompileParser.Shared.Lib.Constants;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

public class IgnoredTypeTests
{
    [Fact]
    public void MapType_WithIgnoredTypePointer_ReturnsIntPtr()
    {
        // Arrange
        var ignoredType = new TypeModel
        {
            BaseName = "IDirect3DSurface9",
            IsIgnored = true
        };

        var typeRef = new TypeReference
        {
            IsPointer = true,
            ReferencedTypeId = 123,
            ReferencedType = ignoredType
        };

        // Act
        var result = PrimitiveTypeMappings.MapType("IDirect3DSurface9*", typeRef);

        // Assert
        Assert.Equal("System.IntPtr", result);
    }

    [Fact]
    public void MapType_WithUnknownTypePointer_ReturnsIntPtr()
    {
        // Arrange
        var typeRef = new TypeReference
        {
            IsPointer = true,
            ReferencedTypeId = null, // Unknown type ID
            ReferencedType = null
        };

        // Act
        var result = PrimitiveTypeMappings.MapType("SomeUnknownType*", typeRef);

        // Assert
        Assert.Equal("System.IntPtr", result);
    }

    [Fact]
    public void MapType_WithNonIgnoredTypePointer_ReturnsTypedPointer()
    {
        // Arrange
        var normalType = new TypeModel
        {
            BaseName = "Vector3",
            IsIgnored = false
        };

        var typeRef = new TypeReference
        {
            IsPointer = true,
            ReferencedTypeId = 456,
            ReferencedType = normalType
        };

        // Act
        var result = PrimitiveTypeMappings.MapType("Vector3*", typeRef);

        // Assert
        Assert.Equal($"{CSharpBindingsGenerator.NAMESPACE}.Vector3*", result);
    }
}
