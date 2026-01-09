using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;
using ACDecompileParser.Shared.Lib.Output;

namespace ACDecompileParser.Shared.Lib.Services;

public class TypeHierarchyService : ITypeHierarchyService
{
    public Dictionary<(string outputFileName, string physicalPath), List<TypeModel>> GroupTypesByBaseNameAndNamespace(
        List<TypeModel> typeModels,
        HierarchyRuleEngine? ruleEngine = null)
    {
        var groupedTypes = new Dictionary<(string, string), List<TypeModel>>(new CaseInsensitiveTupleComparer());

        // Group all types by their base type path and the physical path calculated by rules
        foreach (var typeModel in typeModels)
        {
            string groupBaseName;
            string physicalPath;
            string? fileNameRule = null;

            // 1. Determine the logical parent/group (e.g., if this is a vtable, it belongs to its parent)
            if (!string.IsNullOrEmpty(typeModel.BaseTypePath) &&
                ParsingUtilities.ExtractBaseTypeName(typeModel.BaseTypePath) != typeModel.BaseName)
            {
                var groupBaseType = FindRootTypeForGrouping(typeModel, typeModels);

                if (groupBaseType != null)
                {
                    groupBaseName = groupBaseType.BaseName;

                    // The physical path is determined by the root type of the group
                    if (ruleEngine != null)
                    {
                        var location = ruleEngine.CalculateLocation(groupBaseType);
                        physicalPath = location.Path;
                        fileNameRule = location.FileName;
                    }
                    else
                    {
                        physicalPath = groupBaseType.Namespace.Replace("::", "/");
                    }
                }
                else
                {
                    groupBaseName = typeModel.BaseName;
                    if (ruleEngine != null)
                    {
                        var location = ruleEngine.CalculateLocation(typeModel);
                        physicalPath = location.Path;
                        fileNameRule = location.FileName;
                    }
                    else
                    {
                        physicalPath = typeModel.Namespace.Replace("::", "/");
                    }
                }
            }
            else
            {
                groupBaseName = typeModel.BaseName;
                if (ruleEngine != null)
                {
                    var location = ruleEngine.CalculateLocation(typeModel);
                    physicalPath = location.Path;
                    fileNameRule = location.FileName;
                }
                else
                {
                    physicalPath = typeModel.Namespace.Replace("::", "/");
                }
            }

            // Use rule-provided filename if available, otherwise use group base name
            string outputFileName = fileNameRule ?? groupBaseName;
            var key = (outputFileName, physicalPath);

            if (!groupedTypes.ContainsKey(key))
            {
                groupedTypes[key] = new List<TypeModel>();
            }

            groupedTypes[key].Add(typeModel);
        }

        return groupedTypes;
    }

    public string GetRootBaseTypeName(TypeModel typeModel, List<TypeModel> allTypes)
    {
        // Only follow BaseTypePath chains for types that should be grouped together
        // This includes vtables and nested types that are logically part of their parent
        if (string.IsNullOrEmpty(typeModel.BaseTypePath))
        {
            // If no BaseTypePath is set, use the base name directly
            return typeModel.BaseName;
        }

        // Check if this is a case where we should follow the chain
        // This is typically for vtables or nested types that should be grouped with their parent
        string currentBaseName = typeModel.BaseTypePath;
        var visited = new HashSet<string>();

        while (true)
        {
            // Find the type whose BaseName matches the currentBaseName (without template args)
            string currentBaseNameWithoutTemplate = ParsingUtilities.ExtractBaseTypeName(currentBaseName);
            var currentType = allTypes.FirstOrDefault(t => t.BaseName == currentBaseNameWithoutTemplate);

            if (currentType == null || string.IsNullOrEmpty(currentType.BaseTypePath))
            {
                // If we can't find the type or it has no BaseTypePath, it's the root
                break;
            }

            // Check if the type points to itself (indicating it's the root)
            string currentTypeBaseNameWithoutTemplate = ParsingUtilities.ExtractBaseTypeName(currentType.BaseTypePath);
            if (currentTypeBaseNameWithoutTemplate == currentType.BaseName)
            {
                // This is the root type
                break;
            }

            // If we've visited this name before, we have a cycle, so stop
            if (visited.Contains(currentBaseNameWithoutTemplate))
            {
                break;
            }

            visited.Add(currentBaseNameWithoutTemplate);

            // Move to the type that this type should be grouped with
            currentBaseName = currentType.BaseTypePath;
        }

        // Return the base name without template arguments
        return ParsingUtilities.ExtractBaseTypeName(currentBaseName);
    }

