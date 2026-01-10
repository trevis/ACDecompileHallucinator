using ACDecompileParser.Shared.Lib.Constants;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output.CSharp;

/// <summary>
/// Generates C# bindings (unsafe structs) from TypeModel definitions.
/// </summary>
public class CSharpBindingsGenerator
{
    private readonly ITypeRepository? _repository;
    private readonly OffsetCalculationService? _offsetService;

    public CSharpBindingsGenerator(ITypeRepository? repository = null, OffsetCalculationService? offsetService = null)
    {
        _repository = repository;
        _offsetService = offsetService;

        // If repository is available but service is not, create one locally
        if (_offsetService == null && _repository != null)
        {
            _offsetService = new OffsetCalculationService(_repository);
        }
    }

    /// <summary>
    /// Generates C# binding code for a type as a string.
    /// </summary>
    public string Generate(TypeModel type)
    {
        var sb = new System.Text.StringBuilder();
        GenerateType(type, sb, 0);
        return sb.ToString();
    }

    /// <summary>
    /// Generates C# binding code for a list of types with namespace header.
    /// </summary>
    /// </summary>
    public string GenerateWithNamespace(List<TypeModel> types, string namespaceName = "ACBindings")
    {
        var sb = new System.Text.StringBuilder();

        // Track generated type names to prevent duplicates
        var generatedNames = new HashSet<string>();

        // Pre-processing: Group types by their intended generated name and pick the best candidate
        // This handles cases where we have both a forward declaration and a full definition in the same group.
        // We prioritize the one with members or base types.
        var distinctTypes = new List<TypeModel>();
        var typesByName = types
            .Where(t => t.ParentType == null)
            .GroupBy(t => GetGeneratedTypeName(t))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kvp in typesByName)
        {
            var candidates = kvp.Value;
            if (candidates.Count == 1)
            {
                distinctTypes.Add(candidates[0]);
            }
            else
            {
                // Pick the best candidate
                // 1. Prefer types with members
                // 2. Prefer types with base types
                // 3. Prefer types with largest source size (heuristic for content)
                // 4. Fallback to first
                var best = candidates
                    .OrderByDescending(t => (t.StructMembers?.Count ?? 0) + (t.FunctionBodies?.Count ?? 0))
                    .ThenByDescending(t => t.BaseTypes?.Count ?? 0)
                    .ThenByDescending(t => t.Source?.Length ?? 0)
                    .First();
                distinctTypes.Add(best);
            }
        }

        // Group types by their Namespace
        var namespaceGroups = distinctTypes
            .GroupBy(t => t.Namespace ?? string.Empty)
            .OrderBy(g => g.Key)
            .ToList();

