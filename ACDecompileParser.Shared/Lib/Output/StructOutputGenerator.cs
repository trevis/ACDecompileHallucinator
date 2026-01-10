using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output;

public class StructOutputGenerator : TypeOutputGeneratorBase
{
    private readonly MemberTokenGenerator _memberGenerator;

    public StructOutputGenerator(ITypeRepository? repository = null,
        ITypeTokenizationService? tokenizationService = null) : base(repository, tokenizationService)
    {
        _memberGenerator = new MemberTokenGenerator(TokenizationService);
    }

    public override IEnumerable<CodeToken> Generate(TypeModel type)
    {
        // Output the reconstructed struct from database information
        foreach (var token in GenerateReconstructedTokens(type))
        {
            yield return token;
        }
    }

    private IEnumerable<CodeToken> GenerateReconstructedTokens(TypeModel type)
    {
        // Determine the struct keyword based on the original definition
        string structKeyword = "struct";
        if (type.Source.Contains("class"))
        {
            structKeyword = "class";
        }
        else if (type.Source.Contains("union"))
        {
            structKeyword = "union";
        }

        // Start the reconstructed struct
        yield return new CodeToken("// Reconstructed from database (WIP)", TokenType.Comment);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // Handle template parameters if this is a templated struct
        if (type.TemplateArguments?.Count > 0)
        {
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

        // Use pre-loaded data if available (from GetTypesForGroup or batch loading)
        // Only query the repository as a fallback to avoid N+1 queries
        var baseTypes = type.BaseTypes?.Any() == true
            ? type.BaseTypes
            : (Repository?.GetBaseTypesWithRelatedTypes(type.Id) ?? new List<TypeInheritance>());

        yield return new CodeToken(structKeyword, TokenType.Keyword);
        yield return new CodeToken(" ", TokenType.Whitespace);

        // Include namespace inline in the struct name (e.g., AC1Modern::AFrame)
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            yield return new CodeToken(type.Namespace + "::", TokenType.Identifier);
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
        yield return new CodeToken("{", TokenType.Punctuation);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // Resolve associated function bodies (try the type itself, then fallback to parent class for vtables)
        var associatedBodies = type.FunctionBodies ?? new List<FunctionBodyModel>();
        if (!associatedBodies.Any() && (type.IsVTable || (type.BaseName != null && type.BaseName.EndsWith("_vtbl"))))
        {
            if (Repository != null && type.BaseName != null)
            {
                // Try to find the parent class
                // Convention: ClassName_vtbl -> ClassName
                var className = type.BaseName.Replace("_vtbl", "");
                // Try to find the type by name in the same namespace
                var parentType = Repository.GetTypesForGroup(className, type.Namespace).FirstOrDefault();

                if (parentType != null)
                {
                    associatedBodies = Repository.GetFunctionBodiesForType(parentType.Id);
                }
            }
        }

        var matchedBodyIds = new HashSet<int>();

        // Use pre-loaded struct members if available, only query as fallback
        var members = type.StructMembers?.Any() == true
            ? type.StructMembers
            : (Repository?.GetStructMembersWithRelatedTypes(type.Id) ?? new List<StructMemberModel>());

        // Order members by DeclarationOrder to maintain the original source declaration order
        foreach (var member in members.OrderBy(m => m.DeclarationOrder))
        {
            yield return new CodeToken("    ", TokenType.Whitespace);

            // Attempt to match with a function body
            FunctionBodyModel? exactMatch = null;
            if (associatedBodies.Any() && member.IsFunctionPointer)
            {
                // Try exact name match (MemberName == BodyName or BodyName ends with ::MemberName)
                // Start with simplest robust check
                exactMatch = associatedBodies.FirstOrDefault(b =>
                {
                    string bodyName = b.FunctionSignature?.Name ?? b.FullyQualifiedName;
                    return bodyName.EndsWith($"::{member.Name}") || bodyName == member.Name;
                });

                if (exactMatch != null)
                {
                    matchedBodyIds.Add(exactMatch.Id);
                    string sigStr =
                        _memberGenerator.GetNormalizedSignatureString(exactMatch.FunctionSignature,
                            exactMatch.FullyQualifiedName);
                    yield return new CodeToken($"// Matched: {sigStr}", TokenType.Comment);
                    yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
                    yield return new CodeToken("    ", TokenType.Whitespace);
                }
                else
                {
                    yield return new CodeToken($"// Unmatched Method: {member.Name}", TokenType.Comment);
                    yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
                    yield return new CodeToken("    ", TokenType.Whitespace);
                }
            }

            foreach (var token in _memberGenerator.GenerateMemberTokens(member, type.Namespace, exactMatch))
            {
                yield return token;
            }

            if (member.Offset.HasValue)
            {
                yield return new CodeToken($" // 0x{member.Offset.Value:X2}", TokenType.Comment);
            }

            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        }

        // Static Variables
        var staticVariables = type.StaticVariables?.Any() == true
            ? type.StaticVariables
            : (Repository?.GetStaticVariablesForType(type.Id) ?? new List<StaticVariableModel>());

        if (staticVariables.Any())
        {
            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
            yield return new CodeToken("    // Static Variables", TokenType.Comment);
            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

            foreach (var sv in staticVariables)
            {
                yield return new CodeToken("    ", TokenType.Whitespace);
                yield return new CodeToken("static", TokenType.Keyword);
                yield return new CodeToken(" ", TokenType.Whitespace);

                foreach (var token in TokenizeTypeString(sv.TypeString, sv.TypeReferenceId, type.Namespace))
                {
                    yield return token;
                }

                yield return new CodeToken(" ", TokenType.Whitespace);
                yield return new CodeToken(sv.Name, TokenType.Identifier);
                yield return new CodeToken(";", TokenType.Punctuation);

                string address = sv.Address;
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
                    yield return new CodeToken($" // {sv.Address}", TokenType.Comment);
                }

                yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
            }
        }

        yield return new CodeToken("};", TokenType.Punctuation);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // Report unmatched bodies if we had any bodies to check against, BUT NOT for vtables (too noisy)
        var isVTableLookup = (type.IsVTable || (type.BaseName != null && type.BaseName.EndsWith("_vtbl")));
        if (associatedBodies.Any() && !isVTableLookup)
        {
            var unmatchedBodies = associatedBodies.Where(b => !matchedBodyIds.Contains(b.Id)).ToList();
            if (unmatchedBodies.Any())
            {
                yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
                yield return new CodeToken("// Unmatched Function Bodies:", TokenType.Comment);
                yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

                foreach (var body in unmatchedBodies)
                {
                    string sigStr =
                        _memberGenerator.GetNormalizedSignatureString(body.FunctionSignature, body.FullyQualifiedName);
                    yield return new CodeToken($"// {sigStr}", TokenType.Comment);
                    yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
                }
            }
        }
    }
}
