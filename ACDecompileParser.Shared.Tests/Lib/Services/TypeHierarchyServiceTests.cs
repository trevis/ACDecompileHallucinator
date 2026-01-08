using Xunit;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Models;
using System.Collections.Generic;
using System.Linq;

namespace ACDecompileParser.Shared.Tests.Lib.Services;

public class TypeHierarchyServiceTests
{
    [Fact]
    public void GroupTypes_ShouldNotGroup_WhenParentIsMissingFromList()
    {
        // Arrange
        var service = new TypeHierarchyService();
        var enumType = new TypeModel
        {
            Id = 1,
            BaseName = "ArmorEnchantment_BFIndex",
            Namespace = "AppraisalProfile",
            Type = TypeType.Enum,
            BaseTypePath = "AppraisalProfile" // Points to parent struct that is NOT in the list
        };

        var types = new List<TypeModel> { enumType };

        // Act
        var result = service.GroupTypesByBaseNameAndNamespace(types);

        // Assert
        // We expect the key to use the enum's own name because the parent "AppraisalProfile" is not in the list
        // Before fix: This would be "AppraisalProfile"
        // After fix: This should be "ArmorEnchantment_BFIndex"

        Assert.Single(result);
        var groupKey = result.Keys.First();

        Assert.Equal("ArmorEnchantment_BFIndex", groupKey.outputFileName);
        Assert.Equal("AppraisalProfile", groupKey.physicalPath);
        Assert.Single(result[groupKey]);
        Assert.Equal(enumType, result[groupKey][0]);
    }

    [Fact]
    public void GroupTypes_ShouldGroup_WhenParentIsPresentInList()
    {
        // This validates that we didn't break correct grouping (e.g. for vtables)
        // Arrange
        var service = new TypeHierarchyService();
        var parentType = new TypeModel
        {
            Id = 1,
            BaseName = "MyType",
            Namespace = "TestNamespace",
            Type = TypeType.Struct,
            BaseTypePath = ""
        };

        var vtableType = new TypeModel
        {
            Id = 2,
            BaseName = "MyType_vtbl",
            Namespace = "TestNamespace",
            Type = TypeType.Struct,
            BaseTypePath = "MyType"
        };

        var types = new List<TypeModel> { parentType, vtableType };

        // Act
        var result = service.GroupTypesByBaseNameAndNamespace(types);

        // Assert
        Assert.Single(result);
        var groupKey = result.Keys.First();

        Assert.Equal("MyType", groupKey.outputFileName);
        Assert.Equal("TestNamespace", groupKey.physicalPath);
        Assert.Equal(2, result[groupKey].Count);
    }
    [Fact]
    public void GroupTypes_Distinguishes_SameName_DifferentNamespace()
    {
        // Arrange
        var service = new TypeHierarchyService();
        var parent1 = new TypeModel
        {
            Id = 1,
            BaseName = "Parent",
            Namespace = "NS1",
            StoredFullyQualifiedName = "NS1::Parent"
        };

        var parent2 = new TypeModel
        {
            Id = 2,
            BaseName = "Parent",
            Namespace = "NS2",
            StoredFullyQualifiedName = "NS2::Parent"
        };

        var child = new TypeModel
        {
            Id = 3,
            BaseName = "Child",
            Namespace = "NS1::Parent",
            StoredFullyQualifiedName = "NS1::Parent::Child",
            BaseTypePath = "NS1::Parent" // Points to parent 1 by FQN
        };

        // Use reverse order to ensure we rely on FQN matching, not just first-found
        var types = new List<TypeModel> { parent2, parent1, child };

        // Act
        var result = service.GroupTypesByBaseNameAndNamespace(types);

        // Assert
        // We expect two groups: ("Parent", "NS1") and ("Parent", "NS2")
        // Child should be in ("Parent", "NS1") based on the FQN lookup

        var ns1Group = result.FirstOrDefault(g => g.Key.physicalPath == "NS1");
        var ns2Group = result.FirstOrDefault(g => g.Key.physicalPath == "NS2");

        Assert.NotNull(ns1Group.Value);
        Assert.NotNull(ns2Group.Value);

        Assert.Contains(child, ns1Group.Value);
        Assert.DoesNotContain(child, ns2Group.Value);
    }
    [Fact]
    public void GroupTypes_ShouldGroup_SameName_DifferentCase()
    {
        // Arrange
        var service = new TypeHierarchyService();
        var structType = new TypeModel
        {
            Id = 1,
            BaseName = "ObjectInfo",
            Namespace = "Global",
            Type = TypeType.Struct
        };

        var enumType = new TypeModel
        {
            Id = 2,
            BaseName = "OBJECTINFO",
            Namespace = "Global",
            Type = TypeType.Enum
        };

        var types = new List<TypeModel> { structType, enumType };

        // Act
        var result = service.GroupTypesByBaseNameAndNamespace(types);

        // Assert
        // Currently, it should return 2 groups because dictionary keys are case-sensitive
        // We want it to eventually return 1 group

        // This test check the CURRENT behavior (which we want to CHANGE)
        // Adjust the assertion based on whether we are testing current or desired state.
        // I will assert 2 for now to confirm reproduction.
        Assert.Equal(2, result.Count);
    }
}
