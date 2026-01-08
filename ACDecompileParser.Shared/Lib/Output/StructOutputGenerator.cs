using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Output.Models;

namespace ACDecompileParser.Shared.Lib.Output;

public class StructOutputGenerator : TypeOutputGeneratorBase
{
    public StructOutputGenerator(ITypeRepository? repository = null) : base(repository)
    {
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
                    b.FullyQualifiedName.EndsWith($"::{member.Name}") ||
                    b.FullyQualifiedName == member.Name);

                if (exactMatch != null)
                {
                    matchedBodyIds.Add(exactMatch.Id);
                    string sigStr =
                        GetNormalizedSignatureString(exactMatch.FunctionSignature, exactMatch.FullyQualifiedName);
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

            foreach (var token in ReconstructMemberTokens(member, type.Namespace, exactMatch))
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
                    string sigStr = GetNormalizedSignatureString(body.FunctionSignature, body.FullyQualifiedName);
                    yield return new CodeToken($"// {sigStr}", TokenType.Comment);
                    yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
                }
            }
        }
    }

    private IEnumerable<CodeToken> ReconstructMemberTokens(StructMemberModel member, string? contextNamespace = null,
        FunctionBodyModel? matchedFunction = null)
    {
        // Add alignment if present
        if (member.Alignment.HasValue)
        {
            yield return new CodeToken("__declspec", TokenType.Keyword);
            yield return new CodeToken("(", TokenType.Punctuation);
            yield return new CodeToken("align", TokenType.Keyword);
            yield return new CodeToken("(", TokenType.Punctuation);
            yield return new CodeToken(member.Alignment.Value.ToString(), TokenType.NumberLiteral);
            yield return new CodeToken(")", TokenType.Punctuation);
            yield return new CodeToken(")", TokenType.Punctuation);
            yield return new CodeToken(" ", TokenType.Whitespace);
        }

        // Handle function pointers / vtable delegates
        if (matchedFunction != null && member.IsFunctionPointer && matchedFunction.FunctionSignature != null)
        {
            // Use delegate* unmanaged approach
            yield return new CodeToken("public ", TokenType.Keyword); // Explicitly public for vtables
            yield return
                new CodeToken("static ",
                    TokenType.Keyword); // VTable entries are just pointers, making them static delegates isn't quite right for C++ vtables, but for C# interop bindings, we often emit them as fields.
            // Wait, the user request shows: public static delegate* unmanaged...
            // So they want static delegates?
            // "public static delegate* unmanaged[Thiscall]<Render*, UInt32, void*> _DestructorInternal;"
            // Yes, user request explicitly shows static delegate* unmanaged.

            yield return new CodeToken("delegate", TokenType.Keyword);
            yield return new CodeToken("* ", TokenType.Punctuation);
            yield return new CodeToken("unmanaged", TokenType.Keyword);

            var sig = matchedFunction.FunctionSignature;
            string callingConvention =
                sig.CallingConvention?.Replace("__", "") ??
                "Thiscall"; // Default to Thiscall if missing, strip underscores
            // Capitalize first letter? The user example has "Thiscall".
            if (callingConvention.Equals("thiscall", StringComparison.OrdinalIgnoreCase))
                callingConvention = "Thiscall";
            else if (callingConvention.Equals("cdecl", StringComparison.OrdinalIgnoreCase)) callingConvention = "Cdecl";
            else if (callingConvention.Equals("stdcall", StringComparison.OrdinalIgnoreCase))
                callingConvention = "Stdcall";
            else if (callingConvention.Equals("fastcall", StringComparison.OrdinalIgnoreCase))
                callingConvention = "Fastcall";

            yield return
                new CodeToken($"[{callingConvention}]",
                    TokenType.TypeName); // Attribute-like syntax for unmanaged delegates
            yield return new CodeToken("<", TokenType.Punctuation);

            // Reconstruct parameters
            // Check if 'this' pointer is needed and missing
            // For Thiscall, the first argument should be the 'this' pointer.
            // Sometimes the parsed signature already has it, sometimes not.
            // If callingConvention is Thiscall, we expect the first arg to be the parent type pointer.

            var parameters = sig.Parameters?.OrderBy(p => p.Position).ToList() ?? new List<FunctionParamModel>();

            foreach (var param in parameters)
            {
                foreach (var token in TokenizeTypeString(param.ParameterType ?? "void*", null, contextNamespace))
                {
                    yield return token;
                }

                yield return new CodeToken(", ", TokenType.Punctuation);
            }

            // Return type is the last type argument
            string returnType = sig.ReturnType ?? "void";
            foreach (var token in TokenizeTypeString(returnType, null, contextNamespace))
            {
                yield return token;
            }

            yield return new CodeToken(">", TokenType.Punctuation);
            yield return new CodeToken(" ", TokenType.Whitespace);

            // Name
            string name = member.Name;
            if (name.StartsWith("~") || name.Contains("_dtor_"))
            {
                name = "_DestructorInternal";
            }

            yield return new CodeToken(name, TokenType.Identifier);

            yield return new CodeToken(";", TokenType.Punctuation);
            yield break;
        }

        // Handle generic function pointers (no match found)
        if (member.IsFunctionPointer && member.FunctionSignature != null)
        {
            string returnType = member.FunctionSignature.ReturnType ?? "void";
            string callingConvention = !string.IsNullOrEmpty(member.FunctionSignature.CallingConvention)
                ? member.FunctionSignature.CallingConvention + " "
                : "";

            foreach (var token in TokenizeTypeString(returnType, null, contextNamespace))
            {
                yield return token;
            }

            yield return new CodeToken(" ", TokenType.Whitespace);
            yield return new CodeToken("(", TokenType.Punctuation);
            if (!string.IsNullOrEmpty(callingConvention))
            {
                yield return new CodeToken(callingConvention.Trim(), TokenType.Keyword);
                yield return new CodeToken(" ", TokenType.Whitespace);
            }

            yield return new CodeToken("*", TokenType.Punctuation);
            yield return new CodeToken(member.Name, TokenType.Identifier);
            yield return new CodeToken(")", TokenType.Punctuation);
            yield return new CodeToken("(", TokenType.Punctuation);

            if (member.FunctionSignature.Parameters != null)
            {
                var paramList = member.FunctionSignature.Parameters.ToList();
                for (int i = 0; i < paramList.Count; i++)
                {
                    var param = paramList[i];
                    foreach (var token in TokenizeTypeString(param.ParameterType ?? string.Empty, null,
                                 contextNamespace))
                    {
                        yield return token;
                    }

                    yield return new CodeToken(" ", TokenType.Whitespace);
                    yield return new CodeToken(param.Name ?? string.Empty, TokenType.Identifier);

                    if (i < paramList.Count - 1)
                    {
                        yield return new CodeToken(", ", TokenType.Punctuation);
                    }
                }
            }

            yield return new CodeToken(")", TokenType.Punctuation);
            yield return new CodeToken(";", TokenType.Punctuation);
            yield break;
        }

        // Add type string
        foreach (var token in TokenizeTypeString(member.TypeString ?? string.Empty,
                     member.TypeReference?.ReferencedTypeId, contextNamespace))
        {
            yield return token;
        }

        yield return new CodeToken(" ", TokenType.Whitespace);

        // Add member name
        yield return new CodeToken(member.Name ?? string.Empty, TokenType.Identifier);

        // Handle array declarations
        if (member.TypeReference?.IsArray == true)
        {
            yield return new CodeToken("[", TokenType.Punctuation);
            if (member.TypeReference.ArraySize.HasValue)
            {
                yield return new CodeToken(member.TypeReference.ArraySize.Value.ToString(), TokenType.NumberLiteral);
            }

            yield return new CodeToken("]", TokenType.Punctuation);
        }

        // Handle bit fields
        if (member.BitFieldWidth.HasValue)
        {
            yield return new CodeToken(" : ", TokenType.Punctuation);
            yield return new CodeToken(member.BitFieldWidth.Value.ToString(), TokenType.NumberLiteral);
        }

        // Close with semicolon
        yield return new CodeToken(";", TokenType.Punctuation);
    }

    private string GetNormalizedSignatureString(FunctionSignatureModel? sig, string fallbackName)
    {
        if (sig == null)
            return fallbackName;

        string returnType = sig.ReturnType ?? "void";
        string callingConv = !string.IsNullOrEmpty(sig.CallingConvention) ? sig.CallingConvention + " " : "";
        string name = sig.Name; // Or extract from fallbackName if needed, but sig.Name should be good

        // If Name is missing in sig, try to use fallbackName (handle :: if needed)
        if (string.IsNullOrEmpty(name))
            name = fallbackName;

        var sb = new System.Text.StringBuilder();
        sb.Append(returnType);
        sb.Append(" ");
        sb.Append(callingConv);
        sb.Append(name);
        sb.Append("(");

        if (sig.Parameters != null && sig.Parameters.Any())
        {
            var pList = sig.Parameters.OrderBy(p => p.Position).ToList();
            for (int i = 0; i < pList.Count; i++)
            {
                var p = pList[i];
                sb.Append(p.ParameterType);
                if (!string.IsNullOrEmpty(p.Name))
                {
                    sb.Append(" ");
                    sb.Append(p.Name);
                }

                if (i < pList.Count - 1)
                    sb.Append(", ");
            }
        }

        sb.Append(")");
        return sb.ToString();
    }
}
