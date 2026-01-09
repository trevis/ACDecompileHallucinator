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

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Data is now pre-loaded in CSharpFileOutputGenerator, no need to query here!
        // The TypeModels already have BaseTypes, StructMembers, FunctionBodies, and StaticVariables attached

        // Link parent/child relationships for nested type output
        sw.Restart();
        _hierarchyService.LinkNestedTypes(types);
        var linkMs = sw.ElapsedMilliseconds;

        string result;
        sw.Restart();
        if (includeNamespace)
        {
            result = _generator.GenerateWithNamespace(types);
        }
        else
        {
            var sb = new System.Text.StringBuilder();
            foreach (var type in types.Where(t => t.ParentType == null))
            {
                sb.AppendLine(_generator.Generate(type));
            }

            result = sb.ToString();
        }

        var genMs = sw.ElapsedMilliseconds;

        Console.WriteLine($"  [CSharpGroupProcessor] Link: {linkMs}ms, Generate: {genMs}ms");

        return result;
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
