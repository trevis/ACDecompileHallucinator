using ACDecompileParser.Shared.Lib.Constants;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Shared.Lib.Services;

public class TypeResolutionService
{
    private readonly ITypeRepository _repository;
    private readonly IProgressReporter? _progressReporter;

    public TypeResolutionService(ITypeRepository repository, IProgressReporter? progressReporter = null)
    {
        _repository = repository;
        _progressReporter = progressReporter;
    }

    public void ResolveTypeReferences()
    {
        _progressReporter?.Start("Resolving Type References", 3); // 3 major steps

        // PERFORMANCE: Load all type references once and build lookup
        var typeReferences = _repository.GetAllTypeReferences();

        // Build lookup, handling potential duplicate TypeStrings (take first occurrence)
        var typeRefLookup = new Dictionary<string, TypeReference>();
        foreach (var typeRef in typeReferences)
        {
            if (!string.IsNullOrEmpty(typeRef.TypeString) && !typeRefLookup.ContainsKey(typeRef.TypeString))
            {
                typeRefLookup[typeRef.TypeString] = typeRef;
            }
        }

        // Track which entities we've already updated to avoid duplicate Update() calls
        var updatedTypeRefIds = new HashSet<int>();

        _progressReporter?.Report(0, "Resolving Type References...");

        // First, resolve type references to TypeReferences table
        var updatedTypeReferences = new List<TypeReference>();
        foreach (var typeRef in typeReferences)
        {
            if (!string.IsNullOrEmpty(typeRef.TypeString) && typeRef.ReferencedTypeId == null)
            {
                // Check if it's a primitive type - if so, skip resolution
                if (IsPrimitiveType(typeRef.TypeString))
                {
                    // Set ReferencedTypeId to null to indicate it's a primitive and not an unresolved reference
                    // We don't need to do anything special here since it's already null
                    continue;
                }

                // Use the pre-computed FullyQualifiedType for matching against the Types table
                var fullyQualifiedType = typeRef.FullyQualifiedType;

                // Try to resolve using the fully qualified type first
                var referencedType = _repository.GetTypeByFullyQualifiedName(fullyQualifiedType);

                // If direct lookup failed, try to resolve through typedef chain
                if (referencedType == null)
                {
                    var resolvedTypeRef = _repository.ResolveTypeDefChain(fullyQualifiedType);
                    if (resolvedTypeRef?.ReferencedTypeId != null)
                    {
                        // Use the type that the typedef chain resolves to
                        referencedType = _repository.GetTypeById(resolvedTypeRef.ReferencedTypeId.Value);
                    }
                }

                // If still not found, try with the original TypeString extraction as fallback
                if (referencedType == null)
                {
                    // Extract the base type name from the type string (remove modifiers like *, &, const)
                    var baseTypeName = ParsingUtilities.ExtractBaseTypeName(typeRef.TypeString);

                    // Try to resolve using the original base type name
                    referencedType = _repository.GetTypeByFullyQualifiedName(baseTypeName);

                    // If direct lookup failed, try to resolve through typedef chain
                    if (referencedType == null)
                    {
                        var resolvedTypeRef = _repository.ResolveTypeDefChain(baseTypeName);
                        if (resolvedTypeRef?.ReferencedTypeId != null)
                        {
                            // Use the type that the typedef chain resolves to
                            referencedType = _repository.GetTypeById(resolvedTypeRef.ReferencedTypeId.Value);
                        }
                    }

                    // If still not found, try with normalized type string
                    if (referencedType == null)
                    {
                        var normalizedBaseTypeName = ParsingUtilities.NormalizeTypeString(baseTypeName);
                        if (normalizedBaseTypeName != baseTypeName)
                        {
                            referencedType = _repository.GetTypeByFullyQualifiedName(normalizedBaseTypeName);

                            // If still not found, try with the base name from the normalized string
                            if (referencedType == null)
                            {
                                var normalizedExtractedBaseName =
                                    ParsingUtilities.ExtractBaseTypeName(normalizedBaseTypeName);
                                if (normalizedExtractedBaseName != normalizedBaseTypeName)
                                {
                                    referencedType =
                                        _repository.GetTypeByFullyQualifiedName(normalizedExtractedBaseName);
                                }
                            }
                        }
                    }
                }

                if (referencedType != null && !updatedTypeRefIds.Contains(typeRef.Id))
                {
                    typeRef.ReferencedTypeId = referencedType.Id;
                    updatedTypeReferences.Add(typeRef);
                    updatedTypeRefIds.Add(typeRef.Id);
                }
            }
        }

        // Batch update type references - just update in memory, caller will save
        foreach (var typeRef in updatedTypeReferences)
        {
            _repository.UpdateTypeReference(typeRef);
        }

        // Now resolve TypeTemplateArgument relationships
        _progressReporter?.Report(1, "Resolving Template Arguments...");
        var templateArgs = _repository.GetAllTypeTemplateArguments();
        var updatedTemplateArgs = new List<TypeTemplateArgument>();
        foreach (var ta in templateArgs)
        {
            if (ta.TypeReferenceId == null && !string.IsNullOrEmpty(ta.TypeString))
            {
                // Check if it's a primitive type - if so, skip resolution
                if (IsPrimitiveType(ta.TypeString))
                {
                    continue;
                }

                // Create a TypeReference from the RelatedTypeString and save it using the shared utility
                var typeReference = TypeReferenceUtilities.CreateTypeReference(ta.TypeString);

                // PERFORMANCE: Use in-memory lookup instead of querying database
                if (typeRefLookup.TryGetValue(typeReference.TypeString, out var existingTypeRef))
                {
                    // Use navigation property as the ID might be 0 if it was just added in this batch
                    ta.TypeReference = existingTypeRef;
                }
                else
                {
                    // Insert new type reference
                    _repository.InsertTypeReference(typeReference);

                    // IMPORTANT: Use navigation property so EF Core handles the ID propagation 
                    // because the ID is likely not generated yet (it's 0)
                    ta.TypeReference = typeReference;

                    // Add to lookup for subsequent iterations
                    typeRefLookup[typeReference.TypeString] = typeReference;
                }

                updatedTemplateArgs.Add(ta);
            }
        }

        // Batch update template arguments - just update in memory, caller will save
        foreach (var ta in updatedTemplateArgs)
        {
            _repository.UpdateTypeTemplateArgument(ta);
        }

        // Now resolve TypeInheritance relationships
        _progressReporter?.Report(2, "Resolving Inheritance...");
        var inheritances = _repository.GetAllTypeInheritances();
        var updatedInheritances = new List<TypeInheritance>();
        foreach (var inh in inheritances)
        {
            if (inh.RelatedTypeId == null && !string.IsNullOrEmpty(inh.RelatedTypeString))
            {
                // Check if it's a primitive type - if so, skip resolution
                if (IsPrimitiveType(inh.RelatedTypeString))
                {
                    continue;
                }

                var referencedType = _repository.GetTypeByFullyQualifiedName(inh.RelatedTypeString);

                // If direct lookup failed, try to resolve through typedef chain
                if (referencedType == null)
                {
                    var resolvedTypeRef = _repository.ResolveTypeDefChain(inh.RelatedTypeString);
                    if (resolvedTypeRef?.ReferencedTypeId != null)
                    {
                        // Use the type that the typedef chain resolves to
                        referencedType = _repository.GetTypeById(resolvedTypeRef.ReferencedTypeId.Value);
                    }
                }

                // If still not found, try with normalized type string
                if (referencedType == null)
                {
                    var normalizedTypeString = ParsingUtilities.NormalizeTypeString(inh.RelatedTypeString);
                    if (normalizedTypeString != inh.RelatedTypeString)
                    {
                        referencedType = _repository.GetTypeByFullyQualifiedName(normalizedTypeString);
                    }
                }

                if (referencedType != null)
                {
                    inh.RelatedTypeId = referencedType.Id;
                    updatedInheritances.Add(inh);
                }
            }
        }

        // Batch update inheritances - just update in memory, caller will save
        foreach (var inh in updatedInheritances)
        {
            _repository.UpdateTypeInheritance(inh);
        }

        // StructMemberModel relationships are now handled through TypeReferenceId
        // No additional resolution needed as TypeReferenceId is already set properly
        _progressReporter?.Finish("Resolution completed.");
    }

