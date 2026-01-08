using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Shared.Lib.Services;

public class TypeTokenizationService : ITypeTokenizationService
{
    private readonly ITypeRepository? _repository;
    private TypeLookupCache? _lookupCache;
    private readonly TypeRemappingService _remappingService = new();

    public TypeTokenizationService(ITypeRepository? repository = null)
    {
        _repository = repository;
    }

    public void SetLookupCache(TypeLookupCache cache)
    {
        _lookupCache = cache;
    }

    public IEnumerable<CodeToken> TokenizeTypeString(string typeString, int? primaryReferencedTypeId = null,
        string? contextNamespace = null)
    {
        if (string.IsNullOrEmpty(typeString))
            yield break;

        // illegal memory access workaround: Remap types (e.g. _BYTE -> byte) before tokenizing
        typeString = _remappingService.RemapTypeString(typeString);

        // Use regex to tokenize: words (including namespace separators), numbers, and punctuation
        // Groups:
        // 1. Identifiers/Keywords (including ::) - [a-zA-Z_][a-zA-Z0-9_]*(?:::[a-zA-Z_][a-zA-Z0-9_]*)*
        // 2. Numbers - \d+
        // 3. Punctuation - [^\w\s]
        var regex = new Regex(@"([a-zA-Z_][a-zA-Z0-9_]*(?:::[a-zA-Z_][a-zA-Z0-9_]*)*)|(\d+)|([^\w\s])",
            RegexOptions.Compiled);
        var matches = regex.Matches(typeString);

        bool firstIdentifier = true;
        int lastEndIndex = 0;

        foreach (Match match in matches)
        {
            // Emit whitespace between tokens if there's a gap
            if (match.Index > lastEndIndex)
            {
                string whitespace = typeString.Substring(lastEndIndex, match.Index - lastEndIndex);
                yield return new CodeToken(whitespace, TokenType.Whitespace);
            }

            if (match.Groups[1].Success) // Identifier or Keyword
            {
                string text = match.Value;

                // Check if it's a non-type keyword (like const, static, public, etc.)
                // Type keywords (int, char, float, etc.) should be treated as TypeName in type contexts
                bool isNonTypeKeyword = ParsingUtilities.IsCppTypeKeyword(text) &&
                                        !ParsingUtilities.IsPrimitiveTypeKeyword(text);

                if (isNonTypeKeyword)
                {
                    yield return new CodeToken(text, TokenType.Keyword);
                }
                else
                {
                    string? refId = null;
                    if (firstIdentifier && primaryReferencedTypeId.HasValue)
                    {
                        refId = primaryReferencedTypeId.Value.ToString();
                        firstIdentifier = false;
                    }
                    else if (_repository != null)
                    {
                        // Try to resolve the identifier
                        refId = ResolveTypeId(text, contextNamespace);
                    }

                    yield return new CodeToken(text, TokenType.TypeName, refId);
                }
            }
            else if (match.Groups[2].Success) // Number
            {
                yield return new CodeToken(match.Value, TokenType.NumberLiteral);
            }
            else if (match.Groups[3].Success) // Punctuation
            {
                yield return new CodeToken(match.Value, TokenType.Punctuation);
            }

            lastEndIndex = match.Index + match.Length;
        }
    }

    public string GetCleanBaseName(string typeString)
    {
        if (string.IsNullOrEmpty(typeString))
            return string.Empty;

        // Use ParsingUtilities to extract the base name
        var baseName = ParsingUtilities.ExtractBaseTypeName(typeString);

        // Handle vtable names - remove "_vtbl" suffix if present
        if (baseName.EndsWith("_vtbl"))
        {
            baseName = baseName.Substring(0, baseName.Length - 5); // Remove "_vtbl"
        }

        return baseName;
    }

    private string? ResolveTypeId(string typeName, string? contextNamespace)
    {
        // Fast path: use cache if available (no database queries)
        if (_lookupCache != null)
        {
            // 1. Exact FQN match
            if (_lookupCache.TryGetIdByFqn(typeName, out int id))
                return id.ToString();

            // 2. Namespace aggregation
            if (!string.IsNullOrEmpty(contextNamespace))
            {
                string currentNs = contextNamespace;
                while (!string.IsNullOrEmpty(currentNs))
                {
                    if (_lookupCache.TryGetIdByFqn($"{currentNs}::{typeName}", out id))
                        return id.ToString();

                    int lastColon = currentNs.LastIndexOf("::");
                    currentNs = lastColon >= 0 ? currentNs.Substring(0, lastColon) : string.Empty;
                }
            }

            // 3. BaseName fallback
            if (_lookupCache.TryGetIdsByBaseName(typeName, out var ids) && ids?.Count > 0)
                return ids[0].ToString();

            return null; // Cache miss means type doesn't exist
        }

        // Slow path: fallback to repository queries (for when no cache is set)
        if (_repository == null)
            return null;

        // 1. Exact match by FQN
        var resolved = _repository.GetTypeByFullyQualifiedName(typeName);
        if (resolved != null)
            return resolved.Id.ToString();

        // 2. Try namespace aggregation if context is provided
        if (!string.IsNullOrEmpty(contextNamespace))
        {
            string currentNs = contextNamespace;
            while (!string.IsNullOrEmpty(currentNs))
            {
                string candidate = $"{currentNs}::{typeName}";
                resolved = _repository.GetTypeByFullyQualifiedName(candidate);
                if (resolved != null)
                    return resolved.Id.ToString();

                int lastColon = currentNs.LastIndexOf("::");
                if (lastColon >= 0)
                    currentNs = currentNs.Substring(0, lastColon);
                else
                    currentNs = string.Empty;
            }
        }

        // 3. Fallback: Search by BaseName
        var candidates = _repository.GetTypesForGroup(typeName, "");
        if (candidates?.Any() == true)
            return candidates.First().Id.ToString();

        return null;
    }
}
