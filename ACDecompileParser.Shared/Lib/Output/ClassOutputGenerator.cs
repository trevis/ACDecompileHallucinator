using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output;

/// <summary>
/// Generates C++ class definitions with nested types, preserving memory layout.
/// Outputs vtables first, then enums, then nested structs, then class members.
/// </summary>
public class ClassOutputGenerator : TypeOutputGeneratorBase
{
    private readonly StructOutputGenerator _structGenerator;

    private readonly MemberTokenGenerator _memberGenerator;

    public ClassOutputGenerator(ITypeRepository? repository = null,
        ITypeTokenizationService? tokenizationService = null) : base(repository, tokenizationService)
    {
        _structGenerator = new StructOutputGenerator(repository, tokenizationService);
        _memberGenerator = new MemberTokenGenerator(TokenizationService);
    }

    /// <summary>
    /// Override to propagate cache to nested generators for efficient type resolution.
    /// </summary>
    public new void SetLookupCache(TypeLookupCache cache)
    {
        base.SetLookupCache(cache);
        _structGenerator.SetLookupCache(cache);
        _memberGenerator.SetLookupCache(cache);
    }

    public override IEnumerable<CodeToken> Generate(TypeModel type)
    {
        // If this type has a parent, it should be rendered by its parent's generator
        if (type.ParentType != null)
        {
            yield break;
        }

        // If no nested types AND no function bodies, delegate to struct generator
        // We want to use the full class generator if there are function bodies to render
        bool hasNestedTypes = type.NestedTypes != null && type.NestedTypes.Any();
        bool hasFunctionBodies = type.FunctionBodies != null && type.FunctionBodies.Any();
        bool hasStaticVariables = type.StaticVariables != null && type.StaticVariables.Any();

        if (!hasNestedTypes && !hasFunctionBodies && !hasStaticVariables)
        {
            foreach (var token in _structGenerator.Generate(type))
            {
                yield return token;
            }

            yield break;
        }

        // Generate full class with nested types (and/or function bodies)
        foreach (var token in GenerateTypeDefinition(type, ""))
        {
            yield return token;
        }
    }