    /// <summary>
    /// Manually resolves typedef chains for all type references.
    /// Call this method explicitly when you want to resolve typedefs to their underlying types.
    /// </summary>
    public void ResolveTypeDefReferences()
    {
        var typeReferences = _repository.GetAllTypeReferences();

        foreach (var typeRef in typeReferences)
        {
            if (string.IsNullOrEmpty(typeRef.TypeString))
                continue;

            var baseTypeName = ParsingUtilities.ExtractBaseTypeName(typeRef.TypeString);
            var resolvedType = _repository.ResolveTypeDefChain(baseTypeName);

            if (resolvedType != null && typeRef.ReferencedTypeId == null)
            {
                typeRef.ReferencedTypeId = resolvedType.ReferencedTypeId;
                _repository.UpdateTypeReference(typeRef);
            }
        }
    }

    public int GetUnresolvedTypeCount()
    {
        var unresolvedCount = 0;

        // Count unresolved TypeReferences, excluding primitive types
        unresolvedCount += _repository.GetAllTypeReferences()
            .Count(tr => tr.ReferencedTypeId == null && !string.IsNullOrEmpty(tr.TypeString) &&
                         !IsPrimitiveType(tr.TypeString));

        // Count unresolved TypeTemplateArguments, excluding primitive types
        unresolvedCount += _repository.GetAllTypeTemplateArguments()
            .Count(ta => ta.TypeReferenceId == null && !string.IsNullOrEmpty(ta.TypeString) &&
                         !IsPrimitiveType(ta.TypeString));

        // Count unresolved TypeInheritances, excluding primitive types
        unresolvedCount += _repository.GetAllTypeInheritances()
            .Count(ti => ti.RelatedTypeId == null && !string.IsNullOrEmpty(ti.RelatedTypeString) &&
                         !IsPrimitiveType(ti.RelatedTypeString));

        // Count unresolved StructMemberModel types - no MemberTypeId anymore, so this count is now 0
        // StructMemberModel types are now handled through TypeReferenceId which is handled in the TypeReference count above

        return unresolvedCount;
    }

