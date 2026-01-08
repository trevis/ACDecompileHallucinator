using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Lib.Services;

public class HierarchyTreeBuilder
{
    public HierarchyNode BuildTree(
        Dictionary<(string outputFileName, string physicalPath), List<TypeModel>> groupedTypes)
    {
        var root = new HierarchyNode("Root", "");

        foreach (var group in groupedTypes)
        {
            var (outputFileName, physicalPath) = group.Key;
            var types = group.Value;

            // Special case for "Global" or empty path
            if (string.IsNullOrEmpty(physicalPath) || physicalPath == "Global")
            {
                root.Items.Add((outputFileName, types));
                continue;
            }

            var parts = physicalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentNode = root;
            var currentPath = "";

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;
                currentNode = GetGetOrAddNode(currentNode, part, currentPath);
            }

            currentNode.Items.Add((outputFileName, types));
        }

        return root;
    }

    private HierarchyNode GetGetOrAddNode(HierarchyNode parent, string name, string? fullPath = null)
    {
        if (!parent.Children.TryGetValue(name, out var node))
        {
            node = new HierarchyNode(name, fullPath ?? name);
            parent.Children[name] = node;
        }

        return node;
    }
}
