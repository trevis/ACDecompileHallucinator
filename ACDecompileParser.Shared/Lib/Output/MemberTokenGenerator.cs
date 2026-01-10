using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;
using System.Text;

namespace ACDecompileParser.Shared.Lib.Output;

public class MemberTokenGenerator
{
    private readonly ITypeTokenizationService _tokenizationService;

    public MemberTokenGenerator(ITypeTokenizationService tokenizationService)
    {
        _tokenizationService = tokenizationService;
    }

    /// <summary>
    /// Sets the lookup cache for efficient type resolution without database queries.
    /// </summary>
    public void SetLookupCache(TypeLookupCache cache)
    {
        _tokenizationService.SetLookupCache(cache);
    }

    public IEnumerable<CodeToken> GenerateMemberTokens(StructMemberModel member, string? contextNamespace = null,
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
            foreach (var token in GenerateVTableDelegate(matchedFunction, member, contextNamespace))
            {
                yield return token;
            }

            yield break;
        }

        // Handle generic function pointers (no match found)
        if (member.IsFunctionPointer && member.FunctionSignature != null)
        {
            foreach (var token in GenerateFunctionPointer(member, contextNamespace))
            {
                yield return token;
            }

            yield break;
        }

        // Add type string
        foreach (var token in _tokenizationService.TokenizeTypeString(member.TypeString ?? string.Empty,
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

    private IEnumerable<CodeToken> GenerateVTableDelegate(FunctionBodyModel matchedFunction, StructMemberModel member,
        string? contextNamespace)
    {
        // Use delegate* unmanaged approach
        yield return new CodeToken("public ", TokenType.Keyword); // Explicitly public for vtables
        yield return new CodeToken("static ", TokenType.Keyword);

        yield return new CodeToken("delegate", TokenType.Keyword);
        yield return new CodeToken("* ", TokenType.Punctuation);
        yield return new CodeToken("unmanaged", TokenType.Keyword);

        var sig = matchedFunction.FunctionSignature!;
        string callingConvention = sig.CallingConvention?.Replace("__", "") ?? "Thiscall";

        if (callingConvention.Equals("thiscall", StringComparison.OrdinalIgnoreCase))
            callingConvention = "Thiscall";
        else if (callingConvention.Equals("cdecl", StringComparison.OrdinalIgnoreCase))
            callingConvention = "Cdecl";
        else if (callingConvention.Equals("stdcall", StringComparison.OrdinalIgnoreCase))
            callingConvention = "Stdcall";
        else if (callingConvention.Equals("fastcall", StringComparison.OrdinalIgnoreCase))
            callingConvention = "Fastcall";

        yield return
            new CodeToken($"[{callingConvention}]",
                TokenType.TypeName); // Attribute-like syntax for unmanaged delegates
        yield return new CodeToken("<", TokenType.Punctuation);

        // Reconstruct parameters
        var parameters = sig.Parameters?.OrderBy(p => p.Position).ToList() ?? new List<FunctionParamModel>();

        foreach (var param in parameters)
        {
            foreach (var token in _tokenizationService.TokenizeTypeString(param.ParameterType ?? "void*", null,
                         contextNamespace))
            {
                yield return token;
            }

            yield return new CodeToken(", ", TokenType.Punctuation);
        }

        // Return type is the last type argument
        string returnType = sig.ReturnType ?? "void";
        foreach (var token in _tokenizationService.TokenizeTypeString(returnType, null, contextNamespace))
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
    }

    private IEnumerable<CodeToken> GenerateFunctionPointer(StructMemberModel member, string? contextNamespace)
    {
        var sig = member.FunctionSignature!;
        string returnType = sig.ReturnType ?? "void";
        string callingConvention = !string.IsNullOrEmpty(sig.CallingConvention)
            ? sig.CallingConvention + " "
            : "";

        foreach (var token in _tokenizationService.TokenizeTypeString(returnType, null, contextNamespace))
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
                foreach (var token in _tokenizationService.TokenizeTypeString(param.ParameterType ?? string.Empty, null,
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
    }

    public string GetNormalizedSignatureString(FunctionSignatureModel? sig, string fallbackName)
    {
        if (sig == null)
            return fallbackName;

        string returnType = sig.ReturnType ?? "void";
        string callingConv = !string.IsNullOrEmpty(sig.CallingConvention) ? sig.CallingConvention + " " : "";
        string name = sig.Name;

        // If Name is missing in sig, try to use fallbackName (handle :: if needed)
        if (string.IsNullOrEmpty(name))
            name = fallbackName;

        var sb = new StringBuilder();
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

    public IEnumerable<CodeToken> GenerateSignatureTokens(FunctionSignatureModel? sig, string fallbackName,
        string? contextNamespace)
    {
        if (sig == null)
        {
            yield return new CodeToken(fallbackName, TokenType.Identifier);
            yield break;
        }

        string returnType = sig.ReturnType ?? "void";

        foreach (var token in _tokenizationService.TokenizeTypeString(returnType,
                     sig.ReturnTypeReference?.ReferencedTypeId, contextNamespace))
        {
            yield return token;
        }

        yield return new CodeToken(" ", TokenType.Whitespace);

        if (!string.IsNullOrEmpty(sig.CallingConvention))
        {
            yield return new CodeToken(sig.CallingConvention, TokenType.Keyword);
            yield return new CodeToken(" ", TokenType.Whitespace);
        }

        string name = sig.Name;
        if (string.IsNullOrEmpty(name))
            name = fallbackName;

        // Strip context namespace prefix (e.g. "Class::Method" -> "Method")
        if (!string.IsNullOrEmpty(contextNamespace) && name.StartsWith(contextNamespace + "::"))
        {
            name = name.Substring(contextNamespace.Length + 2);
        }

        yield return new CodeToken(name, TokenType.Identifier);

        yield return new CodeToken("(", TokenType.Punctuation);

        if (sig.Parameters != null && sig.Parameters.Any())
        {
            var pList = sig.Parameters.OrderBy(p => p.Position).ToList();
            for (int i = 0; i < pList.Count; i++)
            {
                var p = pList[i];
                foreach (var token in _tokenizationService.TokenizeTypeString(p.ParameterType ?? string.Empty,
                             p.TypeReference?.ReferencedTypeId, contextNamespace))
                {
                    yield return token;
                }

                if (!string.IsNullOrEmpty(p.Name))
                {
                    yield return new CodeToken(" ", TokenType.Whitespace);
                    yield return new CodeToken(p.Name, TokenType.Identifier);
                }

                if (i < pList.Count - 1)
                {
                    yield return new CodeToken(", ", TokenType.Punctuation);
                }
            }
        }

        yield return new CodeToken(")", TokenType.Punctuation);
    }
}
