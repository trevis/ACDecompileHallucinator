using ACDecompileParser.Shared.Lib.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace ACDecompileParser.Shared.Lib.Utilities;

public class TypeParsingUtilities
{
    /// <summary>
    /// Parses a type string and returns a ParsedTypeInfo object
    /// </summary>
    public static ParsedTypeInfo ParseType(string typeString)
    {
        var result = new ParsedTypeInfo();
        
        // Store the original type string before any modifications
        var originalTypeString = typeString;
        
        // Clean the type string first - just use basic cleaning for now
        typeString = StripComments(typeString);
        
        // Check for empty or whitespace-only strings first
        if (string.IsNullOrWhiteSpace(typeString))
        {
            result.FullTypeString = string.Empty;
            result.BaseName = string.Empty;
            result.Namespace = string.Empty;
            return result;
        }
        
        result.FullTypeString = typeString;  // Store the cleaned but unnormalized string
        
        // Check for const modifier
        if (typeString.StartsWith("const ", StringComparison.OrdinalIgnoreCase))
        {
            result.IsConst = true;
            typeString = typeString.Substring(6).Trim();
        }
        // Check for volatile modifier
        else if (typeString.StartsWith("volatile ", StringComparison.OrdinalIgnoreCase))
        {
            result.IsVolatile = true;
            typeString = typeString.Substring(9).Trim();
        }
        
        // Check for pointer or reference at the end
        if (typeString.EndsWith("*"))
        {
            result.IsPointer = true;
            int count = 0;
            while (typeString.EndsWith("*"))
            {
                count++;
                typeString = typeString.Substring(0, typeString.Length - 1).Trim();
            }
            result.PointerDepth = count;
        }
        else if (typeString.EndsWith("&"))
        {
            result.IsReference = true;
            typeString = typeString.Substring(0, typeString.Length - 1).Trim();
        }
        
        // Handle the case where the type has nested structure with templates
        // For cases like "AC1Legacy::PQueueArray<double>::PQueueNode", we need to find the last "::"
        // that's not inside angle brackets to separate the namespace from the base name first
        int lastNamespaceSeparator = FindLastNamespaceSeparatorOutsideTemplates(typeString);
        
        string baseTypeName = typeString;
        string namespacePart = string.Empty;
        
        if (lastNamespaceSeparator > 0)
        {
            namespacePart = typeString.Substring(0, lastNamespaceSeparator);
            baseTypeName = typeString.Substring(lastNamespaceSeparator + 2);
        }
        
        // Now parse the base type name (which may contain template arguments)
        int templateStart = baseTypeName.IndexOf('<');
        if (templateStart > 0)
        {
            // Find the matching closing bracket
            int templateEnd = FindMatchingCloseBracket(baseTypeName, templateStart);

            if (templateEnd > 0)
            {
                // Extract base name (everything before '<')
                string actualBaseName = baseTypeName.Substring(0, templateStart);

                // Extract any suffix after the closing '>' (e.g., "_vtbl")
                string suffix = "";
                if (templateEnd < baseTypeName.Length - 1)
                {
                    suffix = baseTypeName.Substring(templateEnd + 1);
                }

                // Extract template parameters (between '<' and '>')
                string templateParamsStr = baseTypeName.Substring(templateStart + 1, templateEnd - templateStart - 1);

                // Split template parameters by comma (respecting nested templates)
                var templateParams = SplitTemplateParameters(templateParamsStr);
                foreach (var param in templateParams)
                {
                    var parsedParam = ParseType(param);
                    result.TemplateArguments.Add(parsedParam);
                }
                
                // Combine namespace part with the actual base name and suffix
                if (!string.IsNullOrEmpty(namespacePart))
                {
                    result.BaseName = namespacePart + "::" + actualBaseName + suffix;
                }
                else
                {
                    result.BaseName = actualBaseName + suffix;
                }
            }
            else
            {
                // Fallback to old behavior if no matching bracket found - this handles malformed templates
                // For malformed templates, we should still extract the base name before the template part
                result.BaseName = baseTypeName.Substring(0, templateStart);  // Just the part before '<'
                
                // If there was a namespace part, combine it back
                if (!string.IsNullOrEmpty(namespacePart))
                {
                    result.BaseName = namespacePart + "::" + result.BaseName;
                }
                
                // Extract namespace again from the corrected base name
                int malformedFinalNamespaceEnd = FindLastNamespaceSeparatorOutsideTemplates(result.BaseName);
                if (malformedFinalNamespaceEnd > 0)
                {
                    result.Namespace = result.BaseName.Substring(0, malformedFinalNamespaceEnd);
                    result.BaseName = result.BaseName.Substring(malformedFinalNamespaceEnd + 2);
                }
                
                return result; // Return early for malformed templates to avoid double namespace processing
            }
        }
        else
        {
            // No templates in the base type name
            result.BaseName = typeString;
        }
        
        // Now extract namespace from the final BaseName if it contains "::"
        int finalNamespaceEnd = FindLastNamespaceSeparatorOutsideTemplates(result.BaseName);
        if (finalNamespaceEnd > 0)
        {
            result.Namespace = result.BaseName.Substring(0, finalNamespaceEnd);
            result.BaseName = result.BaseName.Substring(finalNamespaceEnd + 2);
        }
        // If no namespace separator found in the final BaseName, namespace remains empty
        // and the full type name is in BaseName
        
        return result;
    }
    
