using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Storage;

public class TypeRepository : ITypeRepository
{
    private readonly TypeContext _context;
    private bool _disposed;

    public TypeRepository(TypeContext context)
    {
        _context = context;
    }

    // Basic CRUD
    public int InsertType(TypeModel type)
    {
        _context.Types.Add(type);
        return type.Id; // Return ID without saving changes - caller should manage transactions
    }

    public TypeModel? GetTypeById(int id)
    {
        return _context.Types
            .Include(t => t.TemplateArguments)
            .ThenInclude(ta => ta.TypeReference)
            .Include(t => t.BaseTypes)
            .ThenInclude(bt => bt.RelatedType)
            .FirstOrDefault(t => t.Id == id);
    }

    public void UpdateType(TypeModel type)
    {
        _context.Types.Update(type);
        // Caller should manage transactions
    }

    public void DeleteType(int id)
    {
        var type = _context.Types.Find(id);
        if (type != null)
        {
            _context.Types.Remove(type);
            // Caller should manage transactions
        }
    }

    // Type reference CRUD
    public int InsertTypeReference(TypeReference typeReference)
    {
        _context.TypeReferences.Add(typeReference);
        return typeReference.Id; // Return ID without saving changes - caller should manage transactions
    }

    public void InsertTypeReferences(IEnumerable<TypeReference> typeReferences)
    {
        _context.TypeReferences.AddRange(typeReferences);
    }

    public TypeReference? GetTypeReferenceById(int id)
    {
        return _context.TypeReferences.Find(id);
    }

    public void UpdateTypeReference(TypeReference typeReference)
    {
        _context.TypeReferences.Update(typeReference);
        // Caller should manage transactions
    }

    public void DeleteTypeReference(int id)
    {
        var typeReference = _context.TypeReferences.Find(id);
        if (typeReference != null)
        {
            _context.TypeReferences.Remove(typeReference);
            // Caller should manage transactions
        }
    }

    // Type reference queries
    public List<TypeReference> GetAllTypeReferences()
    {
        // NOTE: Not using AsNoTracking because these entities are often updated
        // AsNoTracking would cause issues when calling Update() on entities with Id=0
        return _context.TypeReferences.ToList();
    }

    public List<TypeReference> GetUnresolvedTypeReferences()
    {
        return _context.TypeReferences
            .Where(tr => tr.ReferencedTypeId == null && !string.IsNullOrEmpty(tr.TypeString))
            .ToList();
    }

    public List<TypeTemplateArgument> GetUnresolvedTypeTemplateArguments()
    {
        return _context.TypeTemplateArguments
            .Where(ta => ta.TypeReferenceId == null && !string.IsNullOrEmpty(ta.TypeString))
            .ToList();
    }

    public List<TypeInheritance> GetUnresolvedTypeInheritances()
    {
        return _context.TypeInheritances
            .Where(ti => ti.RelatedTypeId == null && !string.IsNullOrEmpty(ti.RelatedTypeString))
            .ToList();
    }

    // Type template argument CRUD
    public int InsertTypeTemplateArgument(TypeTemplateArgument typeTemplateArgument)
    {
        _context.TypeTemplateArguments.Add(typeTemplateArgument);
        return typeTemplateArgument.Id; // Return ID without saving changes - caller should manage transactions
    }

    public void UpdateTypeTemplateArgument(TypeTemplateArgument typeTemplateArgument)
    {
        _context.TypeTemplateArguments.Update(typeTemplateArgument);
        // Caller should manage transactions
    }

    public List<TypeTemplateArgument> GetAllTypeTemplateArguments()
    {
        // NOTE: Not using AsNoTracking because these entities are often updated
        // AsNoTracking would cause issues when calling Update() on entities with Id=0
        return _context.TypeTemplateArguments.ToList();
    }

    // Type inheritance CRUD
    public int InsertTypeInheritance(TypeInheritance typeInheritance)
    {
        _context.TypeInheritances.Add(typeInheritance);
        return typeInheritance.Id; // Return ID without saving changes - caller should manage transactions
    }

