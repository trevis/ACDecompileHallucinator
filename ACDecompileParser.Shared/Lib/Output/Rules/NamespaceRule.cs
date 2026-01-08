using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output.Rules;

public class NamespaceRule : IHierarchyRule
{
    private readonly string _nsPrefix;
    private readonly string _targetPrefix;
    private readonly NamespaceBehavior _nsBehavior;
    private readonly string? _fileName;

    public NamespaceRule(string nsPrefix, string targetPrefix, NamespaceBehavior nsBehavior = NamespaceBehavior.KeepAll,
        string? fileName = null)
    {
        _nsPrefix = nsPrefix;
        _targetPrefix = targetPrefix;
        _nsBehavior = nsBehavior;
        _fileName = fileName;
    }

    public bool Matches(TypeModel type, IInheritanceGraph graph, out HierarchyResult result)
    {
        if (type.Namespace.StartsWith(_nsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            result = new HierarchyResult(_targetPrefix, _nsBehavior, 0, _fileName);
            return true;
        }

        result = null!;
        return false;
    }
}
