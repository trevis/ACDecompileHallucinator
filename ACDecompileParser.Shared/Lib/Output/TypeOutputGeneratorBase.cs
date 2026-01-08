using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;
using System.Text.RegularExpressions;
using System.Text;

namespace ACDecompileParser.Shared.Lib.Output;

public abstract class TypeOutputGeneratorBase : ICodeGenerator
{
    protected ITypeRepository? Repository;
    protected readonly ITypeTokenizationService TokenizationService;

    protected TypeOutputGeneratorBase(ITypeRepository? repository = null,
        ITypeTokenizationService? tokenizationService = null)
    {
        Repository = repository;
        TokenizationService = tokenizationService ?? new TypeTokenizationService(repository);
    }

    protected TypeLookupCache? LookupCache;

    /// <summary>
    /// Sets the lookup cache for efficient type resolution without database queries.
    /// Should be called before Generate() if caching is desired.
    /// </summary>
    public void SetLookupCache(TypeLookupCache cache)
    {
        LookupCache = cache;
        TokenizationService.SetLookupCache(cache);
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
        return TokenizationService.GetCleanBaseName(typeString);
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


    protected virtual IEnumerable<CodeToken> TokenizeTypeString(string typeString, int? primaryReferencedTypeId = null,
        string? contextNamespace = null)
    {
        return TokenizationService.TokenizeTypeString(typeString, primaryReferencedTypeId, contextNamespace);
    }
}