    public void UpdateTypeInheritance(TypeInheritance typeInheritance)
    {
        _context.TypeInheritances.Update(typeInheritance);
        // Caller should manage transactions
    }

    public List<TypeInheritance> GetAllTypeInheritances()
    {
        // NOTE: Not using AsNoTracking because these entities are often updated
        // AsNoTracking would cause issues when calling Update() on entities with Id=0
        return _context.TypeInheritances.ToList();
    }

    public void UpdateStructMember(StructMemberModel structMember)
    {
        _context.StructMembers.Update(structMember);
        // Caller should manage transactions
    }

    public List<StructMemberModel> GetAllStructMembers()
    {
        return _context.StructMembers.AsNoTracking().ToList();
    }

    // Queries
    public List<TypeModel> GetAllTypes(bool includeIgnored = true)
    {
        var query = _context.Types.AsNoTracking();
        if (!includeIgnored)
        {
            query = query.Where(t => !t.IsIgnored);
        }

        return query
            .Include(t => t.TemplateArguments)
            .ThenInclude(ta => ta.TypeReference)
            .Include(t => t.BaseTypes)
            .ThenInclude(bt => bt.RelatedType)
            .ToList();
    }

    public List<TypeModel> GetTypesByNamespace(string ns)
    {
        return _context.Types
            .Include(t => t.TemplateArguments)
            .ThenInclude(ta => ta.TypeReference)
            .Include(t => t.BaseTypes)
            .ThenInclude(bt => bt.RelatedType)
            .Where(t => t.Namespace == ns)
            .ToList();
    }

    public List<TypeModel> GetTypesByTypeType(TypeType typeType)
    {
        return _context.Types
            .Include(t => t.TemplateArguments)
            .ThenInclude(ta => ta.TypeReference)
            .Include(t => t.BaseTypes)
            .ThenInclude(bt => bt.RelatedType)
            .Where(t => t.Type == typeType)
            .ToList();
    }

    public TypeModel? GetTypeByFullyQualifiedName(string fqn)
    {
        return _context.Types
            .Include(t => t.TemplateArguments)
            .ThenInclude(ta => ta.TypeReference)
            .Include(t => t.BaseTypes)
            .ThenInclude(bt => bt.RelatedType)
            .FirstOrDefault(t => t.StoredFullyQualifiedName == fqn);
    }

    public List<TypeModel> SearchTypes(string searchTerm, bool includeIgnored = true)
    {
        var query = _context.Types.AsQueryable();
        if (!includeIgnored)
        {
            query = query.Where(t => !t.IsIgnored);
        }

        var lowerSearchTerm = searchTerm.ToLower();

        return query
            .Include(t => t.TemplateArguments)
            .ThenInclude(ta => ta.TypeReference)
            .Include(t => t.BaseTypes)
            .ThenInclude(bt => bt.RelatedType)
            .Where(t => t.BaseName.ToLower().Contains(lowerSearchTerm) ||
                        t.Namespace.ToLower().Contains(lowerSearchTerm) ||
                        t.StoredFullyQualifiedName.ToLower().Contains(lowerSearchTerm))
            .ToList();
    }

    // Relationships
    public List<TypeTemplateArgument> GetTemplateArguments(int parentTypeId)
    {
        return _context.TypeTemplateArguments
            .Where(ta => ta.ParentTypeId == parentTypeId)
            .ToList();
    }

    public List<TypeInheritance> GetBaseTypes(int derivedTypeId)
    {
        // ParentTypeId is the derived type (the type that inherits)
        return _context.TypeInheritances
            .Where(ti => ti.ParentTypeId == derivedTypeId)
            .ToList();
    }

    public List<TypeInheritance> GetBaseTypesWithRelatedTypes(int derivedTypeId)
    {
        // ParentTypeId is the derived type (the type that inherits)
        return _context.TypeInheritances
            .Include(ti => ti.RelatedType)
            .Where(ti => ti.ParentTypeId == derivedTypeId)
            .ToList();
    }