        // Optimized case: Only one namespace group
        if (namespaceGroups.Count == 1)
        {
            var group = namespaceGroups.First();
            string subNs = group.Key.Replace("::", ".");
            string finalNs = string.IsNullOrEmpty(subNs) ? namespaceName : $"{namespaceName}.{subNs}";

            sb.AppendLine($"namespace {finalNs};");
            sb.AppendLine();

            foreach (var type in group)
            {
                // Duplicates already handled above
                GenerateType(type, sb, 0); // File-scoped namespace, start at 0
                sb.AppendLine();
            }
        }
        else
        {
            // Multiple namespaces - use block scoped
            foreach (var group in namespaceGroups)
            {
                string subNs = group.Key.Replace("::", ".");
                string finalNs = string.IsNullOrEmpty(subNs) ? namespaceName : $"{namespaceName}.{subNs}";

                sb.AppendLine($"namespace {finalNs}");
                sb.AppendLine("{");

                foreach (var type in group)
                {
                    GenerateType(type, sb, 1);
                    sb.AppendLine();
                }

                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private bool ShouldSkipDuplicate(TypeModel type, HashSet<string> generatedNames)
    {
        string key;
        if (type.Type == TypeType.Enum)
        {
            key = PrimitiveTypeMappings.CleanTypeName(type.BaseName);
        }
        else
        {
            // For structs/classes, use the full flattened name
            // (e.g. ACBindings.HashSet__uint)
            key = PrimitiveTypeMappings.MapType(type.NameWithTemplates);
        }

        if (generatedNames.Contains(key))
            return true;

        generatedNames.Add(key);
        return false;
    }


    private void GenerateType(TypeModel type, System.Text.StringBuilder sb, int indentLevel)
    {
        if (type.Type == TypeType.Enum)
        {
            GenerateEnum(type, sb, indentLevel);
        }
        else if (type.Type == TypeType.Struct || type.Type == TypeType.Class || type.Type == TypeType.Union)
        {
            GenerateStruct(type, sb, indentLevel);
        }
    }

    private static string GetGeneratedTypeName(TypeModel type)
    {
        if (type.Type == TypeType.Enum)
        {
            return PrimitiveTypeMappings.CleanTypeName(type.BaseName);
        }

        string flattenedName = PrimitiveTypeMappings.MapType(type.NameWithTemplates);
        // MapType returns fully qualified (ACBindings.DArray__int), we just want the struct name here
        int lastDot = flattenedName.LastIndexOf('.');
        if (lastDot != -1)
        {
            flattenedName = flattenedName.Substring(lastDot + 1);
        }

        return PrimitiveTypeMappings.CleanTypeName(flattenedName);
    }

    private void GenerateStruct(TypeModel type, System.Text.StringBuilder sb, int indentLevel)
    {
        // ... (this method start)
        string indent = new string(' ', indentLevel * 4);
        string memberIndent = new string(' ', (indentLevel + 1) * 4);

        // Check for destructor in hierarchy for IDisposable
        bool hasDestructor = HasDestructorInHierarchy(type);
        string interfaces = (hasDestructor && !type.IsGeneric) ? " : System.IDisposable" : "";

        // Struct declaration
        string safeBaseName = GetGeneratedTypeName(type);
        AppendXmlDocComment(sb, type.XmlDocComment, indent);
        sb.AppendLine($"{indent}// {type.FullyQualifiedName}");
        sb.AppendLine($"{indent}public unsafe struct {safeBaseName}{interfaces}");
        sb.AppendLine($"{indent}{{");

        bool hasContent = false;

        // Base Classes (composition)
        var baseTypeMap = GetDirectBaseTypes(type);
        if (type.BaseTypes != null && type.BaseTypes.Any())
        {
            sb.AppendLine($"{memberIndent}// Base Classes");
            foreach (var bt in type.BaseTypes)
            {
                TypeModel fallbackRelated = new TypeModel { BaseName = bt.RelatedTypeString ?? "Unknown" };
                TypeModel resolvedType = bt.RelatedType ?? fallbackRelated;
                // Use the specific instantiation string for templates to ensure flattening
                string baseTypeName;
                if (!string.IsNullOrEmpty(bt.RelatedTypeString) && bt.RelatedTypeString.Contains('<'))
                {
                    baseTypeName = PrimitiveTypeMappings.MapType(bt.RelatedTypeString);
                }
                else
                {
                    baseTypeName = GetFullyQualifiedName(resolvedType);
                }

                // Use a consistent sanitized field name based on FQN
                string rawFqn = resolvedType.FullyQualifiedName;
                if (string.IsNullOrEmpty(rawFqn) || rawFqn == "Unknown") rawFqn = bt.RelatedTypeString ?? "Unknown";

                // Mapped type for field name (to handle flattening in field names too if needed)
                string mappedBase = PrimitiveTypeMappings.MapType(rawFqn); // This might be ACBindings.Base__int
                string cleanedFqn = mappedBase.Replace("ACBindings.", "").Replace(".", "_");

                string fieldName = $"BaseClass_{cleanedFqn}";
                sb.AppendLine($"{memberIndent}public {baseTypeName} {fieldName}; // {baseTypeName}");
            }

            hasContent = true;
        }

        // Static variables
        if (type.StaticVariables != null && type.StaticVariables.Any())
        {
            if (hasContent) sb.AppendLine();
            sb.AppendLine($"{memberIndent}// Statics");
            foreach (var sv in type.StaticVariables)
            {
                GenerateStaticMember(sv, sb, indentLevel + 1);
            }

            hasContent = true;
        }

        // Nested types (child types, enums)
        if (type.NestedTypes != null && type.NestedTypes.Any())
        {
            if (hasContent) sb.AppendLine();
            sb.AppendLine($"{memberIndent}// Child Types");

            var generatedNestedTypes = new HashSet<string>();
            foreach (var nested in type.NestedTypes)
            {
                string nestedName = GetGeneratedTypeName(nested);
                if (generatedNestedTypes.Contains(nestedName))
                    continue;

                generatedNestedTypes.Add(nestedName);
                GenerateType(nested, sb, indentLevel + 1);
            }

            hasContent = true;
        }

        // Nested enums are handled via NestedTypes population

        // Struct Members
        var members = type.StructMembers;

        // If members list is empty, try loading from repository as fallback
        // This handles cases where the type instance might be a fresh object (e.g. from nested type linking)
        // that wasn't populated by the bulk pre-loading logic.
        if ((members == null || !members.Any()) && _repository != null)
        {
            var loadedMembers = _repository.GetStructMembersWithRelatedTypes(type.Id);
            if (loadedMembers != null && loadedMembers.Any())
            {
                members = loadedMembers.ToList();
                type.StructMembers = members; // Cache for future use
            }
        }

        if (members != null && members.Any())
        {
            if (hasContent) sb.AppendLine();
            sb.AppendLine($"{memberIndent}// Members");
            foreach (var member in members.OrderBy(m => m.DeclarationOrder))
            {
                GenerateMember(member, sb, indentLevel + 1);
            }

            hasContent = true;
        }

        // Methods (function bodies)
        var functionBodies = type.FunctionBodies ??
                             _repository?.GetFunctionBodiesForType(type.Id).ToList() ?? new List<FunctionBodyModel>();

        if (!type.IsGeneric)
        {
            // Generated Constructors
            var constructors = functionBodies.Where(fb => IsConstructor(fb, type.BaseName)).ToList();
            if (constructors.Any())
            {
                if (hasContent) sb.AppendLine();
                sb.AppendLine($"{memberIndent}// Generated Constructor");
                foreach (var ctor in constructors)
                {
                    GenerateCSharpConstructor(ctor, safeBaseName, sb, indentLevel + 1);
                }

                hasContent = true;
            }

            // Generated Dispose
            if (hasDestructor)
            {
                if (hasContent) sb.AppendLine();
                sb.AppendLine($"{memberIndent}// Generated Dispose");
                GenerateDispose(type, functionBodies, baseTypeMap, sb, indentLevel + 1);
                hasContent = true;
            }
        }

        if (hasContent) sb.AppendLine();
        sb.AppendLine($"{memberIndent}// Methods");

        // Native methods + Pulled up methods
        var allMethods = GetAllMethods(type, baseTypeMap);

        foreach (var (method, sourceType) in allMethods)
        {
            // Method generation might need to know the *flattened* name of the current type for constructors/destructors? 
            // IsConstructor/IsDestructor logic uses type.BaseName. 
            // But in C#, the constructor must be DArray__int.
            // So we need to handle that.

            GenerateMethod(method, sourceType, type, safeBaseName, sb, indentLevel + 1);
        }

        sb.AppendLine($"{indent}}}");
    }

    private Dictionary<string, TypeModel> GetDirectBaseTypes(TypeModel type)
    {
        var map = new Dictionary<string, TypeModel>();
        if (type.BaseTypes == null) return map;

        foreach (var bt in type.BaseTypes)
        {
            // RelatedType should already be loaded via GetBaseTypesForMultipleTypes()
            // which includes .Include(ti => ti.RelatedType)
            TypeModel? related = bt.RelatedType;

            // If null, create a minimal stub to avoid failures
            // (This shouldn't happen if data is pre-loaded correctly in CSharpGroupProcessor)
            if (related == null)
            {
                related = new TypeModel
                {
                    BaseName = bt.RelatedTypeString ?? "Unknown",
                    Namespace = type.Namespace
                    // Note: FullyQualifiedName is a computed property, don't set it directly
                };
            }

            // Use FullyQualifiedName as key to avoid name collisions across namespaces
            string key = related.FullyQualifiedName ?? bt.RelatedTypeString ?? "Unknown";
            if (!map.ContainsKey(key))
            {
                map[key] = related;
            }
        }

        return map;
    }

    private bool HasDestructorInHierarchy(TypeModel type)
    {
        // Only check this type's pre-loaded FunctionBodies - no database queries!
        // FunctionBodies should already be loaded by CSharpFileOutputGenerator
        if (type.FunctionBodies?.Any(fb => IsDestructor(fb, type.BaseName)) == true)
            return true;

        // Check bases recursively (using pre-loaded data only)
        var bases = type.BaseTypes ?? new List<TypeInheritance>();
        foreach (var bt in bases)
        {
            var baseType = bt.RelatedType;
            if (baseType != null && HasDestructorInHierarchy(baseType))
                return true;
        }

        return false;
    }

    private string GetFullyQualifiedName(TypeModel type)
    {
        if (type == null) return "ACBindings.Unknown";

        // For generic types, we must use the flattened name logic provided by MapType
        if (type.IsGeneric)
        {
            return PrimitiveTypeMappings.MapType(type.NameWithTemplates);
        }

        string ns = type.Namespace ?? string.Empty;
        string baseName = PrimitiveTypeMappings.CleanTypeName(type.BaseName ?? "Unknown").Replace("::", ".");
        string fqn;

        if (string.IsNullOrEmpty(ns))
            fqn = baseName;
        else
            fqn = $"{ns.Replace("::", ".")}.{baseName}";

        return $"ACBindings.{fqn}";
    }

// Returns methods directly defined on this type (not inherited)
    private List<(FunctionBodyModel Method, TypeModel SourceType)> GetAllMethods(TypeModel type,
        Dictionary<string, TypeModel> _)
    {
        var result = new List<(FunctionBodyModel, TypeModel)>();
        var signatures = new HashSet<string>();

        // Only add methods directly defined on this type
        var ownBodies = type.FunctionBodies ??
                        _repository?.GetFunctionBodiesForType(type.Id).ToList() ?? new List<FunctionBodyModel>();
        foreach (var body in ownBodies)
        {
            string sigKey = GetSignatureKey(body);
            if (!signatures.Contains(sigKey))
            {
                result.Add((body, type));
                signatures.Add(sigKey);
            }
        }

        // Base methods are NOT pulled up - they stay in their own base class structs
        // Users can access them via baseFieldInstance.Method() if needed

        return result;
    }

    private string GetSignatureKey(FunctionBodyModel body)
    {
        // Unique signature for overriding/hiding checks
        // Name + Params
        // Extract stripped name
        string name = ExtractMethodName(body.FullyQualifiedName);

        var sig = body.FunctionSignature;
        if (sig == null) return name;

        var paramTypes = string.Join(",",
            sig.Parameters?.OrderBy(p => p.Position).Select(p => p.ParameterType) ?? Array.Empty<string>());
        return $"{name}({paramTypes})";
    }

    private bool IsConstructor(FunctionBodyModel fb, string typeName)
    {
        string name = ExtractMethodName(fb.FullyQualifiedName);
        return name == typeName || name.StartsWith($"{typeName}<");
    }

    private bool IsDestructor(FunctionBodyModel fb, string typeName)
    {
        string name = ExtractMethodName(fb.FullyQualifiedName);
        if (!name.StartsWith("~")) return false;

        string dtorName = name.Substring(1);
        return dtorName == typeName || dtorName.StartsWith($"{typeName}<");
    }

    private void GenerateCSharpConstructor(FunctionBodyModel ctor, string typeName, System.Text.StringBuilder sb,
        int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        var parameters = ctor.FunctionSignature?.Parameters?.OrderBy(p => p.Position).ToList() ??
                         new List<FunctionParamModel>();

        // Remove 'this'
        if (parameters.Any() && IsThisParameter(parameters.First()))
            parameters.RemoveAt(0);

        var csParams = new List<string>();
        var callArgs = new List<string>();

        foreach (var p in parameters)
        {
            string csType = p.IsFunctionPointerType && p.NestedFunctionSignature != null
                ? MapFunctionPointerToCSharp(p)
                : PrimitiveTypeMappings.MapType(p.ParameterType ?? "void", p.TypeReference);

            string pName = SanitizeParameterName(p.Name);
            csParams.Add($"{csType} {pName}");
            callArgs.Add(pName);
        }

        sb.AppendLine($"{indent}public {typeName}({string.Join(", ", csParams)}) {{");
        sb.AppendLine($"{indent}    _ConstructorInternal({string.Join(", ", callArgs)});");
        sb.AppendLine($"{indent}}}");
    }

    private void GenerateDispose(TypeModel type, List<FunctionBodyModel> ownBodies,
        Dictionary<string, TypeModel> baseMap, System.Text.StringBuilder sb, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        sb.AppendLine($"{indent}public void Dispose() {{");

        // Call own destructor if exists
        bool hasOwnDestructor = ownBodies.Any(fb => IsDestructor(fb, type.BaseName));
        if (hasOwnDestructor)
        {
            sb.AppendLine($"{indent}    _DestructorInternal();");
        }
        else
        {
            // If no own destructor, call base disposers?
            // User example: "only the base class Render defines a destructor, so we call it."
            // "If ACRender defined one, we would also call it."
            // Implies: If I have distinct destructor, call it. 
            // BUT standard C++ destructor chain means: Derived Destructor -> Base Destructor.
            // If I call _DestructorInternal(), does that natively call base destructor?
            // Usually in C++, the destructor function DOES call the base destructor.
            // So if we have _DestructorInternal, we call it and STOP (it handles recursion).
            // If we DO NOT have _DestructorInternal, we must manually call Base.Dispose().
            // This matches the user example where ACRender (no dtor) calls Base.Dispose().

            foreach (var basePair in baseMap)
            {
                // Check if base has dispose
                if (HasDestructorInHierarchy(basePair.Value))
                {
                    string key = basePair.Key;
                    // Sanitize key (which is FQN) for field name
                    string cleanedKey = PrimitiveTypeMappings.CleanTypeName(key);
                    string fieldName = $"BaseClass_{cleanedKey.Replace("::", ".").Replace(".", "_")}";
                    sb.AppendLine($"{indent}    {fieldName}.Dispose();");
                }
            }
        }

        sb.AppendLine($"{indent}}}");
    }

    private void GenerateEnum(TypeModel type, System.Text.StringBuilder sb, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        string memberIndent = new string(' ', (indentLevel + 1) * 4);

        // Get enum members
        var members = _repository?.GetEnumMembers(type.Id) ?? new List<EnumMemberModel>();

        // Detect underlying type
        string underlyingType = PrimitiveTypeMappings.GetEnumUnderlyingType(members);
        string safeBaseName = PrimitiveTypeMappings.CleanTypeName(type.BaseName);
        AppendXmlDocComment(sb, type.XmlDocComment, indent);
        sb.AppendLine($"{indent}// {type.FullyQualifiedName}");
        sb.AppendLine($"{indent}public enum {safeBaseName} : {underlyingType}");
        sb.AppendLine($"{indent}{{");

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            string comma = i < members.Count - 1 ? "," : "";

            if (!string.IsNullOrEmpty(member.Value))
            {
                sb.AppendLine($"{memberIndent}{member.Name} = {member.Value}{comma}");
            }
            else
            {
                sb.AppendLine($"{memberIndent}{member.Name}{comma}");
            }
        }

        sb.AppendLine($"{indent}}}");
    }

    private void GenerateStaticMember(StaticVariableModel sv, System.Text.StringBuilder sb, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);

        string csType = PrimitiveTypeMappings.MapTypeForStaticPointer(sv.TypeString, sv.TypeReference);
        string address = NormalizeAddress(sv.Address);

        string svName = PrimitiveTypeMappings.SanitizeIdentifier(sv.Name);
        sb.AppendLine($"{indent}public static {csType} {svName} = ({csType}){address};");
    }

