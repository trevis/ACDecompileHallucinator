using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Services;

public static class InheritanceGraphBuilder
{
    public static IInheritanceGraph Build(ITypeRepository repository)
    {
        var lookupData = repository.GetTypeLookupData();
        var inheritances = repository.GetAllTypeInheritances();

        var baseTypeMap = new Dictionary<int, List<int>>();
        var fqnToIdMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var idToFqnMap = new Dictionary<int, string>();

        foreach (var (id, _, _, fqn) in lookupData)
        {
            if (string.IsNullOrEmpty(fqn)) continue;

            fqnToIdMap[fqn] = id;
            idToFqnMap[id] = fqn;
        }

        foreach (var inheritance in inheritances)
        {
            if (!baseTypeMap.TryGetValue(inheritance.ParentTypeId, out var list))
            {
                list = new List<int>();
                baseTypeMap[inheritance.ParentTypeId] = list;
            }

            if (inheritance.RelatedTypeId.HasValue)
            {
                list.Add(inheritance.RelatedTypeId.Value);
            }
        }

        return new InheritanceGraph(baseTypeMap, fqnToIdMap, idToFqnMap);
    }
}