    public List<TypeInheritance> GetDerivedTypes(int baseTypeId)
    {
        // Find types that inherit FROM this base type
        // RelatedTypeId is the base type in an inheritance relationship
        return _context.TypeInheritances
            .Where(ti => ti.RelatedTypeId == baseTypeId)
            .ToList();
    }

    // Enum members
    public List<EnumMemberModel> GetEnumMembers(int enumTypeId)
    {
        return _context.EnumMembers
            .Where(em => em.EnumTypeId == enumTypeId)
            .ToList();
    }

    public int InsertEnumMember(EnumMemberModel enumMember)
    {
        _context.EnumMembers.Add(enumMember);
        return enumMember.Id; // Return ID without saving changes - caller should manage transactions
    }

    public void InsertEnumMembers(IEnumerable<EnumMemberModel> enumMembers)
    {
        _context.EnumMembers.AddRange(enumMembers);
        // Caller should manage transactions
    }

    public void DeleteEnumMembersForType(int enumTypeId)
    {
        var members = _context.EnumMembers.Where(em => em.EnumTypeId == enumTypeId);
        _context.EnumMembers.RemoveRange(members);
        // Caller should manage transactions
    }

    // Struct members
    public List<StructMemberModel> GetStructMembers(int structTypeId)
    {
        return _context.StructMembers
            .Include(sm => sm.TypeReference)
            .Include(sm => sm.FunctionSignature)
            .ThenInclude(fs => fs!.Parameters)
            .Where(sm => sm.StructTypeId == structTypeId)
            .ToList();
    }

    public List<StructMemberModel> GetStructMembersWithRelatedTypes(int structTypeId)
    {
        return _context.StructMembers
            .Include(sm => sm.TypeReference)
            .Include(sm => sm.FunctionSignature)
            .ThenInclude(fs => fs!.Parameters)
            .Where(sm => sm.StructTypeId == structTypeId)
            .ToList();
    }

    public int InsertStructMember(StructMemberModel structMember)
    {
        _context.StructMembers.Add(structMember);
        return structMember.Id; // Return ID without saving changes - caller should manage transactions
    }

    public void InsertStructMembers(IEnumerable<StructMemberModel> structMembers)
    {
        _context.StructMembers.AddRange(structMembers);
        // Caller should manage transactions
    }

    public void DeleteStructMembersForType(int structTypeId)
    {
        var members = _context.StructMembers.Where(sm => sm.StructTypeId == structTypeId);
        _context.StructMembers.RemoveRange(members);
        // Caller should manage transactions
    }

    // Function parameters
    public List<FunctionParamModel> GetFunctionParameters(int parentSignatureId)
    {
        return _context.FunctionParameters
            .Where(fp => fp.ParentFunctionSignatureId == parentSignatureId)
            .ToList();
    }

    public List<FunctionParamModel> GetAllFunctionParameters()
    {
        return _context.FunctionParameters.AsNoTracking().ToList();
    }

    public int InsertFunctionParameter(FunctionParamModel functionParam)
    {
        _context.FunctionParameters.Add(functionParam);
        return functionParam.Id; // Return ID without saving changes - caller should manage transactions
    }

    public void InsertFunctionParameters(IEnumerable<FunctionParamModel> functionParams)
    {
        _context.FunctionParameters.AddRange(functionParams);
        // Caller should manage transactions
    }

    public void DeleteFunctionParametersForMember(int parentSignatureId)
    {
        var paramsList = _context.FunctionParameters.Where(fp => fp.ParentFunctionSignatureId == parentSignatureId);
        _context.FunctionParameters.RemoveRange(paramsList);
        // Caller should manage transactions
    }

    // Function signatures
    public List<FunctionSignatureModel> GetAllFunctionSignatures()
    {
        return _context.FunctionSignatures
            .Include(fs => fs.Parameters)
            .Include(fs => fs.ReturnTypeReference)
            .ToList();
    }

    public FunctionSignatureModel? GetFunctionSignatureById(int id)
    {
        return _context.FunctionSignatures
            .Include(fs => fs.Parameters)
            .Include(fs => fs.ReturnTypeReference)
            .FirstOrDefault(fs => fs.Id == id);
    }