    private void GenerateMember(StructMemberModel member, System.Text.StringBuilder sb, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);

        string csType;
        string comment = "";

        // Handle vtable pointers specially
        if (PrimitiveTypeMappings.IsVTablePointer(member.Name, member.TypeString))
        {
            // Ideally vtable pointers should also use delegates if we knew the signature, 
            // but usually they are void** or similar in the struct definition (e.g. ICIDM_vtbl* __vftable).
            // The actual vtable struct (ICIDM_vtbl) has the function pointers as members.
            csType = "System.IntPtr";
            comment = " // vtable pointer";
        }
        else if (member.IsFunctionPointer)
        {
            // Defensive load: If FunctionSignature is missing but ID is present, try to load it
            if (member.FunctionSignature == null && member.FunctionSignatureId.HasValue && _repository != null)
            {
                member.FunctionSignature = _repository.GetFunctionSignatureById(member.FunctionSignatureId.Value);
            }

            if (member.FunctionSignature != null)
            {
                csType = MapFunctionSignatureToCSharp(member.FunctionSignature);
                comment = " // function pointer";
            }
            else
            {
                // Function pointer member - use void* for simplicity
                csType = "System.IntPtr";
                comment = " // function pointer";
            }
        }
        else
        {
            csType = PrimitiveTypeMappings.MapType(member.TypeString ?? "void", member.TypeReference);

            {
                var tr = member.TypeReference;
                if (tr?.IsArray == true && tr.ArraySize.HasValue)
                {
                    // C# fixed arrays: public fixed byte data[100];
                    string baseType = csType.TrimEnd('*');
                    if (IsPrimitiveForFixed(baseType))
                    {
                        string memberName = PrimitiveTypeMappings.SanitizeIdentifier(member.Name);
                        sb.AppendLine(
                            $"{indent}public fixed {baseType} {memberName}[{tr.ArraySize.Value}];");
                        return;
                    }

                    // For non-primitive arrays, use fixed backing buffer + helper property
                    string propertyName = PrimitiveTypeMappings.SanitizeIdentifier(member.Name);
                    string rawFieldName = $"{propertyName}_Raw";
                    int size = tr.ArraySize.Value;

                    if (tr.IsPointer)
                    {
                        // Array of pointers: public fixed byte name_Raw[Size * 4];
                        sb.AppendLine($"{indent}public fixed byte {rawFieldName}[{size} * 4];");
                        // Helper: public T** name => (T**)System.Runtime.CompilerServices.Unsafe.AsPointer(ref name_Raw[0]);
                        // MapType(T*) returns T*
                        string pointerType = PrimitiveTypeMappings.MapType(member.TypeString ?? "void", tr);
                        sb.AppendLine(
                            $"{indent}public {pointerType}* {propertyName} => ({pointerType}*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref {rawFieldName}[0]);");
                    }
                    else
                    {
                        // Array of structs: public fixed byte name_Raw[Size * HardcodedSize];
                        string baseTypeFqn = PrimitiveTypeMappings.MapType(member.TypeString ?? "void", tr);

                        // Try to get hardcoded size using OffsetCalculationService
                        int elementSize = 0;
                        if (_offsetService != null && tr.ReferencedTypeId.HasValue)
                        {
                            // If we have a resolved type ID, use it directly
                            // We need to fetch the type model first as CalculateTypeSize requires it
                            var referencedType = _repository?.GetTypeById(tr.ReferencedTypeId.Value);
                            if (referencedType != null)
                            {
                                elementSize = _offsetService.CalculateTypeSize(referencedType);
                            }
                        }

                        if (elementSize > 0)
                        {
                            // Use hardcoded size: public fixed byte name_Raw[Size * elementSize];
                            sb.AppendLine($"{indent}public fixed byte {rawFieldName}[{size * elementSize}];");
                        }
                        else
                        {
                            // Fallback to sizeof: public fixed byte name_Raw[Size * sizeof(T)];
                            sb.AppendLine($"{indent}public fixed byte {rawFieldName}[{size} * sizeof({baseTypeFqn})];");
                        }

                        // Helper: public T* name => (T*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref name_Raw[0]);
                        sb.AppendLine(
                            $"{indent}public {baseTypeFqn}* {propertyName} => ({baseTypeFqn}*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref {rawFieldName}[0]);");
                    }

                    return;
                }
            }
        }