    public List<string> GetSampleUnresolvedTypeReferences(int maxCount = 5)
    {
        var unresolvedRefs = new List<string>();

        // Get sample unresolved TypeReferences, excluding primitive types
        var typeRefs = _repository.GetAllTypeReferences()
            .Where(tr => tr.ReferencedTypeId == null && !string.IsNullOrEmpty(tr.TypeString) &&
                         !IsPrimitiveType(tr.TypeString))
            .Take(maxCount - unresolvedRefs.Count)
            .Select(tr => $"TypeReference: {tr.TypeString}");
        unresolvedRefs.AddRange(typeRefs);

        if (unresolvedRefs.Count < maxCount)
        {
            // Get sample unresolved TypeTemplateArguments, excluding primitive types
            var templateArgs = _repository.GetAllTypeTemplateArguments()
                .Where(ta => ta.TypeReferenceId == null && !string.IsNullOrEmpty(ta.TypeString) &&
                             !IsPrimitiveType(ta.TypeString))
                .Take(maxCount - unresolvedRefs.Count)
                .Select(ta => $"TemplateArgument: {ta.TypeString}");
            unresolvedRefs.AddRange(templateArgs);
        }

        if (unresolvedRefs.Count < maxCount)
        {
            // Get sample unresolved TypeInheritances, excluding primitive types
            var inheritances = _repository.GetAllTypeInheritances()
                .Where(ti => ti.RelatedTypeId == null && !string.IsNullOrEmpty(ti.RelatedTypeString) &&
                             !IsPrimitiveType(ti.RelatedTypeString))
                .Take(maxCount - unresolvedRefs.Count)
                .Select(ti => $"Inheritance: {ti.RelatedTypeString}");
            unresolvedRefs.AddRange(inheritances);
        }

        return unresolvedRefs;
    }

