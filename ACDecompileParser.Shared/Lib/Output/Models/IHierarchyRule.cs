using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output.Models;

public enum NamespaceBehavior
{
    KeepAll,
    StripAll,
    StripNLevels
}

public record HierarchyResult(
    string Prefix,
    NamespaceBehavior NamespaceBehavior,
    int StripCount = 0,
    string? FileName = null);

public interface IHierarchyRule
{
    bool Matches(TypeModel type, IInheritanceGraph graph, out HierarchyResult result);
}