    private IEnumerable<CodeToken> GenerateTypeDefinition(TypeModel type, string indent)
    {
        string memberIndent = indent + "    ";

        // Determine the struct keyword
        string structKeyword = GetStructKeyword(type);

        // Comment header (only for top level)
        if (string.IsNullOrEmpty(indent))
        {
            yield return new CodeToken("// Reconstructed from database", TokenType.Comment);
            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        }

        // Handle template parameters
        if (type.TemplateArguments?.Count > 0)
        {
            yield return new CodeToken(indent, TokenType.Whitespace);
            yield return new CodeToken("template", TokenType.Keyword);
            yield return new CodeToken("<", TokenType.Punctuation);

            var sortedArgs = type.TemplateArguments.OrderBy(ta => ta.Position).ToList();
            for (int i = 0; i < sortedArgs.Count; i++)
            {
                var templateArg = sortedArgs[i];
                foreach (var token in TokenizeTypeString(templateArg.TypeString,
                             templateArg.TypeReference?.ReferencedTypeId, type.Namespace))
                {
                    yield return token;
                }

                if (i < sortedArgs.Count - 1)
                {
                    yield return new CodeToken(", ", TokenType.Punctuation);
                }
            }

            yield return new CodeToken(">", TokenType.Punctuation);
            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        }

        // Class declaration with base types
        var baseTypes = type.BaseTypes?.Any() == true
            ? type.BaseTypes
            : (Repository?.GetBaseTypesWithRelatedTypes(type.Id) ?? new List<TypeInheritance>());

        yield return new CodeToken(indent, TokenType.Whitespace);
        yield return new CodeToken(structKeyword, TokenType.Keyword);
        yield return new CodeToken(" ", TokenType.Whitespace);

        string displayNamespace = type.Namespace;
        // For nested types, the namespace usually contains the parent chain.
        // We only want to output the name, not the full namespace prefix if we are already nested.
        // However, the original code logic used `type.Namespace + "::"` for top level.
        // If we represent a nested type, we generally just want the BaseName.

        if (string.IsNullOrEmpty(indent) && !string.IsNullOrEmpty(displayNamespace))
        {
            yield return new CodeToken(displayNamespace + "::", TokenType.Identifier);
        }

        yield return new CodeToken(type.BaseName ?? string.Empty, TokenType.TypeName, type.Id.ToString());

        if (baseTypes.Any())
        {
            bool first = true;
            foreach (var baseType in baseTypes)
            {
                string baseTypeName = baseType.RelatedType?.BaseName ?? baseType.RelatedTypeString ?? "";
                if (!string.IsNullOrEmpty(baseTypeName))
                {
                    if (first)
                    {
                        yield return new CodeToken(" : ", TokenType.Punctuation);
                        first = false;
                    }
                    else
                    {
                        yield return new CodeToken(", ", TokenType.Punctuation);
                    }

                    yield return new CodeToken("public ", TokenType.Keyword);
                    foreach (var token in TokenizeTypeString(baseTypeName, baseType.RelatedTypeId, type.Namespace))
                    {
                        yield return token;
                    }
                }
            }
        }

        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        yield return new CodeToken(indent, TokenType.Whitespace);
        yield return new CodeToken("{", TokenType.Punctuation);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // C++ access specifier - assume public for structs
        // But for classes we might want public: explicitly if we started as class

        // The original code output "public:" unconditionally for the body
        // yield return new CodeToken("public:", TokenType.Keyword); 
        // But logic suggests we just follow standard struct rules or simple public section
        // Let's keep it consistent with previous one if it was doing it.
        // Previous output:
        // {
        // public:

        yield return new CodeToken(indent, TokenType.Whitespace);
        yield return new CodeToken("public:", TokenType.Keyword);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // Forward declarations for nested types (skip enums)
        foreach (var nested in (type.NestedTypes ?? new List<TypeModel>()).Where(nt => nt.Type != TypeType.Enum))
        {
            yield return new CodeToken(memberIndent, TokenType.Whitespace);
            string nestedKeyword = GetStructKeyword(nested);
            yield return new CodeToken(nestedKeyword, TokenType.Keyword);
            yield return new CodeToken(" ", TokenType.Whitespace);
            yield return new CodeToken(nested.BaseName, TokenType.TypeName, nested.Id.ToString());
            yield return new CodeToken(";", TokenType.Punctuation);
            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        }


        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // Output static members
        if (type.StaticVariables != null && type.StaticVariables.Any())
        {
            yield return new CodeToken(memberIndent, TokenType.Whitespace);
            yield return new CodeToken($"// ── Static members ──", TokenType.Comment);
            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

            foreach (var staticVar in type.StaticVariables)
            {
                yield return new CodeToken(memberIndent, TokenType.Whitespace);
                yield return new CodeToken("static ", TokenType.Keyword);

                foreach (var token in TokenizeTypeString(staticVar.TypeString, staticVar.TypeReferenceId,
                             type.Namespace))
                {
                    yield return token;
                }

                yield return new CodeToken(" ", TokenType.Whitespace);

                // Clean up name by removing parent prefix
                string name = staticVar.Name;
                if (name.Contains("::"))
                {
                    int lastColon = name.LastIndexOf("::");
                    name = name.Substring(lastColon + 2);
                }

                yield return new CodeToken(name, TokenType.Identifier);

                if (!string.IsNullOrEmpty(staticVar.Value))
                {
                    yield return new CodeToken(" = ", TokenType.Punctuation);
                    yield return
                        new CodeToken(staticVar.Value,
                            TokenType.StringLiteral); // Using StringLiteral for generic value coloring
                }

                yield return new CodeToken(";", TokenType.Punctuation);

                // Address comment
                if (!string.IsNullOrEmpty(staticVar.Address))
                {
                    string address = staticVar.Address;
                    if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        address = address.Substring(2);
                    }

                    if (long.TryParse(address, System.Globalization.NumberStyles.HexNumber, null, out long addrValue))
                    {
                        yield return new CodeToken($" // 0x{addrValue:X8}", TokenType.Comment);
                    }
                    else
                    {
                        yield return new CodeToken($" // {staticVar.Address}", TokenType.Comment);
                    }
                }

                yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
            }

            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        }


