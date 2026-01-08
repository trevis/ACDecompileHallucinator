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
    public string GenerateGroupContent(List<TypeModel> types, bool includeNamespace = true)
    {
        if (types == null || !types.Any())
            return string.Empty;

        // Pre-load all base types and members in batch (similar to TypeGroupProcessor)
        if (_repository != null)
        {
            var typeIds = types.Select(t => t.Id).ToList();
            var allBaseTypes = _repository.GetBaseTypesForMultipleTypes(typeIds);
            var allMembers = _repository.GetStructMembersForMultipleTypes(typeIds);
            var allBodies = _repository.GetFunctionBodiesForMultipleTypes(typeIds);

            // Fetch static variables in batch
            var allStaticVariables = new Dictionary<int, List<StaticVariableModel>>();
            foreach (var typeId in typeIds)
            {
                var svs = _repository.GetStaticVariablesForType(typeId);
                if (svs.Any())
                {
                    allStaticVariables[typeId] = svs;
                }
            }

            // Attach to types so generators find pre-loaded data
            foreach (var type in types)
            {
                if (allBaseTypes.TryGetValue(type.Id, out var baseTypes))
                    type.BaseTypes = baseTypes;
                else if (type.BaseTypes == null)
                    type.BaseTypes = new List<TypeInheritance>();

                if (allMembers.TryGetValue(type.Id, out var members))
                    type.StructMembers = members;
                else if (type.StructMembers == null)
                    type.StructMembers = new List<StructMemberModel>();

                if (allBodies.TryGetValue(type.Id, out var bodies))
                    type.FunctionBodies = bodies;
                else if (type.FunctionBodies == null)
                    type.FunctionBodies = new List<FunctionBodyModel>();

                if (allStaticVariables.TryGetValue(type.Id, out var staticVars))
                    type.StaticVariables = staticVars;
                else if (type.StaticVariables == null)
                    type.StaticVariables = new List<StaticVariableModel>();
            }
        }

        // Link parent/child relationships for nested type output
        _hierarchyService.LinkNestedTypes(types);

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
}
