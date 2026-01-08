using System;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Tests.Lib.Models;

public class TypeModelsTests
{
    public class TypeTemplateArgumentTests
    {
        [Fact]
        public void Constructor_InitializesDefaultValues()
        {
            // Act
            var templateArg = new TypeTemplateArgument();

            // Assert
            Assert.Equal(0, templateArg.Id);
            Assert.Equal(0, templateArg.ParentTypeId);
            Assert.Equal(0, templateArg.Position);
            Assert.Null(templateArg.TypeReferenceId);
            Assert.Equal(string.Empty, templateArg.TypeString);
            Assert.Null(templateArg.TypeReference);
        }

        [Fact]
        public void Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var typeReference = new TypeReference { TypeString = "int" };
            
            var templateArg = new TypeTemplateArgument
            {
                Id = 5,
                ParentTypeId = 10,
                Position = 1,
                TypeReferenceId = 20,
                TypeString = "int",
                TypeReference = typeReference
            };

            // Assert
            Assert.Equal(5, templateArg.Id);
            Assert.Equal(10, templateArg.ParentTypeId);
            Assert.Equal(20, templateArg.TypeReferenceId);
            Assert.Equal(1, templateArg.Position);
            Assert.Equal("int", templateArg.TypeString);
            Assert.Same(typeReference, templateArg.TypeReference);
        }

        [Fact]
        public void TypeReferenceId_CanBeNull()
        {
            // Arrange
            var templateArg = new TypeTemplateArgument
            {
                TypeReferenceId = null
            };

            // Assert
            Assert.Null(templateArg.TypeReferenceId);
        }

        [Fact]
        public void TypeReference_CanBeNull()
        {
            // Arrange
            var templateArg = new TypeTemplateArgument
            {
                TypeReference = null
            };

            // Assert
            Assert.Null(templateArg.TypeReference);
        }

        [Fact]
        public void RelatedTypeString_DefaultsToEmptyString()
        {
            // Act
            var templateArg = new TypeTemplateArgument();

            // Assert
            Assert.Equal(string.Empty, templateArg.TypeString);
        }
    }

    public class TypeInheritanceTests
    {
        [Fact]
        public void Constructor_InitializesDefaultValues()
        {
            // Act
            var inheritance = new TypeInheritance();

            // Assert
            Assert.Equal(0, inheritance.Id);
            Assert.Equal(0, inheritance.DerivedTypeId);
            Assert.Equal(0, inheritance.Order);
            Assert.Null(inheritance.RelatedTypeId);
            Assert.Equal(string.Empty, inheritance.RelatedTypeString);
            Assert.Null(inheritance.RelatedType);
        }

        [Fact]
        public void Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var resolvedType = new TypeModel { BaseName = "BaseType" };
            
            var inheritance = new TypeInheritance
            {
                Id = 7,
                DerivedTypeId = 15,
                Order = 0,
                RelatedTypeId = 25,
                RelatedTypeString = "BaseClass",
                RelatedType = resolvedType
            };

            // Assert
            Assert.Equal(7, inheritance.Id);
            Assert.Equal(15, inheritance.DerivedTypeId);
            Assert.Equal(25, inheritance.RelatedTypeId);
            Assert.Equal("BaseClass", inheritance.RelatedTypeString);
            Assert.Same(resolvedType, inheritance.RelatedType);
        }

        [Fact]
        public void RelatedTypeId_CanBeNull()
        {
            // Arrange
            var inheritance = new TypeInheritance
            {
                RelatedTypeId = null
            };

            // Assert
            Assert.Null(inheritance.RelatedTypeId);
        }

        [Fact]
        public void RelatedType_CanBeNull()
        {
            // Arrange
            var inheritance = new TypeInheritance
            {
                RelatedType = null
            };

            // Assert
            Assert.Null(inheritance.RelatedType);
        }

        [Fact]
        public void RelatedTypeString_DefaultsToEmptyString()
        {
            // Act
            var inheritance = new TypeInheritance();

            // Assert
            Assert.Equal(string.Empty, inheritance.RelatedTypeString);
        }

        [Fact]
        public void Inheritance_WithUnresolvedType()
        {
            // Arrange
            var inheritance = new TypeInheritance
            {
                Order = 0,
                RelatedTypeString = "UnresolvedBaseType",
                RelatedTypeId = null,
                RelatedType = null
            };

            // Assert
            Assert.Equal("UnresolvedBaseType", inheritance.RelatedTypeString);
            Assert.Null(inheritance.RelatedTypeId);
            Assert.Null(inheritance.RelatedType);
        }

        [Fact]
        public void Inheritance_WithResolvedType()
        {
            // Arrange
            var resolvedBaseType = new TypeModel
            {
                Id = 100,
                BaseName = "ResolvedBase",
                Namespace = "TestNamespace"
            };
            
            var inheritance = new TypeInheritance
            {
                Order = 0,
                RelatedTypeString = "TestNamespace::ResolvedBase",
                RelatedTypeId = 100,
                RelatedType = resolvedBaseType
            };

            // Assert
            Assert.Equal("TestNamespace::ResolvedBase", inheritance.RelatedTypeString);
            Assert.Equal(100, inheritance.RelatedTypeId);
            Assert.NotNull(inheritance.RelatedType);
            Assert.Equal("ResolvedBase", inheritance.RelatedType!.BaseName);
            Assert.Equal("TestNamespace", inheritance.RelatedType.Namespace);
        }
    }
}
