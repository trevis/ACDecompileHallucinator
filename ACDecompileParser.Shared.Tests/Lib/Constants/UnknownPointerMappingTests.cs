using ACDecompileParser.Shared.Lib.Constants;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

public class UnknownPointerMappingTests
{
    [Fact]
    public void MapType_WithKnownPointerType_ReturnsTypedPointer()
    {
        // Arrange: TypeReference with a valid ReferencedTypeId (parsed type)
        var typeRef = new TypeReference
        {
            TypeString = "SomeType*",
            IsPointer = true,
            PointerDepth = 1,
            ReferencedTypeId = 123, // Non-null means it was parsed
            ReferencedType = new TypeModel
            {
                Id = 123,
                BaseName = "SomeType",
                IsIgnored = false
            }
        };

        // Act
        var result = PrimitiveTypeMappings.MapType("SomeType*", typeRef);

        // Assert: Should return typed pointer
        Assert.Equal($"{CSharpBindingsGenerator.NAMESPACE}.SomeType*", result);
    }

    [Fact]
    public void MapType_WithUnknownPointerType_ReturnsIntPtr()
    {
        // Arrange: TypeReference where resolution was attempted but type not found
        // We indicate resolution was attempted by setting ReferencedType to a placeholder
        // (in real scenarios, the repository would set this when loading from DB)
        var typeRef = new TypeReference
        {
            TypeString = "UnknownType*",
            IsPointer = true,
            PointerDepth = 1,
            ReferencedTypeId = null, // Null means type wasn't found
            // Setting ReferencedType to a placeholder indicates resolution was attempted
            // In production, this would be set by the repository when loading TypeReferences
            ReferencedType = new TypeModel { Id = -1, BaseName = "Unknown", IsIgnored = false }
        };

        // Act
        var result = PrimitiveTypeMappings.MapType("UnknownType*", typeRef);

        // Assert: Should return System.IntPtr for unknown types
        Assert.Equal("System.IntPtr", result);
    }

    [Fact]
    public void MapType_WithIgnoredPointerType_ReturnsIntPtr()
    {
        // Arrange: TypeReference with IsIgnored = true
        var typeRef = new TypeReference
        {
            TypeString = "IgnoredType*",
            IsPointer = true,
            PointerDepth = 1,
            ReferencedTypeId = 456,
            ReferencedType = new TypeModel
            {
                Id = 456,
                BaseName = "IgnoredType",
                IsIgnored = true // Marked as ignored
            }
        };

        // Act
        var result = PrimitiveTypeMappings.MapType("IgnoredType*", typeRef);

        // Assert: Should return System.IntPtr for ignored types
        Assert.Equal("System.IntPtr", result);
    }

    [Fact]
    public void MapType_WithVoidPointer_AlwaysReturnsIntPtr()
    {
        // Arrange
        var typeRef = new TypeReference
        {
            TypeString = "void*",
            IsPointer = true,
            PointerDepth = 1,
            ReferencedTypeId = null
        };

        // Act
        var result = PrimitiveTypeMappings.MapType("void*", typeRef);

        // Assert: void* should always map to System.IntPtr
        Assert.Equal("System.IntPtr", result);
    }

    [Fact]
    public void MapType_WithoutTypeReference_UsesDefaultBehavior()
    {
        // Act: Call without TypeReference (backward compatibility)
        var result = PrimitiveTypeMappings.MapType("SomeType*");

        // Assert: Should use default behavior (typed pointer)
        Assert.Equal($"{CSharpBindingsGenerator.NAMESPACE}.SomeType*", result);
    }

    [Fact]
    public void MapType_WithNonPointerType_IgnoresTypeReference()
    {
        // Arrange: Non-pointer type
        var typeRef = new TypeReference
        {
            TypeString = "int",
            IsPointer = false,
            ReferencedTypeId = null
        };

        // Act
        var result = PrimitiveTypeMappings.MapType("int", typeRef);

        // Assert: Should map normally (not affected by TypeReference for non-pointers)
        Assert.Equal("int", result);
    }

    [Fact]
    public void MapTypeForStaticPointer_WithUnknownType_ReturnsIntPtr()
    {
        // Arrange
        var typeRef = new TypeReference
        {
            TypeString = "UnknownType*",
            IsPointer = true,
            PointerDepth = 1,
            ReferencedTypeId = null
        };

        // Act
        var result = PrimitiveTypeMappings.MapTypeForStaticPointer("UnknownType*", typeRef);

        // Assert
        Assert.Equal("System.IntPtr", result);
    }

    [Fact]
    public void MapTypeForStaticPointer_WithKnownType_ReturnsTypedPointer()
    {
        // Arrange
        var typeRef = new TypeReference
        {
            TypeString = "KnownType*",
            IsPointer = true,
            PointerDepth = 1,
            ReferencedTypeId = 789,
            ReferencedType = new TypeModel
            {
                Id = 789,
                BaseName = "KnownType",
                IsIgnored = false
            }
        };

        // Act
        var result = PrimitiveTypeMappings.MapTypeForStaticPointer("KnownType*", typeRef);

        // Assert
        Assert.Equal($"{CSharpBindingsGenerator.NAMESPACE}.KnownType*", result);
    }
}
