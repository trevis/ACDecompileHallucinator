using System.Text;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output;

public class TypeGroupProcessor
{
    private readonly ITypeRepository? _repository;
    private readonly ICodeGenerator _classGenerator;
    private readonly ICodeGenerator _enumGenerator;
    private readonly TypeLookupCache? _lookupCache;
    private readonly ITypeHierarchyService _hierarchyService;

    public TypeGroupProcessor(ITypeRepository? repository = null, TypeLookupCache? lookupCache = null,
        ITypeHierarchyService? hierarchyService = null)
    {
        _repository = repository;
        _lookupCache = lookupCache;
        _hierarchyService = hierarchyService ?? new TypeHierarchyService();
        _classGenerator = new ClassOutputGenerator(repository);
        _enumGenerator = new EnumOutputGenerator(repository);

        // Pass cache to generators for efficient type resolution
        if (_lookupCache != null)
        {
            if (_classGenerator is TypeOutputGeneratorBase classGen)
                classGen.SetLookupCache(_lookupCache);
            if (_enumGenerator is TypeOutputGeneratorBase enumGen)
                enumGen.SetLookupCache(_lookupCache);
        }
    }

    public string GenerateGroupContent(List<TypeModel> types, bool includeHeaderAndNamespace = true)
    {
        if (types == null || types.Count == 0) return string.Empty;

        var tokens = GenerateGroupTokens(types, includeHeaderAndNamespace);
        var content = new StringBuilder();
        foreach (var token in tokens)
        {
            content.Append(token.Text);
        }

        return content.ToString();
    }

    public IEnumerable<CodeToken> GenerateGroupTokens(List<TypeModel> types, bool includeHeaderAndNamespace = true)
    {
        if (types == null || types.Count == 0) return Enumerable.Empty<CodeToken>();

        // Pre-load all base types and members in batch (2 queries instead of 2N)
        // This populates the type objects so generators don't need to query again
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

        var allTokens = new List<CodeToken>();

        if (includeHeaderAndNamespace)
        {
            // Use pragma once instead of traditional header guards
            allTokens.Add(new CodeToken("#pragma once\n\n", TokenType.Keyword));

            // Includes disabled as per Configurable Output Hierarchy Rules spec
        }

        // Add each type definition using the appropriate generator
        // Skip types that have a parent - they're rendered inside their parent
        var topLevelTypes = types.Where(t => t.ParentType == null).ToList();
        for (int i = 0; i < topLevelTypes.Count; i++)
        {
            var type = topLevelTypes[i];
            ICodeGenerator? generator = null;

            // Use enum generator for standalone enums (no parent)
            // Use class generator for structs/classes (handles nested types internally)
            if (type.Type == TypeType.Enum)
            {
                generator = _enumGenerator;
            }
            else if (type.Type == TypeType.Struct || type.Type == TypeType.Union || type.Type == TypeType.Class)
            {
                generator = _classGenerator;
            }

            if (generator != null)
            {
                allTokens.AddRange(generator.Generate(type));
            }

            if (i < topLevelTypes.Count - 1)
            {
                allTokens.Add(new CodeToken("\n\n", TokenType.Punctuation));
            }
        }

        if (includeHeaderAndNamespace)
        {
            // Namespace closing not needed since namespace is inlined
        }

        return allTokens;
    }

    private List<string> GetDependenciesForGroup(List<TypeModel> types)
    {
        var dependencies = new List<string>();

        foreach (var type in types)
        {
            var typeDependencies = type.Type switch
            {
                TypeType.Struct => _classGenerator.GetDependencies(type),
                TypeType.Class => _classGenerator.GetDependencies(type),
                TypeType.Union => _classGenerator.GetDependencies(type),
                TypeType.Enum => _enumGenerator.GetDependencies(type),
                _ => new List<string>()
            };

            foreach (var dep in typeDependencies)
            {
                if (!dependencies.Contains(dep))
                {
                    dependencies.Add(dep);
                }
            }
        }

        return dependencies;
    }
}