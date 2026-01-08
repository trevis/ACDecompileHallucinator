using System.Collections.Generic;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Lib.Services;

/// <summary>
/// A lightweight representation of the type inheritance tree for fast rule matching.
/// </summary>
public interface IInheritanceGraph
{
    bool IsDerivedFrom(int typeId, string baseTypeFqn);
    IEnumerable<int> GetBaseTypeIds(int typeId);
}

public class InheritanceGraph : IInheritanceGraph
{
    private readonly Dictionary<int, List<int>> _baseTypeMap;
    private readonly Dictionary<string, int> _fqnToIdMap;
    private readonly Dictionary<int, string> _idToFqnMap;

    public InheritanceGraph(
        Dictionary<int, List<int>> baseTypeMap, 
        Dictionary<string, int> fqnToIdMap,
        Dictionary<int, string> idToFqnMap)
    {
        _baseTypeMap = baseTypeMap;
        _fqnToIdMap = fqnToIdMap;
        _idToFqnMap = idToFqnMap;
    }

    public bool IsDerivedFrom(int typeId, string baseTypeFqn)
    {
        if (!_fqnToIdMap.TryGetValue(baseTypeFqn, out var baseTypeId))
            return false;

        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(typeId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (currentId == baseTypeId) return true;

            if (visited.Contains(currentId)) continue;
            visited.Add(currentId);

            if (_baseTypeMap.TryGetValue(currentId, out var bases))
            {
                foreach (var b in bases)
                {
                    queue.Enqueue(b);
                }
            }
        }

        return false;
    }

    public IEnumerable<int> GetBaseTypeIds(int typeId)
    {
        if (_baseTypeMap.TryGetValue(typeId, out var bases))
            return bases;
        return Enumerable.Empty<int>();
    }
}