    public int InsertFunctionSignature(FunctionSignatureModel functionSignature)
    {
        _context.FunctionSignatures.Add(functionSignature);
        return functionSignature.Id; // Return ID without saving changes - caller should manage transactions
    }

    public void InsertFunctionSignatures(IEnumerable<FunctionSignatureModel> functionSignatures)
    {
        _context.FunctionSignatures.AddRange(functionSignatures);
        // Caller should manage transactions
    }

    public void DeleteFunctionSignature(int id)
    {
        var signature = _context.FunctionSignatures.Find(id);
        if (signature != null)
        {
            _context.FunctionSignatures.Remove(signature);
            // Caller should manage transactions
        }
    }

    // TypeDef operations
    public int InsertTypeDef(TypeDefModel typeDef)
    {
        _context.TypeDefs.Add(typeDef);
        return typeDef.Id;
    }

    public TypeDefModel? GetTypeDefByName(string name)
    {
        // Support both simple name (e.g., "FARPROC") and fully qualified name (e.g., "MyNamespace::FARPROC")
        if (name.Contains("::"))
        {
            // Fully qualified name - split into namespace and name
            var lastIndex = name.LastIndexOf("::");
            var ns = name.Substring(0, lastIndex);
            var baseName = name.Substring(lastIndex + 2);

            return _context.TypeDefs
                .Include(td => td.TypeReference)
                .Include(td => td.FunctionSignature)
                .ThenInclude(fs => fs!.Parameters)
                .FirstOrDefault(td => td.Namespace == ns && td.Name == baseName);
        }
        else
        {
            // Simple name - try with empty namespace first, then any namespace
            var result = _context.TypeDefs
                .Include(td => td.TypeReference)
                .Include(td => td.FunctionSignature)
                .ThenInclude(fs => fs!.Parameters)
                .FirstOrDefault(td => td.Name == name && string.IsNullOrEmpty(td.Namespace));

            if (result == null)
            {
                // If not found with empty namespace, try any namespace
                result = _context.TypeDefs
                    .Include(td => td.TypeReference)
                    .Include(td => td.FunctionSignature)
                    .ThenInclude(fs => fs!.Parameters)
                    .FirstOrDefault(td => td.Name == name);
            }

            return result;
        }
    }

    public TypeDefModel? GetTypeDefById(int id)
    {
        return _context.TypeDefs
            .Include(td => td.TypeReference)
            .Include(td => td.FunctionSignature)
            .ThenInclude(fs => fs!.Parameters)
            .FirstOrDefault(td => td.Id == id);
    }

    public List<TypeDefModel> GetAllTypeDefs()
    {
        return _context.TypeDefs
            .Include(td => td.TypeReference)
            .Include(td => td.FunctionSignature)
            .ToList();
    }

    public TypeReference? ResolveTypeDefChain(string typedefName, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();

        if (visited.Contains(typedefName))
        {
            Console.WriteLine($"Warning: Circular typedef detected involving '{typedefName}'");
            return null;
        }

        visited.Add(typedefName);

        var typeDef = GetTypeDefByName(typedefName);
        if (typeDef == null)
            return null;

        // Check if the typedef references another typedef
        var referencedType = _context.Types.Find(typeDef.TypeReference?.ReferencedTypeId);
        if (referencedType == null)
        {
            // Type not found in Types table, might be a primitive or forward declaration
            return typeDef.TypeReference;
        }

        // If the referenced type is also a typedef, continue the chain
        var referencedTypeDef = GetTypeDefByName(referencedType.FullyQualifiedName);
        if (referencedTypeDef != null)
        {
            return ResolveTypeDefChain(referencedTypeDef.FullyQualifiedName, visited);
        }

        return typeDef.TypeReference;
    }