    /// <summary>
    /// Splits template parameters by comma, respecting nested angle brackets
    /// </summary>
    private static List<string> SplitTemplateParameters(string templateParamsStr)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        int angleBracketDepth = 0;

        for (int i = 0; i < templateParamsStr.Length; i++)
        {
            char c = templateParamsStr[i];

            if (c == '<')
            {
                angleBracketDepth++;
                current.Append(c);
            }
            else if (c == '>')
            {
                angleBracketDepth--;
                current.Append(c);
            }
            else if (c == ',' && angleBracketDepth == 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result;
    }

    /// <summary>
    /// Finds the matching closing bracket for an opening bracket at the specified index
    /// </summary>
    private static int FindMatchingCloseBracket(string str, int openBracketIndex)
    {
        if (openBracketIndex < 0 || openBracketIndex >= str.Length || str[openBracketIndex] != '<')
            return -1;

        int depth = 1;
        for (int i = openBracketIndex + 1; i < str.Length; i++)
        {
            if (str[i] == '<')
                depth++;
            else if (str[i] == '>')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Finds the last occurrence of "::" that is outside of any template arguments (angle brackets)
    /// </summary>
    private static int FindLastNamespaceSeparatorOutsideTemplates(string str)
    {
        int lastSeparator = -1;
        int angleBracketDepth = 0;
        
        for (int i = 0; i < str.Length - 1; i++)
        {
            if (str[i] == '<')
            {
                angleBracketDepth++;
            }
            else if (str[i] == '>')
            {
                angleBracketDepth--;
            }
            else if (str[i] == ':' && i + 1 < str.Length && str[i + 1] == ':' && angleBracketDepth == 0)
            {
                // Found a "::" outside of template arguments
                lastSeparator = i;
            }
        }
        
        return lastSeparator;
    }

    /// <summary>
    /// Removes C++ style comments (// and /* */) from a string
    /// </summary>
    private static string StripComments(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        var result = new StringBuilder();
        int i = 0;
        
        while (i < input.Length)
        {
            // Check for line comment
            if (i < input.Length - 1 && input[i] == '/' && input[i + 1] == '/')
            {
                // Skip until end of line
                while (i < input.Length && input[i] != '\n')
                {
                    i++;
                }
                continue;
            }
            
            // Check for block comment
            if (i < input.Length - 1 && input[i] == '/' && input[i + 1] == '*')
            {
                i += 2; // Skip /*
                // Skip until end of comment
                while (i < input.Length - 1 && !(input[i] == '*' && input[i + 1] == '/'))
                {
                    i++;
                }
                i += 2; // Skip */
                continue;
            }
            
            result.Append(input[i]);
            i++;
        }
        
        return result.ToString();
    }
}
