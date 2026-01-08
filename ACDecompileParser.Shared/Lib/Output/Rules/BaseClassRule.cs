using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output.Rules;

public class BaseClassRule : IHierarchyRule
{
    private readonly string _baseTypeFqn;
    private readonly string _prefix;
    private readonly NamespaceBehavior _nsBehavior;
    private readonly int _stripCount;
    private readonly string? _fileName;

    public BaseClassRule(string baseTypeFqn, string prefix, NamespaceBehavior nsBehavior = NamespaceBehavior.KeepAll,
        int stripCount = 0, string? fileName = null)
    {
        _baseTypeFqn = baseTypeFqn;
        _prefix = prefix;
        _nsBehavior = nsBehavior;
        _stripCount = stripCount;
        _fileName = fileName;
    }

    public bool Matches(TypeModel type, IInheritanceGraph graph, out HierarchyResult result)
    {
        if (graph.IsDerivedFrom(type.Id, _baseTypeFqn))
        {
            result = new HierarchyResult(_prefix, _nsBehavior, _stripCount, _fileName);
            return true;
        }

        result = null!;
        return false;
    }
}
