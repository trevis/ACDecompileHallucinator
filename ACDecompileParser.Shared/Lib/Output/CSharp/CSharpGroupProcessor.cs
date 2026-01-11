using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Shared.Lib.Output.CSharp;

/// <summary>
/// Processes groups of types to generate C# binding code.
/// Similar to TypeGroupProcessor but for C# output.
/// </summary>
public class CSharpGroupProcessor
{
    private readonly ITypeRepository? _repository;
    private readonly TypeLookupCache? _lookupCache;
    private readonly ITypeHierarchyService _hierarchyService;
    private readonly CSharpBindingsGenerator _generator;

    public CSharpGroupProcessor(ITypeRepository? repository = null, TypeLookupCache? lookupCache = null,
        ITypeHierarchyService? hierarchyService = null)
    {
        _repository = repository;
        _lookupCache = lookupCache;
        _hierarchyService = hierarchyService ?? new TypeHierarchyService();
        _generator = new CSharpBindingsGenerator(repository);
    }

    /// <summary>
    /// Generates C# binding code for a group of types as a string.
    /// </summary>
    public string GenerateGroupContent(List<TypeModel> types, bool includeNamespace = true, bool preloadData = true)
    {
        if (types == null || !types.Any())
            return string.Empty;

        // Link parent/child relationships for nested type output
        _hierarchyService.LinkNestedTypes(types);

        // Pre-load all data in batch if requested and we have a repository
        if (preloadData && _repository != null)
        {
            var allTypes = new List<TypeModel>(types);
            foreach (var type in types)
            {
                CollectNestedTypes(type, allTypes);
            }

            var allTypeIds = allTypes.Select(t => t.Id).Distinct().ToList();

            var allBaseTypes = _repository.GetBaseTypesForMultipleTypes(allTypeIds);
            var allMembers = _repository.GetStructMembersForMultipleTypes(allTypeIds);
            var allBodies = _repository.GetFunctionBodiesForMultipleTypes(allTypeIds);
            var allStaticVariables = _repository.GetStaticVariablesForMultipleTypes(allTypeIds);

            // Attach to ALL types so generators find pre-loaded data
            foreach (var type in allTypes)
            {
                if (allBaseTypes.TryGetValue(type.Id, out var baseTypes))
                    type.BaseTypes = baseTypes;
                else if (type.BaseTypes == null || !type.BaseTypes.Any())
                    type.BaseTypes = new List<TypeInheritance>();

                if (allMembers.TryGetValue(type.Id, out var members))
                    type.StructMembers = members;
                else if (type.StructMembers == null || !type.StructMembers.Any())
                    type.StructMembers = new List<StructMemberModel>();

                if (allBodies.TryGetValue(type.Id, out var bodies))
                    type.FunctionBodies = bodies;
                else if (type.FunctionBodies == null || !type.FunctionBodies.Any())
                    type.FunctionBodies = new List<FunctionBodyModel>();

                if (allStaticVariables.TryGetValue(type.Id, out var staticVars))
                    type.StaticVariables = staticVars;
                else if (type.StaticVariables == null || !type.StaticVariables.Any())
                    type.StaticVariables = new List<StaticVariableModel>();
            }
        }

        if (includeNamespace)
        {
            return _generator.GenerateWithNamespace(types);
        }
        else
        {
            var sb = new System.Text.StringBuilder();
            foreach (var type in types.Where(t => t.ParentType == null))
            {
                sb.AppendLine(_generator.Generate(type));
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Recursively collects all nested types from a type hierarchy for batch loading.
    /// </summary>
    private void CollectNestedTypes(TypeModel type, List<TypeModel> allTypes)
    {
        if (type.NestedTypes == null) return;

        foreach (var nested in type.NestedTypes)
        {
            if (!allTypes.Contains(nested))
            {
                allTypes.Add(nested);
                CollectNestedTypes(nested, allTypes);
            }
        }
    }

    /// <summary>
    /// Generates C# binding code for a single type.
    /// </summary>
    public string GenerateTypeContent(TypeModel type)
    {
        if (type == null)
            return string.Empty;

        // Load related data for the type
        if (_repository != null)
        {
            if (type.BaseTypes == null)
            {
                var baseTypes = _repository.GetBaseTypesForMultipleTypes(new[] { type.Id });
                if (baseTypes.TryGetValue(type.Id, out var bt))
                    type.BaseTypes = bt;
                else
                    type.BaseTypes = new List<TypeInheritance>();
            }

            if (type.StructMembers == null)
            {
                var members = _repository.GetStructMembersForMultipleTypes(new[] { type.Id });
                if (members.TryGetValue(type.Id, out var m))
                    type.StructMembers = m;
                else
                    type.StructMembers = new List<StructMemberModel>();
            }

            if (type.FunctionBodies == null)
            {
                var bodies = _repository.GetFunctionBodiesForMultipleTypes(new[] { type.Id });
                if (bodies.TryGetValue(type.Id, out var fb))
                    type.FunctionBodies = fb;
                else
                    type.FunctionBodies = new List<FunctionBodyModel>();
            }

            if (type.StaticVariables == null)
            {
                type.StaticVariables = _repository.GetStaticVariablesForType(type.Id);
            }
        }

        return _generator.Generate(type);
    }

    /// <summary>
    /// Populates XmlDocComment properties for all types and their methods using the provided provider.
    /// </summary>
    public async Task PopulateCommentsAsync(List<TypeModel> types, ICommentProvider commentProvider)
    {
        foreach (var type in types)
        {
            if (type.Type == TypeType.Enum)
            {
                type.XmlDocComment = await commentProvider.GetEnumCommentAsync(type.StoredFullyQualifiedName);
            }
            else
            {
                type.XmlDocComment = await commentProvider.GetStructCommentAsync(type.StoredFullyQualifiedName);
            }

            if (type.FunctionBodies != null)
            {
                foreach (var fb in type.FunctionBodies)
                {
                    fb.XmlDocComment = await commentProvider.GetMethodCommentAsync(fb.FullyQualifiedName);
                }
            }

            // Recurse into nested types if they exist
            if (type.NestedTypes != null)
            {
                await PopulateCommentsAsync(type.NestedTypes, commentProvider);
            }
        }
    }
}
