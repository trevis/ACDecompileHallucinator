using System;
using System.Collections.Generic;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Lib.Storage;

public interface ITypeRepository : IDisposable
{
    // Basic CRUD
    int InsertType(TypeModel type);
    TypeModel? GetTypeById(int id);
    void UpdateType(TypeModel type);
    void DeleteType(int id);

    // Type reference CRUD
    int InsertTypeReference(TypeReference typeReference);
    TypeReference? GetTypeReferenceById(int id);
    void UpdateTypeReference(TypeReference typeReference);
    void DeleteTypeReference(int id);

    // Type reference queries
    List<TypeReference> GetAllTypeReferences();

    // Unresolved queries
    List<TypeReference> GetUnresolvedTypeReferences();
    List<TypeTemplateArgument> GetUnresolvedTypeTemplateArguments();

    List<TypeInheritance> GetUnresolvedTypeInheritances();

    // Type template argument CRUD
    int InsertTypeTemplateArgument(TypeTemplateArgument typeTemplateArgument);
    void UpdateTypeTemplateArgument(TypeTemplateArgument typeTemplateArgument);
    List<TypeTemplateArgument> GetAllTypeTemplateArguments();

    // Type inheritance CRUD
    int InsertTypeInheritance(TypeInheritance typeInheritance);
    void UpdateTypeInheritance(TypeInheritance typeInheritance);
    List<TypeInheritance> GetAllTypeInheritances();

    void UpdateStructMember(StructMemberModel structMember);
    List<StructMemberModel> GetAllStructMembers();


    // Queries
    List<TypeModel> GetAllTypes(bool includeIgnored = true);
    List<TypeModel> GetTypesByNamespace(string ns);
    List<TypeModel> GetTypesByTypeType(TypeType typeType);
    TypeModel? GetTypeByFullyQualifiedName(string fqn);
    List<TypeModel> SearchTypes(string searchTerm, bool includeIgnored = true);
    List<TypeModel> GetTypesForGroup(string baseName, string ns);

    // Relationships
    List<TypeTemplateArgument> GetTemplateArguments(int parentTypeId);
    List<TypeInheritance> GetBaseTypes(int derivedTypeId);
    List<TypeInheritance> GetBaseTypesWithRelatedTypes(int derivedTypeId);
    List<TypeInheritance> GetDerivedTypes(int baseTypeId);

    // Enum members
    List<EnumMemberModel> GetEnumMembers(int enumTypeId);
    int InsertEnumMember(EnumMemberModel enumMember);
    void InsertEnumMembers(IEnumerable<EnumMemberModel> enumMembers);
    void DeleteEnumMembersForType(int enumTypeId);

    // Struct members
    List<StructMemberModel> GetStructMembers(int structTypeId);
    List<StructMemberModel> GetStructMembersWithRelatedTypes(int structTypeId);
    int InsertStructMember(StructMemberModel structMember);
    void InsertStructMembers(IEnumerable<StructMemberModel> structMembers);
    void DeleteStructMembersForType(int structTypeId);

    // Function parameters
    List<FunctionParamModel> GetFunctionParameters(int parentSignatureId);
    List<FunctionParamModel> GetAllFunctionParameters();
    int InsertFunctionParameter(FunctionParamModel functionParam);
    void InsertFunctionParameters(IEnumerable<FunctionParamModel> functionParams);
    void DeleteFunctionParametersForMember(int parentSignatureId);

    // Function signatures (for function pointer parameters and return types)
    List<FunctionSignatureModel> GetAllFunctionSignatures();
    FunctionSignatureModel? GetFunctionSignatureById(int id);
    int InsertFunctionSignature(FunctionSignatureModel functionSignature);
    void InsertFunctionSignatures(IEnumerable<FunctionSignatureModel> functionSignatures);
    void DeleteFunctionSignature(int id);

    // TypeDef operations
    int InsertTypeDef(TypeDefModel typeDef);
    TypeDefModel? GetTypeDefByName(string name);
    TypeDefModel? GetTypeDefById(int id);
    List<TypeDefModel> GetAllTypeDefs();
    TypeReference? ResolveTypeDefChain(string typedefName, HashSet<string>? visited = null);

    // Bulk operations
    void InsertTypes(IEnumerable<TypeModel> types);

    // Type resolution
    void ResolveTypeReferences();
    int GetUnresolvedTypeCount();
    List<string> GetSampleUnresolvedTypeReferences(int maxCount = 5);
    List<DetailedUnresolvedReference> GetDetailedUnresolvedTypeReferences(int maxCount = 5);

    // BaseTypePath operations
    void UpdateBaseTypePath(int typeId, string basePath);
    void PopulateBaseTypePaths(List<TypeModel> allTypes);

    // Transaction management
    void SaveChanges();

    /// <summary>
    /// Gets minimal type data for building lookup caches.
    /// Much faster than GetAllTypes() as it loads no navigation properties.
    /// </summary>
    List<(int Id, string BaseName, string Namespace, string? StoredFqn)> GetTypeLookupData();

    /// <summary>
    /// Batch loads base types for multiple types in a single query.
    /// More efficient than calling GetBaseTypesWithRelatedTypes for each type individually.
    /// </summary>
    Dictionary<int, List<TypeInheritance>> GetBaseTypesForMultipleTypes(IEnumerable<int> typeIds);

    // Function body operations
    int InsertFunctionBody(FunctionBodyModel functionBody);
    List<FunctionBodyModel> GetFunctionBodiesForType(int typeId);
    Dictionary<int, List<FunctionBodyModel>> GetFunctionBodiesForMultipleTypes(IEnumerable<int> typeIds);

    /// <summary>
    /// Batch loads struct members for multiple types in a single query.
    /// More efficient than calling GetStructMembersWithRelatedTypes for each type individually.
    /// </summary>
    Dictionary<int, List<StructMemberModel>> GetStructMembersForMultipleTypes(IEnumerable<int> typeIds);

    void InsertTypeReferences(IEnumerable<TypeReference> typeReferences);

    // Static variable operations
    int InsertStaticVariable(StaticVariableModel staticVariable);
    void InsertStaticVariables(IEnumerable<StaticVariableModel> staticVariables);
    List<StaticVariableModel> GetStaticVariablesForType(int typeId);
}