        // Pre-load enum members for all nested enums in a single batch query
        Dictionary<int, List<EnumMemberModel>>? enumMembersCache = null;
        var nestedEnumIds = (type.NestedTypes ?? new List<TypeModel>())
            .Where(nt => nt.Type == TypeType.Enum)
            .Select(nt => nt.Id)
            .ToList();
        if (nestedEnumIds.Any() && Repository != null)
        {
            enumMembersCache = Repository.GetEnumMembersForMultipleTypes(nestedEnumIds);
        }

        // Output nested types (vtables first, already sorted by LinkNestedTypes)
        foreach (var nested in (type.NestedTypes ?? new List<TypeModel>()))
        {
            if (nested.Type == TypeType.Enum)
            {
                // Output enum (using pre-loaded cache)
                foreach (var token in GenerateNestedEnum(nested, memberIndent, enumMembersCache))
                {
                    yield return token;
                }
            }
            else
            {
                // Recursive call for nested struct/class
                foreach (var token in GenerateTypeDefinition(nested, memberIndent))
                {
                    yield return token;
                }
            }

            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        }

        // Separator comment before members
        yield return new CodeToken(memberIndent, TokenType.Whitespace);
        yield return new CodeToken($"// ── {type.BaseName} class members ──", TokenType.Comment);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // Output class members
        var members = type.StructMembers?.Any() == true
            ? type.StructMembers
            : (Repository?.GetStructMembersWithRelatedTypes(type.Id) ?? new List<StructMemberModel>());

        foreach (var member in members.OrderBy(m => m.DeclarationOrder))
        {
            foreach (var token in GenerateMemberWithOffset(member, type.Namespace, memberIndent))
            {
                yield return token;
            }
        }

        // Output method signatures from function bodies
        var functionBodies = type.FunctionBodies ?? new List<FunctionBodyModel>();
        if (functionBodies.Any())
        {
            // Collect virtual method names from nested vtables
            var vtableMethodNames = GetVTableMethodOffsets(type);

            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
            yield return new CodeToken(memberIndent, TokenType.Whitespace);
            yield return new CodeToken($"// ── Method signatures ──", TokenType.Comment);
            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

            foreach (var body in functionBodies.OrderBy(b => b.FunctionSignature?.Name ?? b.FullyQualifiedName))
            {
                yield return new CodeToken(memberIndent, TokenType.Whitespace);

                // Extract method name from signature name (if available) or FQN for virtual check
                string methodName = body.FunctionSignature?.Name ?? body.FullyQualifiedName;
                int lastColonIndex = methodName.LastIndexOf("::");
                if (lastColonIndex >= 0)
                {
                    methodName = methodName.Substring(lastColonIndex + 2);
                }

                // If we still have parentheses (shouldn't happen with FunctionSignature.Name), strip them
                int parenIndex = methodName.IndexOf('(');
                if (parenIndex >= 0)
                {
                    methodName = methodName.Substring(0, parenIndex);
                }

                // Check if this method is virtual (exists in vtable)
                bool isVirtual = vtableMethodNames.ContainsKey(methodName);
                if (isVirtual)
                {
                    yield return new CodeToken("virtual ", TokenType.Keyword);
                }

                // Output the signature
                foreach (var token in _memberGenerator.GenerateSignatureTokens(body.FunctionSignature,
                             body.FullyQualifiedName,
                             type.Namespace))
                {
                    yield return token;
                }

                yield return new CodeToken(";", TokenType.Punctuation);

                // Add function address offset comment if available
                if (body.Offset.HasValue)
                {
                    yield return new CodeToken($" // 0x{body.Offset.Value:X8}", TokenType.Comment);
                }

                yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
            }
        }