    // Bulk operations
    public void InsertTypes(IEnumerable<TypeModel> types)
    {
        var typeList = types.ToList();
        Console.WriteLine($"DEBUG: Received {typeList.Count} types for insertion");

        // Check for null/empty StoredFullyQualifiedName values
        var nullOrEmptyFqns = typeList.Where(t => string.IsNullOrEmpty(t.StoredFullyQualifiedName)).ToList();
        if (nullOrEmptyFqns.Any())
        {
            Console.WriteLine(
                $"ERROR: Found {nullOrEmptyFqns.Count} types with null or empty StoredFullyQualifiedName:");
            foreach (var type in nullOrEmptyFqns.Take(10)) // Show first 10
            {
                Console.WriteLine(
                    $"  - Type: {type.Type}, Namespace: '{type.Namespace}', BaseName: '{type.BaseName}', FullyQualifiedName: '{type.FullyQualifiedName}'");
            }

            throw new InvalidOperationException(
                $"Found {nullOrEmptyFqns.Count} types with null or empty StoredFullyQualifiedName");
        }

        // Also check against existing types in the database
        var existingFqns = _context.Types
            .Select(t => t.StoredFullyQualifiedName)
            .ToHashSet();

        // Filter out types that already exist in the database
        var typesToInsert = typeList
            .Where(t => !existingFqns.Contains(t.StoredFullyQualifiedName))
            .ToList();

        if (typesToInsert.Count < typeList.Count)
        {
            Console.WriteLine(
                $"Warning: {typeList.Count - typesToInsert.Count} type(s) already exist in database, skipping those");
        }

        // Check for duplicates in the final list before insertion
        var fqnGroups = typesToInsert.GroupBy(t => t.StoredFullyQualifiedName);
        var duplicates = fqnGroups.Where(g => g.Count() > 1).ToList();

        if (duplicates.Any())
        {
            Console.WriteLine($"ERROR: Found {duplicates.Count()} duplicate fully qualified names in types to insert:");
            foreach (var dupGroup in duplicates.Take(5)) // Show first 5 duplicate groups
            {
                Console.WriteLine($"  Duplicate FQN: '{dupGroup.Key}' appears {dupGroup.Count()} times");
                foreach (var dup in dupGroup.Take(3)) // Show first 3 of each duplicate group
                {
                    Console.WriteLine($"    - Type: {dup.Type}, Namespace: {dup.Namespace}, BaseName: {dup.BaseName}");
                }
            }

            throw new InvalidOperationException(
                $"Found duplicate StoredFullyQualifiedName values: {string.Join(", ", duplicates.Select(d => d.Key))}");
        }

        if (typesToInsert.Any())
        {
            Console.WriteLine($"Inserting {typesToInsert.Count} new types");
            _context.Types.AddRange(typesToInsert);
        }
        else
        {
            Console.WriteLine("No new types to insert");
        }

        // Caller should manage transactions
    }

    // Type resolution - now handled by TypeResolutionService
    public void ResolveTypeReferences()
    {
        var resolutionService = new TypeResolutionService(this);
        resolutionService.ResolveTypeReferences();
    }

    public int GetUnresolvedTypeCount()
    {
        var resolutionService = new TypeResolutionService(this);
        return resolutionService.GetUnresolvedTypeCount();
    }

    public List<string> GetSampleUnresolvedTypeReferences(int maxCount = 5)
    {
        var resolutionService = new TypeResolutionService(this);
        return resolutionService.GetSampleUnresolvedTypeReferences(maxCount);
    }

    public List<DetailedUnresolvedReference> GetDetailedUnresolvedTypeReferences(int maxCount = 5)
    {
        var resolutionService = new TypeResolutionService(this);
        return resolutionService.GetDetailedUnresolvedTypeReferences(maxCount);
    }

    // BaseTypePath operations - now handled by TypeResolutionService
    public void UpdateBaseTypePath(int typeId, string basePath)
    {
        var type = _context.Types.Find(typeId);
        if (type != null)
        {
            type.BaseTypePath = basePath;
            // Caller should manage transactions
        }
    }

    public void PopulateBaseTypePaths(List<TypeModel> allTypes)
    {
        var resolutionService = new TypeResolutionService(this);
        resolutionService.PopulateBaseTypePaths(allTypes);
    }

    // Transaction management
    public void SaveChanges()
    {
        _context.SaveChanges();
    }

