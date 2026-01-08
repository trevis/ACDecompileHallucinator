using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;

/// <summary>
/// A test implementation of ITypeRepository for use in unit tests.
/// Provides in-memory storage for all repository operations.
/// </summary>
public class TestTypeRepository : ITypeRepository
{
    private readonly List<TypeModel> _types = new List<TypeModel>();
    private readonly List<StructMemberModel> _structMembers = new List<StructMemberModel>();
    private readonly List<EnumMemberModel> _enumMembers = new List<EnumMemberModel>();
    private readonly List<FunctionParamModel> _functionParams = new List<FunctionParamModel>();
    private readonly List<FunctionSignatureModel> _functionSignatures = new List<FunctionSignatureModel>();
    private readonly List<TypeReference> _typeReferences = new List<TypeReference>();
    private readonly List<TypeTemplateArgument> _typeTemplateArguments = new List<TypeTemplateArgument>();
    private readonly List<TypeInheritance> _typeInheritances = new List<TypeInheritance>();
    private readonly List<TypeDefModel> _typeDefs = new List<TypeDefModel>();
    private readonly List<FunctionBodyModel> _functionBodies = new List<FunctionBodyModel>();
    private readonly List<StaticVariableModel> _staticVariables = new List<StaticVariableModel>();

    private int _nextTypeId = 1;
    private int _nextStructMemberId = 1;
    private int _nextEnumMemberId = 1;
    private int _nextFunctionParamId = 1;
    private int _nextFunctionSignatureId = 1;
    private int _nextTypeReferenceId = 1;
    private int _nextTypeTemplateArgumentId = 1;

    private int _nextTypeInheritanceId = 1;
    private int _nextTypeDefId = 1;
    private int _nextFunctionBodyId = 1;
    private int _nextStaticVariableId = 1;


    public void Dispose()
    {
        // No resources to dispose in mock
    }

    // Basic CRUD
    public int InsertType(TypeModel type)
    {
        type.Id = _nextTypeId++;
        _types.Add(type);
        return type.Id;
    }

    public TypeModel? GetTypeById(int id)
    {
        return _types.FirstOrDefault(t => t.Id == id);
    }

    public void UpdateType(TypeModel type)
    {
        var existing = _types.FirstOrDefault(t => t.Id == type.Id);
        if (existing != null)
        {
            var index = _types.IndexOf(existing);
            _types[index] = type;
        }
    }

    public void DeleteType(int id)
    {
        _types.RemoveAll(t => t.Id == id);
    }

    // Type reference CRUD
    public int InsertTypeReference(TypeReference typeReference)
    {
        typeReference.Id = _nextTypeReferenceId++;
        _typeReferences.Add(typeReference);
        return typeReference.Id;
    }

    public TypeReference? GetTypeReferenceById(int id)
    {
        return _typeReferences.FirstOrDefault(tr => tr.Id == id);
    }

    public void UpdateTypeReference(TypeReference typeReference)
    {
        var existing = _typeReferences.FirstOrDefault(tr => tr.Id == typeReference.Id);
        if (existing != null)
        {
            var index = _typeReferences.IndexOf(existing);
            _typeReferences[index] = typeReference;
        }
    }

    public void InsertTypeReferences(IEnumerable<TypeReference> typeReferences)
    {
        foreach (var tr in typeReferences)
            InsertTypeReference(tr);
    }

    public void DeleteTypeReference(int id)
    {
        _typeReferences.RemoveAll(tr => tr.Id == id);
    }

    // Type reference queries
    public List<TypeReference> GetAllTypeReferences()
    {
        return _typeReferences.ToList();
    }

    // Type template argument CRUD
    public int InsertTypeTemplateArgument(TypeTemplateArgument typeTemplateArgument)
    {
        typeTemplateArgument.Id = _nextTypeTemplateArgumentId++;
        _typeTemplateArguments.Add(typeTemplateArgument);
        return typeTemplateArgument.Id;
    }

    // Function body operations
    public int InsertFunctionBody(FunctionBodyModel functionBody)
    {
        functionBody.Id = _nextFunctionBodyId++;
        _functionBodies.Add(functionBody);
        return functionBody.Id;
    }

