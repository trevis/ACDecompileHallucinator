using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Utilities;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;
using System.Text.RegularExpressions;
using System.Text;

namespace ACDecompileParser.Shared.Lib.Output;

public abstract class TypeOutputGeneratorBase : ICodeGenerator
{
    protected ITypeRepository? Repository;

    protected TypeOutputGeneratorBase(ITypeRepository? repository = null)
    {
        Repository = repository;
    }

    protected TypeLookupCache? LookupCache;

    /// <summary>
    /// Sets the lookup cache for efficient type resolution without database queries.
    /// Should be called before Generate() if caching is desired.
    /// </summary>
    public void SetLookupCache(TypeLookupCache cache)
    {
        LookupCache = cache;
    }

    public abstract IEnumerable<CodeToken> Generate(TypeModel type);

    public virtual string GenerateDefinition(TypeModel type)
    {
        var tokens = Generate(type);
        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            sb.Append(token.Text);
        }

        return sb.ToString();
    }

    public List<string> GetDependencies(TypeModel type)
    {
        var dependencies = new List<string>();

        // Add dependencies for base types (inheritance)
        if (Repository != null)
        {
            var baseTypes = Repository.GetBaseTypesWithRelatedTypes(type.Id);
            foreach (var baseType in baseTypes)
            {
                if (baseType.RelatedType != null && !string.IsNullOrEmpty(baseType.RelatedType.BaseName))
                {
                    string baseTypeName = GetCleanBaseName(baseType.RelatedType.BaseName);
                    if (!string.IsNullOrEmpty(baseTypeName) && !IsPrimitiveType(baseTypeName) &&
                        !dependencies.Contains($"{baseTypeName}.h"))
                    {
                        dependencies.Add($"{baseTypeName}.h");
                    }
                }
                // Fallback to RelatedTypeString if RelatedType is not resolved
                else if (!string.IsNullOrEmpty(baseType.RelatedTypeString))
                {
                    string baseTypeName = GetCleanBaseName(baseType.RelatedTypeString);
                    if (!string.IsNullOrEmpty(baseTypeName) && !IsPrimitiveType(baseTypeName) &&
                        !dependencies.Contains($"{baseTypeName}.h"))
                    {
                        dependencies.Add($"{baseTypeName}.h");
                    }
                }
            }
        }
        else
        {
            // Fallback to the original method if no repository is available
            foreach (var baseType in type.BaseTypes)
            {
                if (baseType.RelatedType != null && !string.IsNullOrEmpty(baseType.RelatedType.BaseName))
                {
                    string baseTypeName = GetCleanBaseName(baseType.RelatedType.BaseName);
                    if (!string.IsNullOrEmpty(baseTypeName) && !IsPrimitiveType(baseTypeName) &&
                        !dependencies.Contains($"{baseTypeName}.h"))
                    {
                        dependencies.Add($"{baseTypeName}.h");
                    }
                }
                // Fallback to RelatedTypeString if RelatedType is not resolved
                else if (!string.IsNullOrEmpty(baseType.RelatedTypeString))
                {
                    string baseTypeName = GetCleanBaseName(baseType.RelatedTypeString);
                    if (!string.IsNullOrEmpty(baseTypeName) && !IsPrimitiveType(baseTypeName) &&
                        !dependencies.Contains($"{baseTypeName}.h"))
                    {
                        dependencies.Add($"{baseTypeName}.h");
                    }
                }
            }
        }

        // Add dependencies for member types if this is a struct
        if (type.Type == TypeType.Struct && Repository != null)
        {
            var structMembers = Repository.GetStructMembersWithRelatedTypes(type.Id);
            foreach (var member in structMembers)
            {
                if (member.TypeReference != null && !string.IsNullOrEmpty(member.TypeReference.TypeString))
                {
                    string memberTypeName = GetCleanBaseName(member.TypeReference.TypeString);
                    if (!string.IsNullOrEmpty(memberTypeName) && !IsPrimitiveType(memberTypeName) &&
                        !dependencies.Contains($"{memberTypeName}.h"))
                    {
                        dependencies.Add($"{memberTypeName}.h");
                    }
                }
                // Fallback to TypeString if TypeReference is not resolved
                else if (!string.IsNullOrEmpty(member.TypeString))
                {
                    string memberTypeName = GetCleanBaseName(member.TypeString);
                    if (!string.IsNullOrEmpty(memberTypeName) && !IsPrimitiveType(memberTypeName) &&
                        !dependencies.Contains($"{memberTypeName}.h"))
                    {
                        dependencies.Add($"{memberTypeName}.h");
                    }
                }
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Extracts a clean base name from a type string, removing template arguments and handling vtables
    /// </summary>
    /// <param name="typeString">The type string to clean</param>
    /// <returns>A clean base name suitable for include paths</returns>
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

    /// <summary>
    /// Checks if a type name represents a primitive type that should not be included
    /// </summary>
    /// <param name="typeName">The type name to check</param>
    /// <returns>True if the type is a primitive type, false otherwise</returns>
    protected bool IsPrimitiveType(string typeName)
    {
        // Check if the type name is in the primitive types set
        if (Constants.PrimitiveTypes.TypeNames.Contains(typeName))
            return true;

        // Check for common primitive combinations like "unsigned int", "long long", etc.
        // Normalize the type name by replacing multiple spaces with single space
        string normalizedTypeName = Regex.Replace(typeName.Trim(), @"\s+", " ");

        // Check for common primitive combinations
        if (Constants.PrimitiveTypes.TypeCombinations.Contains(normalizedTypeName))
            return true;

        // Check if it's a pointer to a primitive type
        if (normalizedTypeName.EndsWith("*") || normalizedTypeName.EndsWith("&"))
        {
            string baseType = normalizedTypeName.Substring(0, normalizedTypeName.Length - 1).Trim();
            return IsPrimitiveType(baseType);
        }

        return false;
    }

    private readonly TypeRemappingService _remappingService = new();

    protected virtual IEnumerable<CodeToken> TokenizeTypeString(string typeString, int? primaryReferencedTypeId = null,
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
                    else if (Repository != null)
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

    private string? ResolveTypeId(string typeName, string? contextNamespace)
    {
        // Fast path: use cache if available (no database queries)
        if (LookupCache != null)
        {
            // 1. Exact FQN match
            if (LookupCache.TryGetIdByFqn(typeName, out int id))
                return id.ToString();

            // 2. Namespace aggregation
            if (!string.IsNullOrEmpty(contextNamespace))
            {
                string currentNs = contextNamespace;
                while (!string.IsNullOrEmpty(currentNs))
                {
                    if (LookupCache.TryGetIdByFqn($"{currentNs}::{typeName}", out id))
                        return id.ToString();

                    int lastColon = currentNs.LastIndexOf("::");
                    currentNs = lastColon >= 0 ? currentNs.Substring(0, lastColon) : string.Empty;
                }
            }

            // 3. BaseName fallback
            if (LookupCache.TryGetIdsByBaseName(typeName, out var ids) && ids?.Count > 0)
                return ids[0].ToString();

            return null; // Cache miss means type doesn't exist
        }

        // Slow path: fallback to repository queries (for when no cache is set)
        if (Repository == null)
            return null;

        // 1. Exact match by FQN
        var resolved = Repository.GetTypeByFullyQualifiedName(typeName);
        if (resolved != null)
            return resolved.Id.ToString();

        // 2. Try namespace aggregation if context is provided
        if (!string.IsNullOrEmpty(contextNamespace))
        {
            string currentNs = contextNamespace;
            while (!string.IsNullOrEmpty(currentNs))
            {
                string candidate = $"{currentNs}::{typeName}";
                resolved = Repository.GetTypeByFullyQualifiedName(candidate);
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
        var candidates = Repository.GetTypesForGroup(typeName, "");
        if (candidates?.Any() == true)
            return candidates.First().Id.ToString();

        return null;
    }
}
