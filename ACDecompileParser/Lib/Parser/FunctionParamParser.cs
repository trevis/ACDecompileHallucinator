using ACDecompileParser.Shared.Lib.Models;
using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public class FunctionParamParser
{
    /// <summary>
    /// Parses function parameters from a parameter string (e.g., "int param1, float param2")
    /// </summary>
    public static List<FunctionParamModel> ParseFunctionParameters(string parametersString, string? file = null,
        int? lineNumber = null, string? source = null)
    {
        var parameters = new List<FunctionParamModel>();

        if (string.IsNullOrWhiteSpace(parametersString))
        {
            return parameters;
        }

        // Split parameters by comma, but respect nested angle brackets, parentheses, and brackets
        var paramList = SplitFunctionParametersCorrectly(parametersString);

        for (int i = 0; i < paramList.Count; i++)
        {
            var param = paramList[i].Trim();
            if (string.IsNullOrEmpty(param))
                continue;

            // Remove "struct " keyword globally from the parameter string to handle cases like "struct Collection *"
            param = Regex.Replace(param, @"\bstruct\s+", "");

            // Check if this parameter is a function pointer type
            if (ParsingUtilities.IsFunctionPointerParameter(param))
            {
                var funcPtrParam = ParseFunctionPointerParameter(param, i, file, lineNumber, source);
                if (funcPtrParam != null)
                {
                    parameters.Add(funcPtrParam);
                    continue;
                }
            }

            bool isPointer = false;
            bool isConst = false;
            // Extract the rightmost identifier (potential parameter name)
            var (paramTypeString, paramName) = ExtractRightmostIdentifier(param);

            // If we didn't extract anything (unnamed parameter), use the whole param as the type
            if (string.IsNullOrWhiteSpace(paramTypeString) && string.IsNullOrWhiteSpace(paramName))
            {
                paramTypeString = param;
                paramName = string.Empty;
            }

            // Normalize the type string (remove extra spaces, normalize pointer/reference syntax)
            paramTypeString = ParsingUtilities.NormalizeTypeString(paramTypeString);

            // Process the type string to extract const, pointers, and the base type
            string cleanTypeString = paramTypeString;

            // First, check for const at the beginning of the type string
            if (cleanTypeString.StartsWith("const ", StringComparison.OrdinalIgnoreCase))
            {
                isConst = true;
                cleanTypeString = cleanTypeString.Substring(6).Trim();
                if (cleanTypeString.EndsWith("const"))
                {
                    cleanTypeString = cleanTypeString.Substring(0, cleanTypeString.Length - 5).Trim();
                }
            }

            // Process pointers from the end of the string
            int pointerDepth = 0;
            int endPos = cleanTypeString.Length - 1;
            while (endPos >= 0 && (cleanTypeString[endPos] == '*' || cleanTypeString[endPos] == ' ' ||
                                   cleanTypeString[endPos] == '\t'))
            {
                if (cleanTypeString[endPos] == '*')
                {
                    isPointer = true;
                    pointerDepth++;
                }

                endPos--;
            }

            if (endPos >= 0)
            {
                cleanTypeString = cleanTypeString.Substring(0, endPos + 1).Trim();
            }
            else
            {
                cleanTypeString = string.Empty;
            }

            // Auto-name unnamed parameters as __param1, __param2, etc.
            if (string.IsNullOrEmpty(paramName))
            {
                paramName = $"__param{i + 1}";
            }
            else
            {
                // Validate the parameter name
                ParsingUtilities.ValidateParsedName(paramName, param, "FunctionParamParser", file, lineNumber);
            }

            // Validate the parameter type
            ParsingUtilities.ValidateParsedType(cleanTypeString, param, "FunctionParamParser", file, lineNumber);

            var typeReference = TypeResolver.CreateTypeReference(paramTypeString);

            // Update the TypeReference with the actual pointer and const information
            typeReference.IsPointer = isPointer;
            typeReference.IsConst = isConst;
            typeReference.IsReference = false; // We don't detect reference parameters in function parameters
            typeReference.PointerDepth = pointerDepth;

            parameters.Add(new FunctionParamModel
            {
                ParameterType = paramTypeString,
                Name = paramName,
                Source = source ?? param.Trim(),
                Position = i,
                LineNumber = lineNumber,
                File = file,
                TypeReference = typeReference,
                TypeReferenceId = null // Will be set when saving to database
            });
        }

        // Handle duplicate parameter names by renaming them with _1, _2, _3 suffixes
        RenameDuplicateParameters(parameters);

        return parameters;
    }

    /// <summary>
    /// Parses a function pointer parameter (e.g., HRESULT (__cdecl *)(const unsigned __int16 *))
    /// </summary>
    private static FunctionParamModel? ParseFunctionPointerParameter(string param, int position, string? file = null,
        int? lineNumber = null, string? source = null)
    {
        var (returnType, callingConvention, innerParams, extractedName, pointerDepth) =
            ParsingUtilities.ExtractFunctionPointerParameterInfo(param);

        if (returnType == null)
            return null;

        // Use extracted name if available, otherwise generate one
        var paramName = !string.IsNullOrEmpty(extractedName)
            ? extractedName
            : $"__param{position + 1}";

        // Create the function signature for this parameter
        var functionSignature = new FunctionSignatureModel
        {
            Name = paramName,
            ReturnType = ParsingUtilities.NormalizeTypeString(returnType),
            CallingConvention = callingConvention ?? string.Empty,
            ReturnTypeReference = TypeResolver.CreateTypeReference(returnType),
            Source = source ?? param.Trim(),
            File = file,
            LineNumber = lineNumber
        };

        // Parse the inner parameters of the function pointer type
        if (!string.IsNullOrEmpty(innerParams))
        {
            var innerParamList = ParseFunctionSignatureParameters(innerParams, file, lineNumber, source);
            functionSignature.Parameters = innerParamList;
        }

        // Create the FunctionParamModel that references this signature
        return new FunctionParamModel
        {
            ParameterType = param, // Store the full function pointer type string
            Name = paramName,
            Source = source ?? param.Trim(),
            LineNumber = lineNumber,
            File = file,
            Position = position,
            IsFunctionPointerType = true,
            PointerDepth = pointerDepth,
            NestedFunctionSignature = functionSignature,
            TypeReference = null, // Function pointer params don't have a simple type reference
            TypeReferenceId = null
        };
    }

    /// <summary>
    /// Parses parameters for a function signature (used for nested function pointer types)
    /// </summary>
    public static List<FunctionParamModel> ParseFunctionSignatureParameters(string parametersString,
        string? file = null, int? lineNumber = null, string? source = null)
    {
        var parameters = new List<FunctionParamModel>();

        if (string.IsNullOrWhiteSpace(parametersString))
            return parameters;

        var paramList = SplitFunctionParametersCorrectly(parametersString);

        for (int i = 0; i < paramList.Count; i++)
        {
            var param = paramList[i].Trim();
            if (string.IsNullOrEmpty(param))
                continue;

            // Remove "struct " keyword
            param = Regex.Replace(param, @"\bstruct\s+", "");

            // Check if this is a nested function pointer
            if (ParsingUtilities.IsFunctionPointerParameter(param))
            {
                var nestedFuncSig = ParseNestedFunctionSignature(param, i, file, lineNumber, source);
                if (nestedFuncSig.HasValue)
                {
                    parameters.Add(new FunctionParamModel
                    {
                        ParameterType = param,
                        Name = nestedFuncSig.Value.Signature.Name,
                        Position = i,
                        IsFunctionPointerType = true,
                        NestedFunctionSignature = nestedFuncSig.Value.Signature,
                        Source = source ?? param.Trim(),
                        LineNumber = lineNumber,
                        File = file,
                        TypeReference = null
                        // ParentFunctionSignatureId will be set automatically by EF Core via the navigation property
                    });
                    continue;
                }
            }

            // Parse as a regular parameter
            var (paramTypeString, paramName) = ExtractRightmostIdentifier(param);

            if (string.IsNullOrWhiteSpace(paramTypeString) && string.IsNullOrWhiteSpace(paramName))
            {
                paramTypeString = param;
                paramName = string.Empty;
            }

            paramTypeString = ParsingUtilities.NormalizeTypeString(paramTypeString);

            if (string.IsNullOrEmpty(paramName))
            {
                paramName = $"__param{i + 1}";
            }

            parameters.Add(new FunctionParamModel
            {
                ParameterType = paramTypeString,
                Name = paramName,
                Position = i,
                Source = source ?? param.Trim(),
                LineNumber = lineNumber,
                File = file,
                TypeReference = TypeResolver.CreateTypeReference(paramTypeString)
                // ParentFunctionSignatureId will be set automatically by EF Core via the navigation property
            });
        }

        return parameters;
    }

    /// <summary>
    /// Parses a nested function pointer to create a FunctionSignatureModel
    /// </summary>
    private static (FunctionSignatureModel Signature, string RawParam)? ParseNestedFunctionSignature(string param,
        int position, string? file = null, int? lineNumber = null, string? source = null)
    {
        var (returnType, callingConvention, innerParams, extractedName, _) =
            ParsingUtilities.ExtractFunctionPointerParameterInfo(param);

        if (returnType == null)
            return null;

        // Use extracted name if available, otherwise generate one
        var funcName = !string.IsNullOrEmpty(extractedName)
            ? extractedName
            : $"__nested_funcptr{position + 1}";

        var funcSig = new FunctionSignatureModel
        {
            Name = funcName,
            ReturnType = ParsingUtilities.NormalizeTypeString(returnType),
            CallingConvention = callingConvention ?? string.Empty,
            ReturnTypeReference = TypeResolver.CreateTypeReference(returnType),
            Source = source ?? param.Trim(),
            File = file,
            LineNumber = lineNumber
        };

        // Recursively parse inner parameters
        if (!string.IsNullOrEmpty(innerParams))
        {
            funcSig.Parameters = ParseFunctionSignatureParameters(innerParams, file, lineNumber, source);
        }

        return (funcSig, param);
    }

    /// <summary>
    /// Renames duplicate parameter names by adding numeric suffixes (_1, _2, etc.)
    /// </summary>
    private static void RenameDuplicateParameters(List<FunctionParamModel> parameters)
    {
        // Group parameters by name to find duplicates
        var nameGroups = parameters
            .GroupBy(p => p.Name)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in nameGroups)
        {
            int suffix = 1;
            foreach (var param in group)
            {
                param.Name = $"{param.Name}_{suffix}";
                suffix++;
            }
        }
    }


    /// <summary>
    /// Extracts the rightmost identifier from a parameter string.
    /// Returns (stringWithoutIdentifier, identifier).
    /// If the identifier is the only thing in the string (unnamed type parameter), returns (parameter, empty).
    /// </summary>
    private static (string WithoutIdentifier, string Identifier) ExtractRightmostIdentifier(string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            return (string.Empty, string.Empty);

        parameter = parameter.Trim();

        // 1. Skip trailing modifiers (*, &, const, volatile, whitespace) to find the end of the identifier
        int endPos = SkipTrailingModifiers(parameter);

        if (endPos < 0)
            return (parameter, string.Empty); // Only modifiers found

        // 2. Scan backwards to find the start of the identifier
        var (identifier, startPos) = ScanIdentifierBackwards(parameter, endPos);

        if (string.IsNullOrEmpty(identifier))
            return (parameter, string.Empty);

        // 3. Heuristics to determine if this identifier is a parameter name or part of the type
        bool isPartOfType = false;

        // If it's a known C++ keyword, starts with __, or contains ::, it's definitely a type
        if (ParsingUtilities.IsCppTypeKeyword(identifier) || identifier.StartsWith("__") || identifier.Contains("::"))
        {
            isPartOfType = true;
        }
        else
        {
            string preceding = parameter.Substring(0, startPos).TrimEnd();

            // If preceding is empty or only modifiers, then identifier MUST be the type name
            // (e.g., "tagDVTARGETDEVICE *", "const char *")
            if (string.IsNullOrEmpty(preceding) || IsModifierOnly(preceding))
            {
                isPartOfType = true;
            }
            // If preceding ends with :: or >, it's likely a type (e.g. Namespace::Type or Template<T>::Type)
            else if (preceding.EndsWith("::"))
            {
                isPartOfType = true;
            }
        }

        if (isPartOfType)
        {
            return (parameter, string.Empty);
        }

        // Special case: "const" check (redundant but safe)
        if (identifier.Equals("const", StringComparison.OrdinalIgnoreCase))
        {
            return (parameter, string.Empty);
        }

        // 4. It's a valid parameter name!
        // The type part is everything EXCEPT the identifier we found.
        // We must preserve everything before AND after the identifier (the modifiers at the end).
        string before = parameter.Substring(0, startPos).TrimEnd();
        string after = parameter.Substring(endPos + 1).Trim();
        string typePart = (before + " " + after).Trim();

        // Normalize spaces
        typePart = Regex.Replace(typePart, @"\s+", " ");

        // Handle "struct Name" case where "struct " is in typePart
        if (typePart.StartsWith("struct ", StringComparison.OrdinalIgnoreCase))
        {
            typePart = typePart.Substring(7).Trim();
        }

        return (typePart, identifier);
    }

    /// <summary>
    /// Checks if a string contains only C++ type modifiers (const, volatile, etc.)
    /// </summary>
    private static bool IsModifierOnly(string text)
    {
        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        foreach (var part in parts)
        {
            if (!part.Equals("const", StringComparison.OrdinalIgnoreCase) &&
                !part.Equals("volatile", StringComparison.OrdinalIgnoreCase) &&
                !part.Equals("struct", StringComparison.OrdinalIgnoreCase) &&
                !part.Equals("enum", StringComparison.OrdinalIgnoreCase) &&
                !part.Equals("union", StringComparison.OrdinalIgnoreCase) &&
                !part.Equals("*") &&
                !part.Equals("&") &&
                !part.Equals("**")) // Handle double pointers without space
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Skips trailing modifiers (*, &, const, whitespace) from the end of the string.
    /// Returns the index of the last character of the actual identifier.
    /// </summary>
    private static int SkipTrailingModifiers(string text)
    {
        int pos = text.Length - 1;

        while (pos >= 0)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(text[pos]))
            {
                pos--;
                continue;
            }

            // Skip pointers and references
            if (text[pos] == '*' || text[pos] == '&')
            {
                pos--;
                continue;
            }

            // Check for "const"
            if (pos >= 4)
            {
                string last5 = text.Substring(Math.Max(0, pos - 4), Math.Min(5, pos + 1));
                if (last5.Equals("const", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's a whole word or followed by a symbol we skip
                    // Actually, if we are skipping trailing modifiers, we can just skip it.
                    pos -= 5;
                    continue;
                }
            }

            // Check for "volatile" just in case
            if (pos >= 7)
            {
                string last8 = text.Substring(Math.Max(0, pos - 7), Math.Min(8, pos + 1));
                if (last8.Equals("volatile", StringComparison.OrdinalIgnoreCase))
                {
                    pos -= 8;
                    continue;
                }
            }

            // Found something that isn't a modifier
            break;
        }

        return pos;
    }

    /// <summary>
    /// Scans backwards from endPos to find the start of an identifier.
    /// Handles namespace qualifiers (::) correctly.
    /// Returns the identifier and its start position.
    /// </summary>
    private static (string Identifier, int StartPos) ScanIdentifierBackwards(string text, int endPos)
    {
        int currentPos = endPos;
        int identifierEnd = endPos + 1;

        while (currentPos >= 0)
        {
            // Scan backwards for alphanumeric characters
            while (currentPos >= 0 && (char.IsLetterOrDigit(text[currentPos]) || text[currentPos] == '_' ||
                                       text[currentPos] == '~'))
                currentPos--;

            // Check if we hit a namespace separator (::)
            if (currentPos >= 1 && text.Substring(currentPos - 1, 2) == "::")
            {
                currentPos -= 2;
                // Continue scanning the next part of the identifier (the namespace part)
                continue;
            }

            break;
        }

        int startPos = currentPos + 1;
        if (startPos >= identifierEnd)
            return (string.Empty, -1);

        string identifier = text.Substring(startPos, identifierEnd - startPos);
        return (identifier, startPos);
    }


    /// <summary>
    /// Splits function parameters by comma, respecting nested angle brackets, parentheses, and brackets
    /// </summary>
    private static List<string> SplitFunctionParametersCorrectly(string parametersString)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        int angleBracketDepth = 0;
        int parenDepth = 0;
        int bracketDepth = 0;

        for (int i = 0; i < parametersString.Length; i++)
        {
            char c = parametersString[i];

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
            else if (c == '(')
            {
                parenDepth++;
                current.Append(c);
            }
            else if (c == ')')
            {
                parenDepth--;
                current.Append(c);
            }
            else if (c == '[')
            {
                bracketDepth++;
                current.Append(c);
            }
            else if (c == ']')
            {
                bracketDepth--;
                current.Append(c);
            }
            else if (c == ',' && angleBracketDepth == 0 && parenDepth == 0 && bracketDepth == 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
