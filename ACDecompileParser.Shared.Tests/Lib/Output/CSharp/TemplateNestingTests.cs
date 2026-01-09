using Xunit;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class TemplateNestingTests
{
    [Fact]
    public void OneTemplateShouldNotBeChildOfAnotherSameTemplate()
    {
        // Arrange
        var hierarchyService = new TypeHierarchyService();

        var type1 = new TypeModel
        {
            Id = 1,
            BaseName = "IntrusiveHashTable",
            Namespace = "",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 0, TypeString = "int" }
            }
        };
        type1.StoredFullyQualifiedName = type1.FullyQualifiedName;
        type1.BaseTypePath = type1.StoredFullyQualifiedName;

        var type2 = new TypeModel
        {
            Id = 2,
            BaseName = "IntrusiveHashTable",
            Namespace = "",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument { Position = 0, TypeString = "uint" }
            }
        };
        type2.StoredFullyQualifiedName = type2.FullyQualifiedName;
        type2.BaseTypePath = type2.StoredFullyQualifiedName;

        var types = new List<TypeModel> { type1, type2 };

        // Act
        hierarchyService.LinkNestedTypes(types);

        // Assert
        Assert.Null(type1.ParentType);
        Assert.Null(type2.ParentType);
        Assert.True(type1.NestedTypes == null || !type1.NestedTypes.Any());
        Assert.True(type2.NestedTypes == null || !type2.NestedTypes.Any());
    }
}
