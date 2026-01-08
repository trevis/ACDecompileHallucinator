using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public class MemberParser
{
    private static int _paddingCounter;

    /// <summary>
    /// Resets the padding counter to 0. This is primarily used for testing purposes.
    /// </summary>
    public static void ResetPaddingCounter()
    {
        _paddingCounter = 0;
    }

    /// <summary>
    /// Parses a member declaration line and returns a StructMemberModel
    /// </summary>
    public static StructMemberModel? ParseMemberDeclaration(string line, int? lineNumber = null, string? file = null)
    {
        // Check for the special case: _BYTE[\d+]; without a name (may include comments like offsets)
        // This should be treated as an array with a generated __padding# name
        var paddingMatch = Regex.Match(line, @"^_BYTE\s*\[(\d+)\]\s*;.*$");
        if (paddingMatch.Success)
        {
            // Extract the array size
            if (int.TryParse(paddingMatch.Groups[1].Value, out int arraySize))
            {
                // Extract offset from comments in the original line first
                int? offset = null;
                var offsetMatch = Regex.Match(line, @"/\*\s*(0[xX][0-9a-fA-F]+|[0-9]+)\s*\*/");
                if (offsetMatch.Success)
                {
                    string offsetStr = offsetMatch.Groups[1].Value;
                    if (offsetStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        // Hexadecimal offset
                        if (int.TryParse(offsetStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null,
                                out int hexOffset))
                        {
                            offset = hexOffset;
                        }
                    }
                    else
                    {
                        // Decimal offset
                        if (int.TryParse(offsetStr, out int decOffset))
                        {
                            offset = decOffset;
                        }
                    }
                }

                // Generate a unique padding name
                string paddingName = $"__padding{_paddingCounter++}";

                // Create a TypeReference for the _BYTE type with array information
                var typeReference = TypeResolver.CreateTypeReference("_BYTE");
                typeReference.IsArray = true;
                typeReference.ArraySize = arraySize;

                return new StructMemberModel
                {
                    TypeString = "_BYTE",
                    Name = paddingName,
                    Source = line.Trim(),
                    Offset = offset,
                    TypeReference = typeReference,
                    TypeReferenceId = null, // Will be set when saving to database
                    LineNumber = lineNumber,
                    File = file
                };
            }
        }

        // Remove "struct " keyword globally, not just from the beginning
        line = Regex.Replace(line, @"\bstruct\s+", "");
        // Skip forward declarations (ending with semicolon but no assignment or braces)
        // Updated to handle lines ending with semicolon followed by comments
        bool actuallyEndsWithSemicolon = line.TrimEnd().EndsWith(";") ||
                                         Regex.IsMatch(line.TrimEnd(), @";\s*/\*.*\*/\s*$");
        if (actuallyEndsWithSemicolon && (!line.Contains('=') && !line.Contains('{') && !line.Contains('}')) ||
            line.Contains(" *operator"))
        {
            // Extract type and name from member declaration
            // Format: "type memberName;" or "type *memberName;" or "type memberName[arraysize];"

            // Extract offset from comments in the original line first
            int? offset = null;
            var offsetMatch = Regex.Match(line, @"/\*\s*(0[xX][0-9a-fA-F]+|[0-9]+)\s*\*/");
            if (offsetMatch.Success)
            {
                string offsetStr = offsetMatch.Groups[1].Value;
                if (offsetStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    // Hexadecimal offset
                    if (int.TryParse(offsetStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null,
                            out int hexOffset))
                    {
                        offset = hexOffset;
                    }
                }
                else
                {
                    // Decimal offset
                    if (int.TryParse(offsetStr, out int decOffset))
                    {
                        offset = decOffset;
                    }
                }
            }

            // Remove comments like "/* 0x008 */" from the line to get the clean declaration
            var cleanLine = Regex
                .Replace(line, @"/\*\s*(0[xX][0-9a-fA-F]+|[0-9]+)\s*\*/\s*", string.Empty)
                .Trim();

            // Now trim the semicolon from the cleaned line
            cleanLine = cleanLine.TrimEnd(';').Trim();

            // Extract alignment information (e.g., "__declspec(align(8))") before other parsing
            var (hasAlignment, alignment) = ParsingUtilities.ExtractAlignmentInfo(cleanLine);

            // Remove alignment syntax from the line to avoid interfering with function pointer parsing
            if (hasAlignment)
            {
                cleanLine = ParsingUtilities.RemoveAlignmentSyntax(cleanLine);
            }

            // Check if this is a function pointer that returns a function pointer
            if (ParsingUtilities.HasFunctionPointerReturnType(cleanLine))
            {
                var nestedInfo = ParsingUtilities.ExtractNestedFunctionPointerInfo(cleanLine);
                if (nestedInfo.FunctionName != null)
                {
                    return ParseNestedFunctionPointerMember(cleanLine, nestedInfo, offset, alignment, lineNumber, file);
                }
            }

            // Check if this is a regular function pointer
            var functionPointerInfo = ParsingUtilities.ExtractFunctionPointerInfo(cleanLine);

            if (functionPointerInfo.IsFunctionPointer)
            {
                var returnType = ParsingUtilities.NormalizeTypeString(functionPointerInfo.ReturnType);
                if (returnType.EndsWith("*"))
                {
                    returnType = returnType.TrimEnd('*').Trim();
                }

                // Create a function signature model for this function pointer
                var functionSignature = new FunctionSignatureModel
                {
                    Name = $"__sig_{functionPointerInfo.Name}",
                    ReturnType = ParsingUtilities.NormalizeTypeString(functionPointerInfo.ReturnType),
                    CallingConvention = functionPointerInfo.CallingConvention,
                    ReturnTypeReference = TypeResolver.CreateTypeReference(functionPointerInfo.ReturnType),
                    Parameters = ParseFunctionParameters(functionPointerInfo.Parameters ?? string.Empty, file,
                        lineNumber, line),
                    Source = line.Trim(),
                    File = file,
                    LineNumber = lineNumber
                };

                functionSignature.FullyQualifiedName = ParsingUtilities.GenerateNormalizedFunctionSignature(
                    functionSignature.ReturnType,
                    functionSignature.CallingConvention,
                    functionSignature.Name,
                    functionSignature.Parameters);

                // Create a function pointer member
                var typeReference = TypeResolver.CreateTypeReference(returnType);

                return new StructMemberModel
                {
                    TypeString = returnType,
                    Name = functionPointerInfo.Name,
                    Source = line.Trim(),
                    Offset = offset,
                    Alignment = alignment,
                    IsFunctionPointer = true,
                    FunctionSignature = functionSignature,
                    FunctionSignatureId = null, // Will be set when saving to database
                    TypeReference = typeReference,
                    TypeReferenceId = null, // Will be set when saving to database
                    LineNumber = lineNumber,
                    File = file
                };
            }
            else
            {
                // Extract bit field information before processing the declaration
                var (isBitField, bitFieldWidth) = ParsingUtilities.ExtractBitFieldInfo(cleanLine);

                // If it's a bit field, remove the bit field syntax for further parsing
                string declarationToParse = isBitField ? ParsingUtilities.RemoveBitFieldSyntax(cleanLine) : cleanLine;

                // Remove semicolon from the declaration to parse to ensure proper name extraction
                string declarationToParseWithoutSemicolon = declarationToParse.TrimEnd(';').Trim();

                // Create a regular member
                var rawTypeString = ParsingUtilities.ExtractTypeFromDeclaration(declarationToParse,
                    ParsingUtilities.ExtractNameFromDeclaration(declarationToParseWithoutSemicolon, file, lineNumber),
                    file, lineNumber);

                // Extract array information before processing the type
                var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo(declarationToParse);

                // Check if the type string ends with a pointer asterisk and extract it
                bool isPointer = false;
                string cleanTypeString = rawTypeString;

                if (rawTypeString.EndsWith("*"))
                {
                    // Remove trailing asterisks and spaces to determine if it's a pointer
                    int endPos = rawTypeString.Length - 1;
                    while (endPos >= 0 && (rawTypeString[endPos] == '*' || rawTypeString[endPos] == ' ' ||
                                           rawTypeString[endPos] == '\t'))
                    {
                        if (rawTypeString[endPos] == '*')
                        {
                            isPointer = true;
                        }

                        endPos--;
                    }

                    if (endPos >= 0)
                    {
                        cleanTypeString = rawTypeString.Substring(0, endPos + 1).Trim();
                    }
                    else
                    {
                        cleanTypeString = string.Empty;
                    }
                }

                // Create a TypeReference for the type information
                var typeReference = TypeResolver.CreateTypeReference(cleanTypeString);

                // Count the actual pointer depth from the raw type string
                int pointerDepth = 0;
                if (rawTypeString.EndsWith("*"))
                {
                    // Count the number of asterisks at the end of the type string
                    int pos = rawTypeString.Length - 1;
                    while (pos >= 0 && (rawTypeString[pos] == '*' || char.IsWhiteSpace(rawTypeString[pos])))
                    {
                        if (rawTypeString[pos] == '*')
                        {
                            pointerDepth++;
                        }

                        pos--;
                    }
                }

                // Update the TypeReference with the actual pointer information
                typeReference.IsPointer = isPointer;
                typeReference.PointerDepth = pointerDepth;

                // Update the TypeReference with array information
                typeReference.IsArray = isArray;
                typeReference.ArraySize = arraySize;

                return new StructMemberModel
                {
                    TypeString = ParsingUtilities.NormalizeTypeString(rawTypeString),
                    Name = ParsingUtilities.ExtractNameFromDeclaration(
                        declarationToParseWithoutSemicolon, file, lineNumber), // Use the version without semicolon
                    Source = line.Trim(),
                    Offset = offset,
                    Alignment = alignment,
                    BitFieldWidth = bitFieldWidth,
                    TypeReference = typeReference,
                    TypeReferenceId = null, // Will be set when saving to database
                    LineNumber = lineNumber,
                    File = file
                };
            }
        }
        else
        {
            string context = "";
            if (!string.IsNullOrEmpty(file)) context += $" in '{file}'";
            if (lineNumber.HasValue) context += $" at line {lineNumber}";
            Console.WriteLine($"Warning: Unable to parse line{context}: {line}");
        }

        return null;
    }

    /// <summary>
    /// Parses function parameters from a string (helper method to avoid direct dependency)
    /// </summary>
    private static List<FunctionParamModel> ParseFunctionParameters(string parametersString, string? file = null,
        int? lineNumber = null, string? source = null)
    {
        return FunctionParamParser.ParseFunctionParameters(parametersString, file, lineNumber, source);
    }

    /// <summary>
    /// Parses a function pointer member that returns a function pointer type.
    /// Example: HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))(IKeystoneWindow *, unsigned int, unsigned int, int)
    /// </summary>
    private static StructMemberModel? ParseNestedFunctionPointerMember(
        string cleanLine,
        (string? OuterReturnType, string? OuterCallingConvention, string? InnerCallingConvention, string? FunctionName,
            string? InnerParams, string? OuterParams) nestedInfo,
        int? offset,
        int? alignment,
        int? lineNumber = null,
        string? file = null)
    {
        if (nestedInfo.FunctionName == null || nestedInfo.OuterReturnType == null)
            return null;

        // Create the return function signature (the function type that this function pointer returns)
        var returnFunctionSignature = new FunctionSignatureModel
        {
            Name = $"__return_sig_{nestedInfo.FunctionName}",
            ReturnType = ParsingUtilities.NormalizeTypeString(nestedInfo.OuterReturnType),
            CallingConvention = nestedInfo.OuterCallingConvention ?? string.Empty,
            ReturnTypeReference = TypeResolver.CreateTypeReference(nestedInfo.OuterReturnType)
        };

        // Parse the parameters of the returned function type
        if (!string.IsNullOrEmpty(nestedInfo.OuterParams))
        {
            returnFunctionSignature.Parameters =
                FunctionParamParser.ParseFunctionSignatureParameters(nestedInfo.OuterParams, file, lineNumber,
                    cleanLine);
            returnFunctionSignature.Source = cleanLine.Trim();
            returnFunctionSignature.File = file;
            returnFunctionSignature.LineNumber = lineNumber;
        }

        returnFunctionSignature.FullyQualifiedName = ParsingUtilities.GenerateNormalizedFunctionSignature(
            returnFunctionSignature.ReturnType,
            returnFunctionSignature.CallingConvention,
            returnFunctionSignature.Name,
            returnFunctionSignature.Parameters);

        // The "return type" of the main function is the function pointer type string
        // We'll represent it as a special type string indicating it returns a function pointer
        string returnTypeString =
            $"{nestedInfo.OuterReturnType} ({nestedInfo.OuterCallingConvention ?? ""} *)({nestedInfo.OuterParams ?? ""})";
        var returnTypeReference = TypeResolver.CreateTypeReference(returnTypeString);

        // Create the main function signature for this nested function pointer
        var mainFunctionSignature = new FunctionSignatureModel
        {
            Name = $"__sig_{nestedInfo.FunctionName}",
            ReturnType = returnTypeString,
            CallingConvention = nestedInfo.InnerCallingConvention ?? string.Empty,
            ReturnTypeReference = returnTypeReference,
            Parameters = ParseFunctionParameters(nestedInfo.InnerParams ?? string.Empty, file, lineNumber, cleanLine),
            ReturnFunctionSignature = returnFunctionSignature,
            ReturnFunctionSignatureId = null, // Will be set when saving to database
            Source = cleanLine.Trim(),
            File = file,
            LineNumber = lineNumber
        };

        mainFunctionSignature.FullyQualifiedName = ParsingUtilities.GenerateNormalizedFunctionSignature(
            mainFunctionSignature.ReturnType,
            mainFunctionSignature.CallingConvention,
            mainFunctionSignature.Name,
            mainFunctionSignature.Parameters);

        // Create the main function pointer member
        return new StructMemberModel
        {
            TypeString = returnTypeString,
            Name = nestedInfo.FunctionName,
            Source = cleanLine.Trim(),
            Offset = offset,
            Alignment = alignment,
            IsFunctionPointer = true,
            FunctionSignature = mainFunctionSignature,
            FunctionSignatureId = null, // Will be set when saving to database
            TypeReference = returnTypeReference,
            TypeReferenceId = null,
            LineNumber = lineNumber,
            File = file
        };
    }
}