    public List<DetailedUnresolvedReference> GetDetailedUnresolvedTypeReferences(int maxCount = 5)
    {
        var detailedRefs = new List<DetailedUnresolvedReference>();

        // Get all types to map type IDs to names
        var allTypes = _repository.GetAllTypes();
        var typeLookup = allTypes.ToDictionary(t => t.Id, t => t);

        // Get all struct members to map type reference IDs to struct members
        var allStructMembers = _repository.GetAllStructMembers();
        var structMemberLookup = allStructMembers
            .Where(sm => sm.TypeReferenceId.HasValue)
            .GroupBy(sm => sm.TypeReferenceId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all template arguments to map type reference IDs to template arguments
        var allTemplateArgs = _repository.GetAllTypeTemplateArguments();
        var templateArgLookup = allTemplateArgs
            .Where(ta => ta.TypeReferenceId.HasValue)
            .GroupBy(ta => ta.TypeReferenceId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all inheritance relationships to map type reference IDs to inheritance relationships
        var allInheritances = _repository.GetAllTypeInheritances();
        // Create a lookup for inheritances where the RelatedTypeString matches unresolved type strings
        var inheritanceLookup = allInheritances
            .Where(i => i.RelatedTypeId == null) // Unresolved
            .GroupBy(i => i.RelatedTypeString)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all function signatures to map type reference IDs to function signature return types
        var allFunctionSignatures = _repository.GetAllFunctionSignatures();
        var functionSignatureLookup = allFunctionSignatures
            .Where(fs => fs.ReturnTypeReferenceId.HasValue)
            .GroupBy(fs => fs.ReturnTypeReferenceId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all function parameters to map type reference IDs to function parameter types
        var allFunctionParams = _repository.GetAllFunctionParameters();
        var functionParamLookup = allFunctionParams
            .Where(fp => fp.TypeReferenceId.HasValue)
            .GroupBy(fp => fp.TypeReferenceId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all typedefs to map type reference IDs to typedefs
        var allTypedefs = _repository.GetAllTypeDefs();
        var typedefLookup = allTypedefs
            .GroupBy(t => t.TypeReferenceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get sample unresolved TypeReferences, excluding primitive types
        var typeRefs = _repository.GetAllTypeReferences()
            .Where(tr => tr.ReferencedTypeId == null && !string.IsNullOrEmpty(tr.TypeString) &&
                         !IsPrimitiveType(tr.TypeString))
            .Take(maxCount)
            .ToList();

        foreach (var typeRef in typeRefs)
        {
            // Find which struct member, template argument, inheritance relationship, function signature, or function parameter uses this type reference
            var detailedRefsForThisType = new List<DetailedUnresolvedReference>();

            // Check if this type reference is used by struct members
            if (structMemberLookup.TryGetValue(typeRef.Id, out var structMembers))
            {
                foreach (var structMember in structMembers)
                {
                    if (typeLookup.TryGetValue(structMember.StructTypeId, out var parentType))
                    {
                        detailedRefsForThisType.Add(new DetailedUnresolvedReference
                        {
                            TypeString = typeRef.TypeString,
                            EntityType = "StructMember",
                            ParentEntityName = parentType.FullyQualifiedName,
                            MemberName = structMember.Name,
                            Context = $"offset 0x{structMember.Offset?.ToString("X") ?? "unknown"}",
                            File = parentType.File,
                            LineNumber = structMember.LineNumber ?? parentType.LineNumber
                        });
                    }
                }
            }

            // Check template arguments that use this type reference
            if (templateArgLookup.TryGetValue(typeRef.Id, out var templateArgs))
            {
                foreach (var templateArg in templateArgs)
                {
                    if (typeLookup.TryGetValue(templateArg.ParentTypeId, out var parentType))
                    {
                        detailedRefsForThisType.Add(new DetailedUnresolvedReference
                        {
                            TypeString = typeRef.TypeString,
                            EntityType = "TemplateArgument",
                            ParentEntityName = parentType.FullyQualifiedName,
                            Context = $"position {templateArg.Position} in template",
                            File = parentType.File,
                            LineNumber = parentType.LineNumber
                        });
                    }
                }
            }

            // Check inheritance relationships where the RelatedTypeString matches this type string
            if (inheritanceLookup.TryGetValue(typeRef.TypeString, out var inheritances))
            {
                foreach (var inheritance in inheritances)
                {
                    if (typeLookup.TryGetValue(inheritance.ParentTypeId, out var derivedType))
                    {
                        detailedRefsForThisType.Add(new DetailedUnresolvedReference
                        {
                            TypeString = typeRef.TypeString,
                            EntityType = "Inheritance",
                            ParentEntityName = derivedType.FullyQualifiedName,
                            Context = "base type in inheritance",
                            File = derivedType.File,
                            LineNumber = derivedType.LineNumber
                        });
                    }
                }
            }

            // Check function signatures that use this type reference as return type
            if (functionSignatureLookup.TryGetValue(typeRef.Id, out var functionSignatures))
            {
                foreach (var functionSignature in functionSignatures)
                {
                    // Find the struct member that uses this function signature (if any)
                    var parentStructMember =
                        allStructMembers.FirstOrDefault(sm => sm.FunctionSignatureId == functionSignature.Id);
                    if (parentStructMember != null)
                    {
                        if (typeLookup.TryGetValue(parentStructMember.StructTypeId, out var parentType))
                        {
                            detailedRefsForThisType.Add(new DetailedUnresolvedReference
                            {
                                TypeString = typeRef.TypeString,
                                EntityType = "FunctionSignature.ReturnType",
                                ParentEntityName = parentType.FullyQualifiedName,
                                MemberName = parentStructMember.Name,
                                Context = $"return type for function pointer member",
                                File = parentType.File,
                                LineNumber = parentStructMember.LineNumber ?? parentType.LineNumber
                            });
                        }
                    }
                    else
                    {
                        // Function signature not directly tied to a struct member, might be in parameters
                        detailedRefsForThisType.Add(new DetailedUnresolvedReference
                        {
                            TypeString = typeRef.TypeString,
                            EntityType = "FunctionSignature.ReturnType",
                            ParentEntityName = "Unknown",
                            MemberName = "Unknown",
                            Context = "return type in function signature"
                        });
                    }
                }
            }

            // Check function parameters that use this type reference
            if (functionParamLookup.TryGetValue(typeRef.Id, out var functionParams))
            {
                foreach (var functionParam in functionParams)
                {
                    // Find the parent struct member or function signature that uses this parameter
                    if (functionParam.ParentFunctionSignatureId.HasValue)
                    {
                        // This parameter belongs to a function signature
                        var parentFunctionSignature = allFunctionSignatures.FirstOrDefault(fs =>
                            fs.Id == functionParam.ParentFunctionSignatureId.Value);
                        if (parentFunctionSignature != null)
                        {
                            // Find the struct member that uses this function signature (if any)
                            var parentStructMember = allStructMembers.FirstOrDefault(sm =>
                                sm.FunctionSignatureId == parentFunctionSignature.Id);
                            if (parentStructMember != null)
                            {
                                if (typeLookup.TryGetValue(parentStructMember.StructTypeId, out var parentType))
                                {
                                    detailedRefsForThisType.Add(new DetailedUnresolvedReference
                                    {
                                        TypeString = typeRef.TypeString,
                                        EntityType = "FunctionParam",
                                        ParentEntityName = parentType.FullyQualifiedName,
                                        MemberName = parentStructMember.Name,
                                        Context = $"parameter {functionParam.Position} in function pointer member"
                                    });
                                }
                            }
                            else
                            {
                                detailedRefsForThisType.Add(new DetailedUnresolvedReference
                                {
                                    TypeString = typeRef.TypeString,
                                    EntityType = "FunctionParam",
                                    ParentEntityName = "Unknown",
                                    MemberName = "Unknown",
                                    Context = $"parameter {functionParam.Position} in function signature"
                                });
                            }
                        }
                    }
                    else
                    {
                        // Parameter not tied to any specific parent
                        detailedRefsForThisType.Add(new DetailedUnresolvedReference
                        {
                            TypeString = typeRef.TypeString,
                            EntityType = "FunctionParam",
                            ParentEntityName = "Unknown",
                            MemberName = "Unknown",
                            Context = $"parameter {functionParam.Position} in unknown context"
                        });
                    }
                }
            }

            // Check if this type reference is used by typedefs
            if (typedefLookup.TryGetValue(typeRef.Id, out var typedefs))
            {
                foreach (var typedef in typedefs)
                {
                    detailedRefsForThisType.Add(new DetailedUnresolvedReference
                    {
                        TypeString = typeRef.TypeString,
                        EntityType = "Typedef",
                        ParentEntityName = typedef.FullyQualifiedName,
                        File = typedef.File,
                        LineNumber = typedef.LineNumber
                    });
                }
            }

            // If no specific context was found, create a basic reference
            if (detailedRefsForThisType.Count == 0)
            {
                // Let's also check if this type reference ID is used by any struct members that might not have been found in the initial lookup
                var fallbackStructMembers = allStructMembers.Where(sm => sm.TypeReferenceId == typeRef.Id).ToList();
                if (fallbackStructMembers.Any())
                {
                    // Found struct members that use this type reference - try to get parent type info
                    var firstMember = fallbackStructMembers.First();
                    var parentType = typeLookup.TryGetValue(firstMember.StructTypeId, out var type) ? type : null;

                    detailedRefsForThisType.Add(new DetailedUnresolvedReference
                    {
                        TypeString = typeRef.TypeString,
                        EntityType = "StructMember",
                        ParentEntityName = parentType?.FullyQualifiedName ?? "Unknown",
                        MemberName = firstMember.Name,
                        Context = $"member of struct (fallback lookup)",
                        File = parentType?.File,
                        LineNumber = firstMember.LineNumber ?? parentType?.LineNumber
                    });
                }
                else
                {
                    detailedRefsForThisType.Add(new DetailedUnresolvedReference
                    {
                        TypeString = typeRef.TypeString,
                        EntityType = "TypeReference",
                        ParentEntityName = "Unknown",
                        MemberName = null,
                        Context = "unmapped reference",
                        File = typeRef.File, // Use the TypeReference's own file/line if available
                        LineNumber = typeRef.LineNumber
                    });
                }
            }

            // Add all references for this type string to the main list
            detailedRefs.AddRange(detailedRefsForThisType);

            // Stop if we've reached the max count
            if (detailedRefs.Count >= maxCount)
            {
                detailedRefs = detailedRefs.Take(maxCount).ToList();
                break;
            }
        }

        return detailedRefs;
    }

    /// <summary>
    /// Checks if a type string represents a primitive type that should be excluded from unresolved counts
    /// </summary>
    private bool IsPrimitiveType(string typeString)
    {
        if (string.IsNullOrWhiteSpace(typeString))
            return false;

        // Normalize the type string to handle spacing issues
        var normalizedType = ParsingUtilities.NormalizeTypeString(typeString);

        // Check if it's an exact match for a primitive type
        if (PrimitiveTypes.TypeNames.Contains(normalizedType))
            return true;

        // Check if it's a multi-word primitive type combination (like "unsigned int")
        if (PrimitiveTypes.TypeCombinations.Contains(normalizedType))
            return true;

        // Extract the base type name (removing modifiers like *, &, const) and check again
        var baseTypeName = ParsingUtilities.ExtractBaseTypeName(normalizedType);
        if (PrimitiveTypes.TypeNames.Contains(baseTypeName))
            return true;

        // Check if the base type name is a multi-word combination
        if (PrimitiveTypes.TypeCombinations.Contains(baseTypeName))
            return true;

        return false;
    }

    public void PopulateBaseTypePaths(List<TypeModel> allTypes)
    {
        // PERFORMANCE: Build lookups once to avoid O(N^2) complexity with repeated linear searches
        // 1. Lookup by Fully Qualified Name
        var fqnLookup = new Dictionary<string, TypeModel>();
        foreach (var t in allTypes)
        {
            if (!string.IsNullOrEmpty(t.StoredFullyQualifiedName) && !fqnLookup.ContainsKey(t.StoredFullyQualifiedName))
            {
                fqnLookup[t.StoredFullyQualifiedName] = t;
            }
        }

        // 2. Lookup by (BaseName, Namespace)
        // Grouping by BaseName first might be efficient enough, or use a composite key
        var baseNameNamespaceLookup = new Dictionary<(string BaseName, string Namespace), TypeModel>();
        foreach (var t in allTypes)
        {
            var key = (t.BaseName, t.Namespace);
            if (!baseNameNamespaceLookup.ContainsKey(key))
            {
                baseNameNamespaceLookup[key] = t;
            }
        }

        // Collect all the updates first, then apply them
        var updates = new List<(int typeId, string basePath)>();
        foreach (var type in allTypes)
        {
            // Determine the base type path based on the type's namespace and inheritance
            var path = DetermineBaseTypePath(type, fqnLookup, baseNameNamespaceLookup);
            updates.Add((type.Id, path));
        }

        // Apply all updates
        foreach (var (typeId, basePath) in updates)
        {
            _repository.UpdateBaseTypePath(typeId, basePath);
        }
    }

    private string DetermineBaseTypePath(TypeModel type,
        Dictionary<string, TypeModel> fqnLookup,
        Dictionary<(string BaseName, string Namespace), TypeModel> baseNameNamespaceLookup)
    {
        // Debug logging for specific types
        if (type.BaseName == "ACCmdInterp" || type.BaseName == "ACCmdInterp_vtbl" ||
            type.BaseName == "IInputActionCallback")
        {
            Console.WriteLine(
                $"[DEBUG] DetermineBaseTypePath for {type.BaseName} (NS: '{type.Namespace}', FQN: '{type.StoredFullyQualifiedName}')");
        }

        // For a vtable, group with its parent struct (remove "_vtbl" suffix)
        if (type.BaseName.EndsWith("_vtbl"))
        {
            var parentName = type.BaseName.Substring(0, type.BaseName.Length - 5); // Remove "_vtbl"

            // First, look for the parent in the same namespace
            if (baseNameNamespaceLookup.TryGetValue((parentName, type.Namespace), out var parentType))
            {
                // For vtables of nested types, we want to group them with the root parent
                // Find the root parent for this type
                var rootParent = FindRootParent(parentType, fqnLookup, baseNameNamespaceLookup);
                if (rootParent != null)
                {
                    return rootParent.StoredFullyQualifiedName;
                }

                return parentType.StoredFullyQualifiedName;
            }
        }

        // For nested types, use the root parent type's path
        // First, check if the namespace itself contains "::" indicating deeper nesting
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            // If the namespace has "::", we need to find the root parent
            if (type.Namespace.Contains("::"))
            {
                var rootParent = FindRootParentFromNamespace(type.Namespace, fqnLookup, baseNameNamespaceLookup);
                if (rootParent != null)
                {
                    return rootParent.StoredFullyQualifiedName;
                }
            }
            else
            {
                // Simple namespace, look for a parent type that matches the namespace
                if (fqnLookup.TryGetValue(type.Namespace, out var parentType))
                {
                    var rootParent = FindRootParent(parentType, fqnLookup, baseNameNamespaceLookup);
                    return rootParent != null
                        ? rootParent.StoredFullyQualifiedName
                        : parentType.StoredFullyQualifiedName;
                }
            }
        }

        // For types with "::" in their BaseName (e.g., when Namespace is empty but BaseName has "::")
        // This handles cases where the full name is in BaseName like "ActionState::SingleKeyInfo"
        if (type.BaseName.Contains("::"))
        {
            var parts = type.BaseName.Split(new[] { "::" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                // Get the root part (the first part of the namespace chain)
                var rootName = parts[0];

                // Look for the root type by fully qualified name
                if (fqnLookup.TryGetValue(rootName, out var rootType))
                {
                    return rootType.StoredFullyQualifiedName;
                }

                // If not found directly, try to find it by BaseName
                if (baseNameNamespaceLookup.TryGetValue((rootName, string.Empty), out var rootTypeByName))
                {
                    return rootTypeByName.StoredFullyQualifiedName;
                }
            }
        }

        // For nested types where the parent is in the same namespace
        // e.g., if we have "Parent::Child" and "Parent" exists as a separate type
        // This also handles nested types within template instantiations
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            // Look for the parent type in allTypes based on the namespace
            // For example, if this type is "PQueueNode" with namespace "AC1Legacy::PQueueArray<double>"
            // We want to find the type with FullyQualifiedName "AC1Legacy::PQueueArray<double>"
            if (fqnLookup.TryGetValue(type.Namespace, out var parentType))
            {
                // This nested type should be grouped with its parent
                return parentType.StoredFullyQualifiedName;
            }

            // If not found directly, try to find the parent by extracting the base name
            // from the namespace (removing template parameters)
            var parentBaseName = ParsingUtilities.ExtractBaseTypeName(type.Namespace);

            if (baseNameNamespaceLookup.TryGetValue((parentBaseName, string.Empty), out var parentTypeByBaseName))
            {
                // Check if this parent type's StoredFullyQualifiedName matches the namespace
                if (parentTypeByBaseName.StoredFullyQualifiedName == type.Namespace)
                {
                    return parentTypeByBaseName.StoredFullyQualifiedName;
                }
            }
        }

        // Default to the type's own fully qualified name
        return type.StoredFullyQualifiedName;
    }

    private TypeModel? FindRootParent(TypeModel type,
        Dictionary<string, TypeModel> fqnLookup,
        Dictionary<(string BaseName, string Namespace), TypeModel> baseNameNamespaceLookup)
    {
        // Find the root parent by traversing up the namespace hierarchy
        // For example, for AsyncCache::CAsyncRequest::CCallbackWrapper, 
        // we want to find the root type AsyncCache

        string currentNamespace = type.Namespace;
        string fullyQualifiedName = type.StoredFullyQualifiedName;

        // If the fully qualified name contains "::", extract the root
        if (fullyQualifiedName.Contains("::"))
        {
            var parts = fullyQualifiedName.Split(new[] { "::" }, StringSplitOptions.None);
            var rootName = parts[0];

            // Look for the root type in allTypes (BaseName match with empty namespace)
            if (baseNameNamespaceLookup.TryGetValue((rootName, string.Empty), out var rootType))
            {
                return rootType;
            }

            // If not found with empty namespace, look for it by fully qualified name
            if (fqnLookup.TryGetValue(rootName, out var rootTypeByFqn))
            {
                return rootTypeByFqn;
            }
        }

        // If the current type's namespace contains "::", we need to find its root
        if (!string.IsNullOrEmpty(currentNamespace) && currentNamespace.Contains("::"))
        {
            var namespaceParts = currentNamespace.Split(new[] { "::" }, StringSplitOptions.None);
            var rootNamespace = namespaceParts[0];

            if (baseNameNamespaceLookup.TryGetValue((rootNamespace, string.Empty), out var rootType))
            {
                return rootType;
            }
        }

        // If the current type is itself the root (no namespace or simple namespace)
        if (string.IsNullOrEmpty(currentNamespace) || !currentNamespace.Contains("::"))
        {
            if (fqnLookup.TryGetValue(fullyQualifiedName, out var rootType))
            {
                return rootType;
            }
        }

        return type; // Default fallback
    }

    private TypeModel? FindRootParentFromNamespace(string namespaceName,
        Dictionary<string, TypeModel> fqnLookup,
        Dictionary<(string BaseName, string Namespace), TypeModel> baseNameNamespaceLookup)
    {
        // Extract the root from a nested namespace like "AsyncCache::CAsyncRequest"
        if (namespaceName.Contains("::"))
        {
            var parts = namespaceName.Split(new[] { "::" }, StringSplitOptions.None);
            var rootName = parts[0];

            // Look for the root type by BaseName with empty namespace
            if (baseNameNamespaceLookup.TryGetValue((rootName, string.Empty), out var rootType))
            {
                return rootType;
            }

            // Fallback: look for the root by FullyQualifiedName
            if (fqnLookup.TryGetValue(rootName, out var rootTypeByFqn))
            {
                return rootTypeByFqn;
            }
        }

        // If namespace doesn't contain "::", it might be the root itself
        if (fqnLookup.TryGetValue(namespaceName, out var type))
        {
            return type;
        }

        return null;
    }
}
