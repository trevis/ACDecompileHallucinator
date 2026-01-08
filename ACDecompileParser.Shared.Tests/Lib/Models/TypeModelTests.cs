using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Tests.Lib.Models;

public class TypeModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var typeModel = new TypeModel();

        // Assert
        Assert.Equal(0, typeModel.Id);
        Assert.Equal(string.Empty, typeModel.BaseName);
        Assert.Equal(string.Empty, typeModel.Namespace);
        Assert.Equal(TypeType.Unknown, typeModel.Type);
        Assert.Equal(string.Empty, typeModel.Source);
        Assert.Empty(typeModel.TemplateArguments);
        Assert.Empty(typeModel.BaseTypes);
    }

    [Fact]
    public void IsGeneric_Property_ReturnsCorrectValue()
    {
        // Arrange
        var nonGeneric = new TypeModel();
        var generic = new TypeModel
        {
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 0, TypeString = "int" }
            }
        };

        // Assert
        Assert.False(nonGeneric.IsGeneric);
        Assert.True(generic.IsGeneric);
    }

    [Fact]
    public void IsGeneric_Property_WithMultipleArguments_ReturnsTrue()
    {
        // Arrange
        var generic = new TypeModel
        {
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 0, TypeString = "int" },
                new TypeTemplateArgument { Position = 1, TypeString = "string" }
            }
        };

        // Assert
        Assert.True(generic.IsGeneric);
    }

    [Fact]
    public void IsGeneric_Property_WithEmptyArguments_ReturnsFalse()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            TemplateArguments = new List<TypeTemplateArgument>()
        };

        // Assert
        Assert.False(typeModel.IsGeneric);
    }

    [Fact]
    public void NameWithTemplates_Property_ReturnsBaseNameForNonGeneric()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "MyType"
        };

        // Assert
        Assert.Equal("MyType", typeModel.NameWithTemplates);
    }

    [Fact]
    public void NameWithTemplates_Property_ReturnsTemplateNameForGeneric()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "Vector",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 0, TypeString = "int" }
            }
        };

        // Assert
        Assert.Equal("Vector<int>", typeModel.NameWithTemplates);
    }

    [Fact]
    public void NameWithTemplates_Property_ReturnsMultipleTemplateArgsCorrectly()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "Map",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 0, TypeString = "string" },
                new TypeTemplateArgument { Position = 1, TypeString = "int" }
            }
        };

        // Assert
        Assert.Equal("Map<string,int>", typeModel.NameWithTemplates);
    }

    [Fact]
    public void NameWithTemplates_Property_SortsArgumentsByPosition()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "Container",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 1, TypeString = "int" },
                new TypeTemplateArgument { Position = 0, TypeString = "string" }
            }
        };

        // Assert
        Assert.Equal("Container<string,int>", typeModel.NameWithTemplates);
    }

    [Fact]
    public void NameWithTemplates_Property_HandlesResolvedTypes()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "Vector",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument 
                { 
                    Position = 0, 
                    TypeString = "int",
                    TypeReference = new TypeReference { TypeString = "int" }
                }
            }
        };

        // Assert
        Assert.Equal("Vector<int>", typeModel.NameWithTemplates);
    }

    [Fact]
    public void NameWithTemplates_Property_HandlesResolvedTypesWithNamespace()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "Vector",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument 
                { 
                    Position = 0, 
                    TypeString = "std::string",
                    TypeReference = new TypeReference { TypeString = "std::string" }
                }
            }
        };

        // Assert
        Assert.Equal("Vector<std::string>", typeModel.NameWithTemplates);
    }

    [Fact]
    public void FullyQualifiedName_Property_ReturnsBaseNameWithoutNamespace()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "MyType"
        };

        // Assert
        Assert.Equal("MyType", typeModel.FullyQualifiedName);
    }

    [Fact]
    public void FullyQualifiedName_Property_ReturnsNamespaceAndBaseName()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "MyType",
            Namespace = "MyNamespace"
        };

        // Assert
        Assert.Equal("MyNamespace::MyType", typeModel.FullyQualifiedName);
    }

    [Fact]
    public void FullyQualifiedName_Property_IncludesTemplateArgs()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            BaseName = "Vector",
            Namespace = "std",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 0, TypeString = "int" }
            }
        };

        // Assert
        Assert.Equal("std::Vector<int>", typeModel.FullyQualifiedName);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var typeModel = new TypeModel
        {
            Id = 42,
            BaseName = "TestClass",
            Namespace = "TestNamespace",
            Type = TypeType.Class,
            Source = "class TestClass { };"
        };

        // Assert
        Assert.Equal(42, typeModel.Id);
        Assert.Equal("TestClass", typeModel.BaseName);
        Assert.Equal("TestNamespace", typeModel.Namespace);
        Assert.Equal(TypeType.Class, typeModel.Type);
        Assert.Equal("class TestClass { };", typeModel.Source);
    }

    [Fact]
    public void TemplateArguments_CanBeSetAndRetrieved()
    {
        // Arrange
        var templateArgs = new List<TypeTemplateArgument>
        {
            new TypeTemplateArgument { Position = 0, TypeString = "T" },
            new TypeTemplateArgument { Position = 1, TypeString = "U" }
        };
        
        var typeModel = new TypeModel
        {
            TemplateArguments = templateArgs
        };

        // Assert
        Assert.Equal(2, typeModel.TemplateArguments.Count);
        Assert.Equal("T", typeModel.TemplateArguments[0].TypeString);
        Assert.Equal("U", typeModel.TemplateArguments[1].TypeString);
    }

    [Fact]
    public void BaseTypes_CanBeSetAndRetrieved()
    {
        // Arrange
        var baseTypes = new List<TypeInheritance>
        {
            new TypeInheritance { RelatedTypeString = "Base1" },
            new TypeInheritance { RelatedTypeString = "Base2" }
        };
        
        var typeModel = new TypeModel
        {
            BaseTypes = baseTypes
        };

        // Assert
        Assert.Equal(2, typeModel.BaseTypes.Count);
        Assert.Equal("Base1", typeModel.BaseTypes[0].RelatedTypeString);
        Assert.Equal("Base2", typeModel.BaseTypes[1].RelatedTypeString);
}
    }