        yield return new CodeToken(indent, TokenType.Whitespace);
        yield return new CodeToken("};", TokenType.Punctuation);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
    }

    private IEnumerable<CodeToken> GenerateNestedEnum(TypeModel nested, string indent,
        Dictionary<int, List<EnumMemberModel>>? enumMembersCache = null)
    {
        string memberIndent = indent + "    ";

        yield return new CodeToken(indent, TokenType.Whitespace);
        yield return new CodeToken("enum", TokenType.Keyword);
        yield return new CodeToken(" ", TokenType.Whitespace);
        yield return new CodeToken(nested.BaseName, TokenType.TypeName, nested.Id.ToString());
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        yield return new CodeToken(indent, TokenType.Whitespace);
        yield return new CodeToken("{", TokenType.Punctuation);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // Output enum members (use cache if available, otherwise fall back to query)
        List<EnumMemberModel> enumMembers;
        if (enumMembersCache != null && enumMembersCache.TryGetValue(nested.Id, out var cached))
        {
            enumMembers = cached;
        }
        else
        {
            enumMembers = Repository?.GetEnumMembers(nested.Id) ?? new List<EnumMemberModel>();
        }

        foreach (var member in enumMembers)
        {
            yield return new CodeToken(memberIndent, TokenType.Whitespace);
            yield return new CodeToken(member.Name, TokenType.Identifier);
            if (!string.IsNullOrEmpty(member.Value))
            {
                yield return new CodeToken(" = ", TokenType.Punctuation);
                yield return new CodeToken(member.Value, TokenType.NumberLiteral);
            }

            yield return new CodeToken(",", TokenType.Punctuation);
            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        }

        yield return new CodeToken(indent, TokenType.Whitespace);
        yield return new CodeToken("};", TokenType.Punctuation);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
    }

    private IEnumerable<CodeToken> GenerateMemberWithOffset(StructMemberModel member, string? contextNamespace,
        string indent)
    {
        yield return new CodeToken(indent, TokenType.Whitespace);

        foreach (var token in _memberGenerator.GenerateMemberTokens(member, contextNamespace))
        {
            yield return token;
        }

        // Add offset comment
        if (member.Offset.HasValue)
        {
            yield return new CodeToken($" // 0x{member.Offset.Value:X2}", TokenType.Comment);
        }

        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
    }

    private static string GetStructKeyword(TypeModel type)
    {
        if (type.Type == TypeType.Enum)
            return "enum";
        if (type.Source.Contains("union"))
            return "union";
        if (type.Source.Contains("class"))
            return "class";
        return "struct";
    }

    /// <summary>
    /// Collects all method names from vtable nested types, with their offsets.
    /// </summary>
    private Dictionary<string, int?> GetVTableMethodOffsets(TypeModel type)
    {
        var vtableMethodOffsets = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

        if (type.NestedTypes == null)
            return vtableMethodOffsets;

        foreach (var nested in type.NestedTypes.Where(nt => nt.IsVTable || nt.BaseName.EndsWith("_vtbl")))
        {
            // Get vtable members
            var vtableMembers = nested.StructMembers?.Any() == true
                ? nested.StructMembers
                : (Repository?.GetStructMembersWithRelatedTypes(nested.Id) ?? new List<StructMemberModel>());

            foreach (var member in vtableMembers.Where(m => m.IsFunctionPointer))
            {
                if (!string.IsNullOrEmpty(member.Name) && !vtableMethodOffsets.ContainsKey(member.Name))
                {
                    vtableMethodOffsets[member.Name] = member.Offset;
                }
            }
        }

        return vtableMethodOffsets;
    }
}