using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output;

public class HierarchyRuleEngine
{
    private readonly List<IHierarchyRule> _rules = new();
    private readonly IInheritanceGraph _graph;

    public HierarchyRuleEngine(IInheritanceGraph graph)
    {
        _graph = graph;
    }

    public void RegisterRule(IHierarchyRule rule)
    {
        _rules.Add(rule);
    }

    public void RegisterRules(IEnumerable<IHierarchyRule> rules)
    {
        _rules.AddRange(rules);
    }

    public (string Path, string? FileName) CalculateLocation(TypeModel type)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(type, _graph, out var result))
            {
                return (ApplyResult(type, result), result.FileName);
            }
        }

        // Default behavior: just the namespace
        return (type.Namespace.Replace("::", "/"), null);
    }

    private string ApplyResult(TypeModel type, HierarchyResult result)
    {
        string processedNamespace = "";

        switch (result.NamespaceBehavior)
        {
            case NamespaceBehavior.KeepAll:
                processedNamespace = type.Namespace;
                break;
            case NamespaceBehavior.StripAll:
                processedNamespace = "";
                break;
            case NamespaceBehavior.StripNLevels:
                var levels = type.Namespace.Split("::", StringSplitOptions.RemoveEmptyEntries);
                if (levels.Length > result.StripCount)
                {
                    processedNamespace = string.Join("/", levels.Skip(result.StripCount));
                }
                else
                {
                    processedNamespace = "";
                }

                break;
        }

        string path = result.Prefix;
        string nsPath = processedNamespace.Replace("::", "/");

        if (!string.IsNullOrEmpty(nsPath))
        {
            if (string.IsNullOrEmpty(path))
                path = nsPath;
            else
                path = $"{path.TrimEnd('/')}/{nsPath.TrimStart('/')}";
        }

        return path.Replace("\\", "/").Trim('/');
    }
}