    public List<FunctionBodyModel> GetFunctionBodiesForType(int typeId)
    {
        return _functionBodies.Where(fb => fb.ParentId == typeId).ToList();
    }

    public Dictionary<int, List<FunctionBodyModel>> GetFunctionBodiesForMultipleTypes(IEnumerable<int> typeIds)
    {
        var idSet = typeIds.ToHashSet();
        return _functionBodies
            .Where(fb => fb.ParentId.HasValue && idSet.Contains(fb.ParentId.Value))
            .GroupBy(fb => fb.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public void UpdateTypeTemplateArgument(TypeTemplateArgument typeTemplateArgument)
    {
        var existing = _typeTemplateArguments.FirstOrDefault(tta => tta.Id == typeTemplateArgument.Id);
        if (existing != null)
        {
            var index = _typeTemplateArguments.IndexOf(existing);
            _typeTemplateArguments[index] = typeTemplateArgument;
        }
    }

    public List<TypeTemplateArgument> GetAllTypeTemplateArguments()
    {
        return _typeTemplateArguments.ToList();
    }

    // Type inheritance CRUD
    public int InsertTypeInheritance(TypeInheritance typeInheritance)
    {
        typeInheritance.Id = _nextTypeInheritanceId++;
        _typeInheritances.Add(typeInheritance);
        return typeInheritance.Id;
    }

    public void UpdateTypeInheritance(TypeInheritance typeInheritance)
    {
        var existing = _typeInheritances.FirstOrDefault(ti => ti.Id == typeInheritance.Id);
        if (existing != null)
        {
            var index = _typeInheritances.IndexOf(existing);
            _typeInheritances[index] = typeInheritance;
        }
    }

    public List<TypeInheritance> GetAllTypeInheritances()
    {
        return _typeInheritances.ToList();
    }

    public void UpdateStructMember(StructMemberModel structMember)
    {
        var existing = _structMembers.FirstOrDefault(sm => sm.Id == structMember.Id);
        if (existing != null)
        {
            var index = _structMembers.IndexOf(existing);
            _structMembers[index] = structMember;
        }
    }

    public List<StructMemberModel> GetAllStructMembers()
    {
        return _structMembers.ToList();
    }

    // Queries
    public List<TypeModel> GetAllTypes(bool includeIgnored = true)
    {
        return includeIgnored ? _types.ToList() : _types.Where(t => !t.IsIgnored).ToList();
    }

    public List<TypeModel> GetTypesByNamespace(string ns)
    {
        return _types.Where(t => t.Namespace == ns).ToList();
    }

    public List<TypeModel> GetTypesByTypeType(TypeType typeType)
    {
        return _types.Where(t => t.Type == typeType).ToList();
    }

    public TypeModel? GetTypeByFullyQualifiedName(string fqn)
    {
        return _types.FirstOrDefault(t => t.FullyQualifiedName == fqn);
    }

    public List<TypeModel> SearchTypes(string searchTerm, bool includeIgnored = true)
    {
        IEnumerable<TypeModel> query = _types;
        if (!includeIgnored)
        {
            query = query.Where(t => !t.IsIgnored);
        }

        return query.Where(t => t.BaseName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                t.Namespace.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                t.StoredFullyQualifiedName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public List<TypeModel> GetTypesForGroup(string baseName, string ns)
    {
        var targetFqn = string.IsNullOrEmpty(ns) ? baseName : $"{ns}::{baseName}";

        return _types.Where(t => (t.BaseName == baseName && t.Namespace == ns) ||
                                 (t.BaseTypePath != null && t.BaseTypePath == targetFqn))
            .ToList();
    }

    // Relationships
    public List<TypeTemplateArgument> GetTemplateArguments(int parentTypeId)
    {
        return _typeTemplateArguments.Where(tta => tta.ParentTypeId == parentTypeId).ToList();
    }

    public List<TypeInheritance> GetBaseTypes(int derivedTypeId)
    {
        return _typeInheritances.Where(ti => ti.ParentTypeId == derivedTypeId).ToList();
    }

    public List<TypeInheritance> GetBaseTypesWithRelatedTypes(int derivedTypeId)
    {
        var baseTypes = _typeInheritances.Where(ti => ti.ParentTypeId == derivedTypeId).ToList();
        // In a real implementation, this would populate the RelatedType property
        return baseTypes;
    }

    public List<TypeInheritance> GetDerivedTypes(int baseTypeId)
    {
        return _typeInheritances.Where(ti => ti.RelatedTypeId == baseTypeId).ToList();
    }

    // Enum members
    public List<EnumMemberModel> GetEnumMembers(int enumTypeId)
    {
        return _enumMembers.Where(em => em.EnumTypeId == enumTypeId).ToList();
    }

    public int InsertEnumMember(EnumMemberModel enumMember)
    {
        enumMember.Id = _nextEnumMemberId++;
        _enumMembers.Add(enumMember);
        return enumMember.Id;
    }

    public void InsertEnumMembers(IEnumerable<EnumMemberModel> enumMembers)
    {
        foreach (var enumMember in enumMembers)
        {
            InsertEnumMember(enumMember);
        }
    }

    public void DeleteEnumMembersForType(int enumTypeId)
    {
        _enumMembers.RemoveAll(em => em.EnumTypeId == enumTypeId);
    }

    /// <summary>
    /// Helper method for tests to add enum members for a specific enum type
    /// </summary>
    public void AddEnumMembers(int enumTypeId, List<EnumMemberModel> members)
    {
        foreach (var member in members)
        {
            member.EnumTypeId = enumTypeId;
            InsertEnumMember(member);
        }
    }

    // Struct members
    public List<StructMemberModel> GetStructMembers(int structTypeId)
    {
        return _structMembers.Where(sm => sm.StructTypeId == structTypeId).ToList();
    }

    public List<StructMemberModel> GetStructMembersWithRelatedTypes(int structTypeId)
    {
        return _structMembers.Where(sm => sm.StructTypeId == structTypeId).ToList();
    }

    public int InsertStructMember(StructMemberModel structMember)
    {
        structMember.Id = _nextStructMemberId++;
        _structMembers.Add(structMember);
        return structMember.Id;
    }

    public void InsertStructMembers(IEnumerable<StructMemberModel> structMembers)
    {
        foreach (var structMember in structMembers)
        {
            InsertStructMember(structMember);
        }
    }

    public void DeleteStructMembersForType(int structTypeId)
    {
        _structMembers.RemoveAll(sm => sm.StructTypeId == structTypeId);
    }

    // Function parameters
    public List<FunctionParamModel> GetFunctionParameters(int parentSignatureId)
    {
        return _functionParams.Where(fp => fp.ParentFunctionSignatureId == parentSignatureId).ToList();
    }

    public List<FunctionParamModel> GetAllFunctionParameters()
    {
        return _functionParams.ToList();
    }

    public int InsertFunctionParameter(FunctionParamModel functionParam)
    {
        functionParam.Id = _nextFunctionParamId++;
        _functionParams.Add(functionParam);
        return functionParam.Id;
    }

    public void InsertFunctionParameters(IEnumerable<FunctionParamModel> functionParams)
    {
        foreach (var functionParam in functionParams)
        {
            InsertFunctionParameter(functionParam);
        }
    }

    public void DeleteFunctionParametersForMember(int parentSignatureId)
    {
        _functionParams.RemoveAll(fp => fp.ParentFunctionSignatureId == parentSignatureId);
    }

    // Function signatures (for function pointer parameters and return types)
    public List<FunctionSignatureModel> GetAllFunctionSignatures()
    {
        return _functionSignatures.ToList();
    }

    public FunctionSignatureModel? GetFunctionSignatureById(int id)
    {
        return _functionSignatures.FirstOrDefault(fs => fs.Id == id);
    }

    public int InsertFunctionSignature(FunctionSignatureModel functionSignature)
    {
        functionSignature.Id = _nextFunctionSignatureId++;
        _functionSignatures.Add(functionSignature);
        return functionSignature.Id;
    }

    public void InsertFunctionSignatures(IEnumerable<FunctionSignatureModel> functionSignatures)
    {
        foreach (var functionSignature in functionSignatures)
        {
            InsertFunctionSignature(functionSignature);
        }
    }

    public void DeleteFunctionSignature(int id)
    {
        _functionSignatures.RemoveAll(fs => fs.Id == id);
    }

    // TypeDef operations
    public int InsertTypeDef(TypeDefModel typeDef)
    {
        typeDef.Id = _nextTypeDefId++;
        _typeDefs.Add(typeDef);
        return typeDef.Id;
    }

    public TypeDefModel? GetTypeDefByName(string name)
    {
        return _typeDefs.FirstOrDefault(td => td.Name == name);
    }

    public TypeDefModel? GetTypeDefById(int id)
    {
        return _typeDefs.FirstOrDefault(td => td.Id == id);
    }

    public List<TypeDefModel> GetAllTypeDefs()
    {
        return _typeDefs.ToList();
    }

    public TypeReference? ResolveTypeDefChain(string typedefName, HashSet<string>? visited = null)
    {
        return _typeReferences.FirstOrDefault(tr => tr.TypeString == typedefName);
    }

    // Bulk operations
    public void InsertTypes(IEnumerable<TypeModel> types)
    {
        foreach (var type in types)
        {
            InsertType(type);
        }
    }

    // Type resolution
    public void ResolveTypeReferences()
    {
        // No-op for mock
    }

    public int GetUnresolvedTypeCount()
    {
        return 0;
    }

    public List<string> GetSampleUnresolvedTypeReferences(int maxCount = 5)
    {
        return new List<string>();
    }

    public List<DetailedUnresolvedReference> GetDetailedUnresolvedTypeReferences(int maxCount = 5)
    {
        return new List<DetailedUnresolvedReference>();
    }

    // BaseTypePath operations
    public void UpdateBaseTypePath(int typeId, string basePath)
    {
        // No-op for mock
    }

    public void PopulateBaseTypePaths(List<TypeModel> allTypes)
    {
        // No-op for mock
    }

    // Transaction management
    public void SaveChanges()
    {
        // No-op for mock
    }

    public List<(int Id, string BaseName, string Namespace, string? StoredFqn)> GetTypeLookupData()
    {
        return _types
            .Where(t => !t.IsIgnored)
            .Select(t => (t.Id, t.BaseName, t.Namespace, (string?)t.StoredFullyQualifiedName))
            .ToList();
    }

    public Dictionary<int, List<TypeInheritance>> GetBaseTypesForMultipleTypes(IEnumerable<int> typeIds)
    {
        var idSet = typeIds.ToHashSet();
        return _typeInheritances
            .Where(ti => idSet.Contains(ti.ParentTypeId))
            .GroupBy(ti => ti.ParentTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public Dictionary<int, List<StructMemberModel>> GetStructMembersForMultipleTypes(IEnumerable<int> typeIds)
    {
        var idSet = typeIds.ToHashSet();
        return _structMembers
            .Where(sm => idSet.Contains(sm.StructTypeId))
            .GroupBy(sm => sm.StructTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public List<TypeReference> GetUnresolvedTypeReferences()
    {
        return new List<TypeReference>();
    }

    public List<TypeTemplateArgument> GetUnresolvedTypeTemplateArguments()
    {
        return new List<TypeTemplateArgument>();
    }

    public List<TypeInheritance> GetUnresolvedTypeInheritances()
    {
        return new List<TypeInheritance>();
    }

    // Static variable operations
    public int InsertStaticVariable(StaticVariableModel staticVariable)
    {
        staticVariable.Id = _nextStaticVariableId++;
        _staticVariables.Add(staticVariable);
        return staticVariable.Id;
    }

    public void InsertStaticVariables(IEnumerable<StaticVariableModel> staticVariables)
    {
        foreach (var sv in staticVariables)
            InsertStaticVariable(sv);
    }

    public List<StaticVariableModel> GetStaticVariablesForType(int typeId)
    {
        return _staticVariables.Where(sv => sv.ParentTypeId == typeId).ToList();
    }
}