    public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction BeginTransaction()
    {
        return _context.Database.BeginTransaction();
    }


    public List<TypeModel> GetTypesForGroup(string baseName, string ns)
    {
        var targetFqn = string.IsNullOrEmpty(ns) ? baseName : $"{ns}::{baseName}";

        return _context.Types.AsNoTracking()
            .Where(t => (t.BaseName == baseName && t.Namespace == ns) ||
                        (t.BaseTypePath != null && t.BaseTypePath == targetFqn))
            .Include(t => t.TemplateArguments)
            .ThenInclude(ta => ta.TypeReference)
            .Include(t => t.BaseTypes)
            .ThenInclude(bt => bt.RelatedType)
            .Include(t => t.StructMembers)
            .ThenInclude(sm => sm.TypeReference)
            .Include(t => t.StructMembers)
            .ThenInclude(sm => sm.FunctionSignature)
            .ThenInclude(fs => fs!.Parameters)
            .Include(t => t.StaticVariables)
            .ThenInclude(sv => sv.TypeReference)
            .ToList();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context.Dispose();
        }

        _disposed = true;
    }

    public List<(int Id, string BaseName, string Namespace, string? StoredFqn)> GetTypeLookupData()
    {
        return _context.Types.AsNoTracking()
            .Where(t => !t.IsIgnored)
            .Select(t => new { t.Id, t.BaseName, t.Namespace, Fqn = t.StoredFullyQualifiedName })
            .ToList()
            .Select(t => (t.Id, t.BaseName, t.Namespace, (string?)t.Fqn))
            .ToList();
    }

    public Dictionary<int, List<TypeInheritance>> GetBaseTypesForMultipleTypes(IEnumerable<int> typeIds)
    {
        var idSet = typeIds.ToHashSet();
        return _context.TypeInheritances
            .Include(ti => ti.RelatedType)
            .Where(ti => idSet.Contains(ti.ParentTypeId))
            .ToList()
            .GroupBy(ti => ti.ParentTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public Dictionary<int, List<StructMemberModel>> GetStructMembersForMultipleTypes(IEnumerable<int> typeIds)
    {
        var idSet = typeIds.ToHashSet();
        return _context.StructMembers
            .Include(sm => sm.TypeReference)
            .Include(sm => sm.FunctionSignature)
            .ThenInclude(fs => fs!.Parameters)
            .Where(sm => idSet.Contains(sm.StructTypeId))
            .ToList()
            .GroupBy(sm => sm.StructTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public int InsertFunctionBody(FunctionBodyModel functionBody)
    {
        _context.FunctionBodies.Add(functionBody);
        return functionBody.Id;
    }

    public List<FunctionBodyModel> GetFunctionBodiesForType(int typeId)
    {
        return _context.FunctionBodies
            .Include(fb => fb.FunctionSignature)
            .ThenInclude(fs => fs.Parameters)
            .Where(fb => fb.ParentId == typeId)
            .ToList();
    }

    public Dictionary<int, List<FunctionBodyModel>> GetFunctionBodiesForMultipleTypes(IEnumerable<int> typeIds)
    {
        var idSet = typeIds.ToHashSet();
        return _context.FunctionBodies
            .Include(fb => fb.FunctionSignature)
            .ThenInclude(fs => fs.Parameters)
            .Where(fb => fb.ParentId.HasValue && idSet.Contains(fb.ParentId.Value))
            .ToList()
            .GroupBy(fb => fb.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public int InsertStaticVariable(StaticVariableModel staticVariable)
    {
        _context.StaticVariables.Add(staticVariable);
        return (int)staticVariable.Id;
    }

    public void InsertStaticVariables(IEnumerable<StaticVariableModel> staticVariables)
    {
        _context.StaticVariables.AddRange(staticVariables);
    }

    public List<StaticVariableModel> GetStaticVariablesForType(int typeId)
    {
        return _context.StaticVariables
            .Include(sv => sv.TypeReference)
            .Where(sv => sv.ParentTypeId == typeId)
            .ToList();
    }
}
