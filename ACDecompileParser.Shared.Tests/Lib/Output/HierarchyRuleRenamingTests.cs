using Xunit;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Output.Rules;
using ACDecompileParser.Shared.Lib.Services;
using System.Collections.Generic;
using System.Linq;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class HierarchyRuleRenamingTests
{
    private class MockGraph : IInheritanceGraph
    {
        public bool IsDerivedFrom(int typeId, string baseTypeFqn) => false;
        public IEnumerable<int> GetBaseTypeIds(int typeId) => Enumerable.Empty<int>();
    }

    [Fact]
    public void HierarchyRule_ShouldSupportRenaming()
    {
        // Arrange
        var graph = new MockGraph();
        var engine = new HierarchyRuleEngine(graph);
        
        // Rule: types in "MyNamespace" go to "Lib/MyLib/" and are renamed to "CustomFileName"
        engine.RegisterRule(new NamespaceRule("MyNamespace", "Lib/MyLib/", NamespaceBehavior.StripAll, "CustomFileName"));

        var type = new TypeModel
        {
            Id = 1,
            BaseName = "OriginalName",
            Namespace = "MyNamespace",
            Type = TypeType.Struct
        };

        // Act
        var location = engine.CalculateLocation(type);

        // Assert
        Assert.Equal("Lib/MyLib", location.Path);
        Assert.Equal("CustomFileName", location.FileName);
    }

    [Fact]
    public void TypeHierarchyService_ShouldGroupRenamedTypesTogether()
    {
        // Arrange
        var graph = new MockGraph();
        var engine = new HierarchyRuleEngine(graph);
        
        // Rule: types starting with "Prefix" go to the same file "MergedFile"
        engine.RegisterRule(new FqnMatchRule("^Prefix", "Generated/", NamespaceBehavior.StripAll, 0, "MergedFile"));

        var service = new TypeHierarchyService();
        var types = new List<TypeModel>
        {
            new TypeModel { Id = 1, BaseName = "PrefixA", Namespace = "", Type = TypeType.Struct },
            new TypeModel { Id = 2, BaseName = "PrefixB", Namespace = "", Type = TypeType.Struct },
            new TypeModel { Id = 3, BaseName = "Other", Namespace = "", Type = TypeType.Struct }
        };

        // Act
        var result = service.GroupTypesByBaseNameAndNamespace(types, engine);

        // Assert
        // Group 1: MergedFile
        // Group 2: Other
        Assert.Equal(2, result.Count);

        var mergedKey = result.Keys.FirstOrDefault(k => k.outputFileName == "MergedFile");
        Assert.NotNull(mergedKey.outputFileName);
        Assert.Equal("Generated", mergedKey.physicalPath);
        Assert.Equal(2, result[mergedKey].Count);
        Assert.Contains(result[mergedKey], t => t.BaseName == "PrefixA");
        Assert.Contains(result[mergedKey], t => t.BaseName == "PrefixB");

        var otherKey = result.Keys.FirstOrDefault(k => k.outputFileName == "Other");
        Assert.NotNull(otherKey.outputFileName);
        Assert.Equal("", otherKey.physicalPath);
    }
}
