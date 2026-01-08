using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;
using System.Text.RegularExpressions;

namespace ACDecompileParser.Shared.Lib.Output.Rules;

public class FqnMatchRule : IHierarchyRule
{
    private readonly string _targetFqnPattern;
    private readonly string _prefix;
    private readonly NamespaceBehavior _nsBehavior;
    private readonly int _stripCount;
    private readonly Regex _regex;
    private readonly string? _fileName;

    public FqnMatchRule(string targetFqnPattern, string prefix,
        NamespaceBehavior nsBehavior = NamespaceBehavior.KeepAll, int stripCount = 0, string? fileName = null)
    {
        _targetFqnPattern = targetFqnPattern;
        _prefix = prefix;
        _nsBehavior = nsBehavior;
        _stripCount = stripCount;
        _fileName = fileName;

        // Use the pattern directly as a regex (allowing full regex syntax)
        _regex = new Regex(_targetFqnPattern, RegexOptions.IgnoreCase);
    }

    public bool Matches(TypeModel type, IInheritanceGraph graph, out HierarchyResult result)
    {
        if (_regex.IsMatch(type.FullyQualifiedName))
        {
            result = new HierarchyResult(_prefix, _nsBehavior, _stripCount, _fileName);
            return true;
        }

        result = null!;
        return false;
    }
}
