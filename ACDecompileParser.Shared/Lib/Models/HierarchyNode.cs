using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Lib.Models;

public class HierarchyNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public Dictionary<string, HierarchyNode> Children { get; set; } = new();
    public List<(string OutputFileName, List<TypeModel> Types)> Items { get; set; } = new();

    public HierarchyNode(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
    }
}