    private TypeModel? FindRootTypeForGrouping(TypeModel typeModel, List<TypeModel> allTypes)
    {
        // Find the root type that this type should be grouped with by traversing the BaseTypePath chain
        TypeModel currentType = typeModel;
        var visited = new HashSet<int>();

        while (!string.IsNullOrEmpty(currentType.BaseTypePath))
        {
            if (visited.Contains(currentType.Id))
            {
                // Cycle detected
                break;
            }

            visited.Add(currentType.Id);

            string targetFqn = currentType.BaseTypePath;
            string targetBaseName = ParsingUtilities.ExtractBaseTypeName(targetFqn);

            // Try to find the parent type
            // 1. Exact FQN Match (Best and most accurate)
            var parent = allTypes.FirstOrDefault(t => t.StoredFullyQualifiedName == targetFqn);

            // 2. BaseName + Namespace Match (Fallback if FQN is missing/mismatched)
            // Look for parent in the same namespace as the current type
            if (parent == null)
            {
                parent = allTypes.FirstOrDefault(t =>
                    t.BaseName == targetBaseName && t.Namespace == currentType.Namespace);
            }

            // 3. BaseName Match (Last resort fallback - can be ambiguous)
            if (parent == null)
            {
                parent = allTypes.FirstOrDefault(t => t.BaseName == targetBaseName);
            }

            if (parent == null)
            {
                // Cannot resolve parent. The chain is broken.
                // We return null so the caller uses the original type's name (or handles it as an orphan)
                return null;
            }

            // Move up to the parent
            currentType = parent;

            // Check if we found the root (no base type path or points to itself)
            if (string.IsNullOrEmpty(currentType.BaseTypePath) ||
                (currentType.BaseTypePath == currentType.StoredFullyQualifiedName) ||
                (ParsingUtilities.ExtractBaseTypeName(currentType.BaseTypePath) == currentType.BaseName))
            {
                return currentType;
            }
        }

        // If we traversed at least one step and found a type, return it.
        // If currentType == typeModel, we didn't find any parent chain, so return null.
        return currentType.Id == typeModel.Id ? null : currentType;
    }

    /// <summary>
    /// Links parent and child types by populating ParentType and NestedTypes properties.
    /// Uses BaseTypePath to determine relationships.
    /// </summary>
    public void LinkNestedTypes(List<TypeModel> types)
    {
        // Build a dictionary for quick lookup by BaseName
        var typesByBaseName = types.GroupBy(t => t.BaseName)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var type in types)
        {
            // Find the root type for grouping using existing logic
            var rootType = FindRootTypeForGrouping(type, types);

            // If this type has a different root type and that root type exists, link them
            if (rootType != null && rootType.Id != type.Id)
            {
                // Set parent relationship
                type.ParentType = rootType;

                // Add this type to the parent's NestedTypes collection
                rootType.NestedTypes ??= new List<TypeModel>();
                if (!rootType.NestedTypes.Any(nt => nt.Id == type.Id))
                {
                    rootType.NestedTypes.Add(type);
                }
            }
        }

        // Sort nested types: vtables first, then by name
        foreach (var type in types.Where(t => t.NestedTypes != null))
        {
            type.NestedTypes = type.NestedTypes!
                .OrderByDescending(nt => nt.IsVTable || nt.BaseName.EndsWith("_vtbl"))
                .ThenBy(nt => nt.BaseName)
                .ToList();
        }
    }

    private class CaseInsensitiveTupleComparer : IEqualityComparer<(string outputFileName, string physicalPath)>
    {
        public bool Equals((string outputFileName, string physicalPath) x,
            (string outputFileName, string physicalPath) y)
        {
            return string.Equals(x.outputFileName, y.outputFileName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.physicalPath, y.physicalPath, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string outputFileName, string physicalPath) obj)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (obj.outputFileName?.ToLowerInvariant().GetHashCode() ?? 0);
                hash = hash * 23 + (obj.physicalPath?.ToLowerInvariant().GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
