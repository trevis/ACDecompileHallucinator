using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;
using System.Text.RegularExpressions;

namespace ACDecompileParser.Shared.Lib.Output.Rules;

public class MemberNameRule : IHierarchyRule
{
    private readonly string _memberNamePattern;
    private readonly string _prefix;
    private readonly NamespaceBehavior _nsBehavior;
    private readonly int _stripCount;
    private readonly Regex _regex;
    private readonly string? _fileName;

    public MemberNameRule(string memberNamePattern, string prefix,
        NamespaceBehavior nsBehavior = NamespaceBehavior.KeepAll, int stripCount = 0, string? fileName = null)
    {
        _memberNamePattern = memberNamePattern;
        _prefix = prefix;
        _nsBehavior = nsBehavior;
        _stripCount = stripCount;
        _fileName = fileName;

        // Use the pattern directly as a regex (allowing full regex syntax)
        _regex = new Regex(_memberNamePattern, RegexOptions.IgnoreCase);
    }

    public bool Matches(TypeModel type, IInheritanceGraph graph, out HierarchyResult result)
    {
        // Only process struct types for member name matching
        if (type.Type == TypeType.Struct && !string.IsNullOrEmpty(type.Source))
        {
            // Try to find member names in the source code
            var memberMatches = FindMemberNamesInSource(type.Source);
            foreach (var memberName in memberMatches)
            {
                if (_regex.IsMatch(memberName))
                {
                    result = new HierarchyResult(_prefix, _nsBehavior, _stripCount, _fileName);
                    return true;
                }
            }
        }

        result = null!;
        return false;
    }

    private List<string> FindMemberNamesInSource(string source)
    {
        var memberNames = new List<string>();

        // This is a simplified approach to extract member names from source
        // A more robust implementation would use proper C++ parsing
        var lines = source.Split('\n');

        foreach (var line in lines)
        {
            // Look for member declarations in the form of "type memberName;" or "type memberName[optional array];"
            // Match patterns like: "int memberName;" or "SomeType* memberName;" or "int memberName[10];"
            var fieldMatch = Regex.Match(line,
                @"\b([a-zA-Z_][a-zA-Z0-9_*&<>:]*\s+)([a-zA-Z_][a-zA-Z0-9_]*)\s*(\[.*?\])?\s*;");
            if (fieldMatch.Success && fieldMatch.Groups.Count > 2 && !string.IsNullOrEmpty(fieldMatch.Groups[2].Value))
            {
                var memberName = fieldMatch.Groups[2].Value.Trim();
                memberNames.Add(memberName);
            }

            // Look for method declarations like "ReturnType methodName(...);"
            var methodMatch = Regex.Match(line,
                @"\b([a-zA-Z_][a-zA-Z0-9_*&<>:]*\s+)(~?[a-zA-Z_][a-zA-Z0-9_]*)\s*\([^)]*\)\s*;");
            if (methodMatch.Success && methodMatch.Groups.Count > 2 &&
                !string.IsNullOrEmpty(methodMatch.Groups[2].Value))
            {
                var methodName = methodMatch.Groups[2].Value.Trim();
                memberNames.Add(methodName);
            }

            // Look for function pointer declarations like "ReturnType (__thiscall *funcName)(...);"
            var funcPtrMatch = Regex.Match(line,
                @"\(\s*(?:[a-zA-Z_][a-zA-Z0-9_]*\s+)*\*\s*(~?[a-zA-Z_][a-zA-Z0-9_]*)\s*\)\s*\([^)]*\)");
            if (funcPtrMatch.Success && funcPtrMatch.Groups.Count > 1 &&
                !string.IsNullOrEmpty(funcPtrMatch.Groups[1].Value))
            {
                var funcPtrName = funcPtrMatch.Groups[1].Value.Trim();
                memberNames.Add(funcPtrName);
            }
        }

        return memberNames.Distinct().ToList();
    }
}
