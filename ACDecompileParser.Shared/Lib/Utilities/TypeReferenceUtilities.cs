using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Lib.Utilities;

public static class TypeReferenceUtilities
{
    /// <summary>
    /// Creates a TypeReference from a type string by parsing it with TypeParsingUtilities
    /// </summary>
    public static TypeReference CreateTypeReference(string typeString)
    {
        // Normalize the type string to ensure consistent formatting
        var normalizedTypeString = ParsingUtilities.NormalizeTypeString(typeString);
        
        // We need to access TypeParsingUtilities to parse the type string
        var parsedTypeInfo = TypeParsingUtilities.ParseType(normalizedTypeString);
        
        // Create a normalized fully qualified type name that follows the same logic as TypeModel.FullyQualifiedName
        // but ensures normalized template arguments
        var fullyQualifiedType = GetNormalizedFullyQualifiedTypeName(parsedTypeInfo);
        
        return new TypeReference
        {
            TypeString = normalizedTypeString,  // Use the normalized type string
            FullyQualifiedType = fullyQualifiedType,  // Use the normalized fully qualified type name
            IsConst = parsedTypeInfo.IsConst,
            IsPointer = parsedTypeInfo.IsPointer,
            IsReference = parsedTypeInfo.IsReference,
            PointerDepth = parsedTypeInfo.PointerDepth
        };
    }
    
    /// <summary>
    /// Gets a normalized fully qualified type name with namespace and template arguments normalized
    /// </summary>
    private static string GetNormalizedFullyQualifiedTypeName(ParsedTypeInfo parsedTypeInfo)
    {
        var nameWithTemplates = GetNormalizedNameWithTemplates(parsedTypeInfo);
        return string.IsNullOrEmpty(parsedTypeInfo.Namespace) ? nameWithTemplates : $"{parsedTypeInfo.Namespace}::{nameWithTemplates}";
    }
    
    /// <summary>
    /// Gets a normalized name with templates that normalizes the template arguments
    /// </summary>
    private static string GetNormalizedNameWithTemplates(ParsedTypeInfo parsedTypeInfo)
    {
        if (!parsedTypeInfo.IsGeneric)
            return parsedTypeInfo.BaseName;
        
        // Process each template argument to normalize spaces around commas
        var normalizedArgs = new List<string>();
        foreach (var arg in parsedTypeInfo.TemplateArguments)
        {
            // Use the normalized type string from the parsed argument and normalize spaces around commas
            string normalizedArg = NormalizeTemplateArgument(arg.FullTypeString);
            normalizedArgs.Add(normalizedArg);
        }
        
        var args = string.Join(",", normalizedArgs);
        return $"{parsedTypeInfo.BaseName}<{args}>";
    }
    
    /// <summary>
    /// Normalizes a template argument by removing spaces around commas
    /// </summary>
    private static string NormalizeTemplateArgument(string arg)
    {
        // Remove spaces before commas
        arg = System.Text.RegularExpressions.Regex.Replace(arg, @"\s*,", ",");
        // Remove spaces after commas (though this should already be handled by NormalizeTypeString)
        arg = System.Text.RegularExpressions.Regex.Replace(arg, @",\s*", ",");
        return arg;
    }
}