        // Handle bit fields
        if (member.BitFieldWidth.HasValue)
        {
            comment = $" // : {member.BitFieldWidth.Value}";
        }

        string sanitizedName = PrimitiveTypeMappings.SanitizeIdentifier(member.Name);
        sb.AppendLine($"{indent}public {csType} {sanitizedName};{comment}");
    }

    private void GenerateMethod(FunctionBodyModel fb, TypeModel sourceType, TypeModel currentType,
        string currentStructName, System.Text.StringBuilder sb,
        int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);

        var sig = fb.FunctionSignature;
        if (sig == null)
        {
            AppendXmlDocComment(sb, fb.XmlDocComment, indent);
            sb.AppendLine($"{indent}// Unable to generate: {fb.FullyQualifiedName} (no signature)");
            return;
        }

        // Prepare the summary injection
        string offsetStr = fb.Offset.HasValue ? $"{fb.Offset.Value:X8}" : "00000000";
        string signature = sig.FullyQualifiedName ?? fb.FullyQualifiedName;
        string escapedSignature = signature.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        string injection = $"<para>Offset: 0x{offsetStr}<br /><code>{escapedSignature}</code></para>";

        string finalComment = fb.XmlDocComment;
        if (string.IsNullOrWhiteSpace(finalComment))
        {
            finalComment = $"<summary>\n{injection}\n</summary>";
        }
        else
        {
            if (finalComment.Contains("</summary>"))
            {
                finalComment = finalComment.Replace("</summary>", $"\n{injection}\n</summary>");
            }
            else
            {
                finalComment = $"<summary>\n{finalComment}\n\n{injection}\n</summary>";
            }
        }

        AppendXmlDocComment(sb, finalComment, indent);

        // Comment out methods that belong to template types
        if (sourceType.IsGeneric)
        {
            sb.AppendLine($"{indent}// {fb.FullyQualifiedName} (template type method)");
            return;
        }

        string offset = fb.Offset.HasValue ? $"0x{fb.Offset.Value:X8}" : "0x00000000";
        string returnType = PrimitiveTypeMappings.MapType(sig.ReturnType ?? "void", sig.ReturnTypeReference);
        string callingConv = PrimitiveTypeMappings.MapCallingConvention(sig.CallingConvention);

        // Extract method name (without namespace/class prefix)
        string methodName = ExtractMethodName(fb.FullyQualifiedName);

        // Rename Constructors/Destructors for Internal methods
        if (IsConstructor(fb, sourceType.BaseName)) methodName = "_ConstructorInternal";
        else if (IsDestructor(fb, sourceType.BaseName)) methodName = "_DestructorInternal";

        // Flatten generic identifiers in method name (e.g. Method<T> -> Method__T)
        if (methodName.Contains('<'))
        {
            methodName = FlattenGenericMethodName(methodName);
        }

        // Get parameters
        var parameters = sig.Parameters?.OrderBy(p => p.Position).ToList() ?? new List<FunctionParamModel>();

        // Determine if this is a static or instance method
        bool isStatic = !callingConv.Equals("Thiscall", StringComparison.OrdinalIgnoreCase);

        if (isStatic)
        {
            GenerateStaticMethod(methodName, returnType, callingConv, parameters, offset, sb, indent, sourceType,
                currentType);
        }
        else
        {
            GenerateInstanceMethod(methodName, returnType, parameters, offset, sourceType, currentType, sb, indent);
        }
    }

    /// <summary>
    /// Converts a function pointer parameter to C# delegate* syntax.
    /// </summary>
    private string MapFunctionPointerToCSharp(FunctionParamModel param)
    {
        var sig = param.NestedFunctionSignature!;
        string baseDecl = MapFunctionSignatureToCSharp(sig);

        if (param.PointerDepth > 1)
        {
            return baseDecl + new string('*', param.PointerDepth - 1);
        }

        return baseDecl;
    }

    private string MapFunctionSignatureToCSharp(FunctionSignatureModel sig)
    {
        var callingConv = PrimitiveTypeMappings.MapCallingConvention(sig.CallingConvention);

        // Build parameter types list
        var paramTypes = sig.Parameters?
            .OrderBy(p => p.Position)
            .Select(p => p.IsFunctionPointerType && p.NestedFunctionSignature != null
                ? MapFunctionPointerToCSharp(p)
                : PrimitiveTypeMappings.MapType(p.ParameterType ?? "void", p.TypeReference))
            .ToList() ?? new List<string>();

        // Map return type
        var returnType = PrimitiveTypeMappings.MapType(sig.ReturnType ?? "void", sig.ReturnTypeReference);

        // Add return type as last type parameter
        paramTypes.Add(returnType);

        var typeParams = string.Join(", ", paramTypes);

        if (string.IsNullOrEmpty(callingConv))
        {
            return $"delegate* unmanaged<{typeParams}>";
        }
        else
        {
            return $"delegate* unmanaged[{callingConv}]<{typeParams}>";
        }
    }

    private void GenerateStaticMethod(string methodName, string returnType, string callingConv,
        List<FunctionParamModel> parameters, string offset, System.Text.StringBuilder sb, string indent,
        TypeModel sourceType, TypeModel currentType)
    {
        // Build parameter list for method signature
        var csParams = new List<string>();
        var delegateParams = new List<string>();
        var callArgs = new List<string>();

        foreach (var p in parameters)
        {
            string csType = p.IsFunctionPointerType && p.NestedFunctionSignature != null
                ? MapFunctionPointerToCSharp(p)
                : PrimitiveTypeMappings.MapType(p.ParameterType ?? "void", p.TypeReference);
            string paramName = SanitizeParameterName(p.Name);

            csParams.Add($"{csType} {paramName}");
            delegateParams.Add(csType);
            callArgs.Add(paramName);
        }

        string paramsStr = string.Join(", ", csParams);
        string delegateTypesStr = delegateParams.Any()
            ? string.Join(", ", delegateParams) + ", " + returnType
            : returnType;
        string callArgsStr = string.Join(", ", callArgs);

        if (sourceType.Id != currentType.Id)
        {
            string line =
                $"{indent}public static {returnType} {methodName}({paramsStr}) => {GetFullyQualifiedName(sourceType)}.{methodName}({callArgsStr});";
            AppendLineCheckForFiltering(sb, line, methodName);
        }
        else
        {
            string line;
            if (returnType == "void")
            {
                line =
                    $"{indent}public static void {methodName}({paramsStr}) => ((delegate* unmanaged[{callingConv}]<{delegateTypesStr}>){offset})({callArgsStr});";
            }
            else
            {
                line =
                    $"{indent}public static {returnType} {methodName}({paramsStr}) => ((delegate* unmanaged[{callingConv}]<{delegateTypesStr}>){offset})({callArgsStr});";
            }

            AppendLineCheckForFiltering(sb, line, methodName);
        }
    }

    private void GenerateInstanceMethod(string methodName, string returnType, List<FunctionParamModel> parameters,
        string offset, TypeModel sourceType, TypeModel currentType, System.Text.StringBuilder sb, string indent)
    {
        // ... (existing param prep logic)
        var csParams = new List<string>();
        var delegateParams = new List<string>();
        var callArgs = new List<string>();

        // Add ref this as first delegate parameter
        delegateParams.Add($"ref {GetFullyQualifiedName(currentType)}");
        callArgs.Add("ref this"); // Default for own

        // Skip first parameter if it's 'this'
        var paramsToProcess = parameters;
        if (parameters.Any() && IsThisParameter(parameters.First()))
        {
            paramsToProcess = parameters.Skip(1).ToList();
        }

        foreach (var p in paramsToProcess)
        {
            string csType = p.IsFunctionPointerType && p.NestedFunctionSignature != null
                ? MapFunctionPointerToCSharp(p)
                : PrimitiveTypeMappings.MapType(p.ParameterType ?? "void", p.TypeReference);
            string paramName = SanitizeParameterName(p.Name);

            csParams.Add($"{csType} {paramName}");
            delegateParams.Add(csType);
            callArgs.Add(paramName);
        }

        string paramsStr = string.Join(", ", csParams);
        string delegateTypesStr = delegateParams.Any()
            ? string.Join(", ", delegateParams) + ", " + returnType
            : returnType;
        string callArgsStr = string.Join(", ", callArgs);

        if (sourceType.Id != currentType.Id)
        {
            // Pulled up instance method
            // Wrapper calling base field

            // Rebuild call args for the wrapper call (excluding implicit this)
            var wrapperCallArgs = callArgs.Skip(1); // Skip "ref this"
            string wrapperCallArgsStr = string.Join(", ", wrapperCallArgs);

            // Base Class Field Name - Must match convention in GenerateStruct using FQN
            string baseField = $"BaseClass_{sourceType.FullyQualifiedName.Replace("::", ".").Replace(".", "_")}";

            string line =
                $"{indent}public {returnType} {methodName}({paramsStr}) => {baseField}.{methodName}({wrapperCallArgsStr});";
            AppendLineCheckForFiltering(sb, line, methodName);
        }
        else
        {
            string line =
                $"{indent}public {returnType} {methodName}({paramsStr}) => ((delegate* unmanaged[Thiscall]<{delegateTypesStr}>){offset})({callArgsStr});";
            AppendLineCheckForFiltering(sb, line, methodName);
        }
    }

    private void AppendLineCheckForFiltering(System.Text.StringBuilder sb, string line, string methodName)
    {
        if (line.Contains("__userpurge") || methodName.StartsWith("operator"))
        {
            sb.AppendLine("// " + line);
        }
        else
        {
            sb.AppendLine(line);
        }
    }

    private static string ExtractMethodName(string fqn)
    {
        if (string.IsNullOrEmpty(fqn))
            return "Unknown";

        // Handle cases like "Render::Set3DViewInternal" or full signatures
        int lastColon = fqn.LastIndexOf("::");
        if (lastColon >= 0 && lastColon + 2 < fqn.Length)
        {
            string afterColon = fqn.Substring(lastColon + 2);
            // If there's a '(' in the name, take only up to it
            int parenIndex = afterColon.IndexOf('(');
            if (parenIndex > 0)
                return afterColon.Substring(0, parenIndex);
            return afterColon;
        }

        // No namespace, check for '(' 
        int paren = fqn.IndexOf('(');
        if (paren > 0)
            return fqn.Substring(0, paren);

        return fqn;
    }

    private static string SanitizeParameterName(string? name)
    {
        return PrimitiveTypeMappings.SanitizeIdentifier(name);
    }

    private static bool IsThisParameter(FunctionParamModel param)
    {
        if (param.Name?.Equals("this", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        // Check if type contains pointer to parent type (heuristic)
        if (param.ParameterType?.EndsWith("*") == true && param.Position == 0)
        {
            // Likely a this pointer
            return true;
        }

        return false;
    }

    private static bool IsPrimitiveForFixed(string csType)
    {
        // Types that can be used with 'fixed' keyword in C#
        return csType switch
        {
            "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or
                "long" or "ulong" or "float" or "double" or "char" => true,
            _ => false
        };
    }

    private static string NormalizeAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return "0x00000000";

        string clean = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? address.Substring(2)
            : address;

        if (long.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out long val))
        {
            return $"0x{val:X8}";
        }

        return address;
    }

    private static string FlattenGenericMethodName(string name)
    {
        // Simple flattening strategy consistent with PrimitiveTypeMappings.ToIdentifier
        // Method<T> -> Method__T

        // Remove spaces
        name = name.Replace(" ", "");

        // Replace brackets and separators
        return name
            .Replace("::", "_")
            .Replace("<", "__")
            .Replace(">", "")
            .Replace(",", "__");
    }

    private void AppendXmlDocComment(System.Text.StringBuilder sb, string? comment, string indent)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return;

        sb.AppendLine();
        var lines = comment.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            sb.AppendLine($"{indent}/// {line}");
        }
    }
}
