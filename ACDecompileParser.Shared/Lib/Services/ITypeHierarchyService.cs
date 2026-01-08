using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;

namespace ACDecompileParser.Shared.Lib.Services;

public interface ITypeHierarchyService
{
    Dictionary<(string outputFileName, string physicalPath), List<TypeModel>> GroupTypesByBaseNameAndNamespace(
        List<TypeModel> typeModels,
        HierarchyRuleEngine? ruleEngine = null);

    string GetRootBaseTypeName(TypeModel typeModel, List<TypeModel> allTypes);
    /// <summary>
    /// Links parent and child types by populating ParentType and NestedTypes properties.
    /// Uses BaseTypePath to determine relationships.
    /// </summary>
    void LinkNestedTypes(List<TypeModel> types);
}
