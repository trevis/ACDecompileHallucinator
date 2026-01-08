using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Services;

public class HierarchyTreeBuilderTests
{
    [Fact]
    public void BuildTree_NestsFoldersCorrectly()
    {
        // Arrange
        var builder = new HierarchyTreeBuilder();
        var flatGroups = new Dictionary<(string outputFileName, string physicalPath), List<TypeModel>>
        {
            { ("TypeInLib", "Lib"), new List<TypeModel>() },
            { ("TypeInDats", "Lib/Dats"), new List<TypeModel>() },
            { ("TypeInDBObjs", "Lib/Dats/DBObjs"), new List<TypeModel>() },
            { ("TypeInOther", "Other"), new List<TypeModel>() }
        };

        // Act
        var root = builder.BuildTree(flatGroups);

        // Assert
        Assert.NotNull(root);

        // Check "Lib"
        Assert.True(root.Children.ContainsKey("Lib"));
        var libNode = root.Children["Lib"];
        Assert.Equal("Lib", libNode.Name);
        Assert.Equal("Lib", libNode.FullPath);
        Assert.Single(libNode.Items); // TypeInLib

        // Check "Lib/Dats" nested under "Lib"
        Assert.True(libNode.Children.ContainsKey("Dats"));
        var datsNode = libNode.Children["Dats"];
        Assert.Equal("Dats", datsNode.Name);
        Assert.Equal("Lib/Dats", datsNode.FullPath);
        Assert.Single(datsNode.Items); // TypeInDats

        // Check "Lib/Dats/DBObjs" nested under "Dats"
        Assert.True(datsNode.Children.ContainsKey("DBObjs"));
        var dbObjsNode = datsNode.Children["DBObjs"];
        Assert.Equal("DBObjs", dbObjsNode.Name);
        Assert.Equal("Lib/Dats/DBObjs", dbObjsNode.FullPath);
        Assert.Single(dbObjsNode.Items); // TypeInDBObjs

        // Check "Other" at root
        Assert.True(root.Children.ContainsKey("Other"));
        var otherNode = root.Children["Other"];
        Assert.Equal("Other", otherNode.Name);
        Assert.Empty(otherNode.Children);
        Assert.Single(otherNode.Items);
    }

    [Fact]
    public void BuildTree_HandlesRootItems()
    {
        // Arrange
        var builder = new HierarchyTreeBuilder();
        var flatGroups = new Dictionary<(string outputFileName, string physicalPath), List<TypeModel>>
        {
            { ("GlobalType", "Global"), new List<TypeModel>() },
            { ("EmptyPathType", ""), new List<TypeModel>() }
        };

        // Act
        var root = builder.BuildTree(flatGroups);

        // Assert
        Assert.Empty(root.Children);
        Assert.Equal(2, root.Items.Count);
        Assert.Contains(root.Items, i => i.OutputFileName == "GlobalType");
        Assert.Contains(root.Items, i => i.OutputFileName == "EmptyPathType");
    }
}
