using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Utilities;

public class TypeReferenceUtilitiesTests
{
    [Fact]
    public void CreateTypeReference_WithNamespaceAndTemplateArgs_SetsFullyQualifiedTypeCorrectly()
    {
        // Arrange
        var typeString = "MyNamespace::MyTemplate<int, double>";

        // Act
        var typeReference = TypeReferenceUtilities.CreateTypeReference(typeString);

        // Assert
        Assert.Equal("MyNamespace::MyTemplate<int,double>", typeReference.FullyQualifiedType);
        Assert.Equal("MyNamespace::MyTemplate<int, double>", typeReference.TypeString);
    }

    [Fact]
    public void CreateTypeReference_WithConstPointer_SetsFullyQualifiedTypeCorrectly()
    {
        // Arrange
        var typeString = "const MyNamespace::MyTemplate<int>*";

        // Act
        var typeReference = TypeReferenceUtilities.CreateTypeReference(typeString);

        // Assert
        Assert.Equal("MyNamespace::MyTemplate<int>", typeReference.FullyQualifiedType);
        Assert.Equal("const MyNamespace::MyTemplate<int>*", typeReference.TypeString);
        Assert.True(typeReference.IsConst);
        Assert.True(typeReference.IsPointer);
    }

    [Fact]
    public void CreateTypeReference_WithNestedTemplateArgs_SetsFullyQualifiedTypeCorrectly()
    {
        // Arrange
        var typeString = "std::vector<std::pair<int, std::string>>";

        // Act
        var typeReference = TypeReferenceUtilities.CreateTypeReference(typeString);

        // Assert
        Assert.Equal("std::vector<std::pair<int,std::string>>", typeReference.FullyQualifiedType);
        Assert.Equal("std::vector<std::pair<int, std::string>>", typeReference.TypeString);
    }

    [Fact]
    public void CreateTypeReference_WithSimpleType_SetsFullyQualifiedTypeCorrectly()
    {
        // Arrange
        var typeString = "int";

        // Act
        var typeReference = TypeReferenceUtilities.CreateTypeReference(typeString);

        // Assert
        Assert.Equal("int", typeReference.FullyQualifiedType);
        Assert.Equal("int", typeReference.TypeString);
    }

    [Fact]
    public void CreateTypeReference_WithNamespaceOnly_SetsFullyQualifiedTypeCorrectly()
    {
        // Arrange
        var typeString = "MyNamespace::MyType";

        // Act
        var typeReference = TypeReferenceUtilities.CreateTypeReference(typeString);

        // Assert
        Assert.Equal("MyNamespace::MyType", typeReference.FullyQualifiedType);
        Assert.Equal("MyNamespace::MyType", typeReference.TypeString);
    }

    [Fact]
    public void CreateTypeReference_WithPointerAndConstAtEnd_SetsCorrectly()
    {
        // Arrange
        var typeString = "IUnknown *const";

        // Act
        var typeReference = TypeReferenceUtilities.CreateTypeReference(typeString);

        // Assert
        Assert.Equal("IUnknown", typeReference.FullyQualifiedType);
        Assert.True(typeReference.IsConst);
        Assert.True(typeReference.IsPointer);
        Assert.Equal(1, typeReference.PointerDepth);
    }

    [Fact]
    public void CreateTypeReference_WithConstBetweenTypeAndPointer_SetsCorrectly()
    {
        // Arrange
        var typeString = "int const *";

        // Act
        var typeReference = TypeReferenceUtilities.CreateTypeReference(typeString);

        // Assert
        Assert.Equal("int", typeReference.FullyQualifiedType);
        Assert.True(typeReference.IsConst);
        Assert.True(typeReference.IsPointer);
        Assert.Equal(1, typeReference.PointerDepth);
    }
}
