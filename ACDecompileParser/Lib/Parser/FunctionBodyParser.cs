using System.Text;
using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public class FunctionBodyParser
{
    private static readonly Regex FunctionStartRegex =
        new Regex(@"^//-+ \([0-9A-F]+\) -+",
            RegexOptions.Compiled);

    private static readonly Regex FunctionOffsetRegex =
        new Regex(@"^//-+ \(([0-9A-F]+)\) -+",
            RegexOptions.Compiled);


    public static List<FunctionBodyModel> Parse(List<string> lines, string? file = null)
    {
        var models = new List<FunctionBodyModel>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (FunctionStartRegex.IsMatch(line))
            {
                // Found a function start - extract offset from header
                int startLineIndex = i;
                long? functionOffset = null;
                var offsetMatch = FunctionOffsetRegex.Match(line);
                if (offsetMatch.Success && offsetMatch.Groups.Count > 1)
                {
                    if (long.TryParse(offsetMatch.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null,
                            out long parsedOffset))
                    {
                        functionOffset = parsedOffset;
                    }
                }

                // Find next non-empty line for signature
                int sigLineIndex = i + 1;
                while (sigLineIndex < lines.Count && string.IsNullOrWhiteSpace(lines[sigLineIndex]))
                {
                    sigLineIndex++;
                }

                if (sigLineIndex >= lines.Count) break;

                string firstSigLine = lines[sigLineIndex].Trim();

                // Skip functions that start with # (e.g. #error) or are deleting destructors
                if (firstSigLine.StartsWith("#") || firstSigLine.Contains("deleting destructor"))
                {
                    i = sigLineIndex;
                    continue;
                }

                // Collect multi-line signature and build body text
                var sigBuilder = new StringBuilder();
                var bodyBuilder = new StringBuilder();
                int currentLineIndex = sigLineIndex;
                int braceDepth = 0;
                bool foundBodyStart = false;
                int bodyEndIndex = -1;

                while (currentLineIndex < lines.Count)
                {
                    string currentLine = lines[currentLineIndex];
                    bodyBuilder.AppendLine(currentLine);

                    if (!foundBodyStart)
                    {
                        if (currentLine.Contains("{"))
                        {
                            int braceIndex = currentLine.IndexOf('{');
                            var linePart = currentLine.Substring(0, braceIndex);
                            var stripped = ParsingUtilities.StripComments(linePart).Trim();
                            if (!string.IsNullOrEmpty(stripped))
                            {
                                sigBuilder.Append(stripped).Append(" ");
                            }

                            foundBodyStart = true;
                        }
                        else
                        {
                            var stripped = ParsingUtilities.StripComments(currentLine).Trim();
                            if (!string.IsNullOrEmpty(stripped))
                            {
                                sigBuilder.Append(stripped).Append(" ");
                            }
                        }
                    }

                    // Count braces to find end
                    foreach (char c in currentLine)
                    {
                        if (c == '{')
                        {
                            braceDepth++;
                        }
                        else if (c == '}')
                        {
                            if (braceDepth > 0)
                            {
                                braceDepth--;
                            }
                        }
                    }

                    if (foundBodyStart && braceDepth == 0)
                    {
                        bodyEndIndex = currentLineIndex;
                        break;
                    }

                    currentLineIndex++;
                }

                if (bodyEndIndex != -1)
                {
                    string fullSignature = Regex.Replace(sigBuilder.ToString().Trim(), @"\s+", " ");
                    var rawBody = bodyBuilder.ToString();
                    var model = ParseFunctionModel(fullSignature, rawBody, file, startLineIndex + 1, functionOffset);
                    if (model != null)
                    {
                        models.Add(model);
                    }

                    i = bodyEndIndex; // Advance main loop
                }
            }
        }

        return models;
    }

    private static FunctionBodyModel? ParseFunctionModel(string signatureLine, string bodyText, string? file,
        int lineNumber, long? offset)
    {
        // Normalize signature line removing trailing { if present
        string cleanSig = signatureLine.Trim().TrimEnd('{').Trim();

        // Extract parameters: find last (...)
        // Use ParsingUtilities.FindMatchingOpenParen to handle nested parentheses in return types/names incorrectly
        int lastParenClose = cleanSig.LastIndexOf(')');
        if (lastParenClose == -1) return null;

        int lastParenOpen = ParsingUtilities.FindMatchingOpenParen(cleanSig, lastParenClose);

        if (lastParenOpen == -1)
        {
            return null;
        }

        // To handle function pointers in return types or name, checking simply LastIndexOf('(') might be risky.
        // But for top-level definitions, the parameters are usually the LAST thing on the line.

        string paramString = cleanSig.Substring(lastParenOpen + 1, lastParenClose - lastParenOpen - 1);
        string preParams = cleanSig.Substring(0, lastParenOpen).Trim();

        // extract name from preParams
        // preParams = "int __thiscall ClientObjMaintSystem::Handle_Qualities__PrivateUpdateString"
        // preParams = "int _E1281_0"

        var (beforeName, name) = ExtractRightmostIdentifier(preParams);

        // beforeName is return type + calling convention
        // Extract calling convention if present
        string callingConvention = "";
        string returnType = beforeName.Trim();

        if (returnType.EndsWith("__thiscall"))
        {
            callingConvention = "__thiscall";
            returnType = returnType.Replace("__thiscall", "").Trim();
        }
        else if (returnType.EndsWith("__cdecl"))
        {
            callingConvention = "__cdecl";
            returnType = returnType.Replace("__cdecl", "").Trim();
        }
        else if (returnType.EndsWith("__stdcall"))
        {
            callingConvention = "__stdcall";
            returnType = returnType.Replace("__stdcall", "").Trim();
        }
        else if (returnType.EndsWith("__fastcall"))
        {
            callingConvention = "__fastcall";
            returnType = returnType.Replace("__fastcall", "").Trim();
        }

        var model = new FunctionBodyModel
        {
            FullyQualifiedName = name,
            BodyText = bodyText,
            File = file,
            LineNumber = lineNumber,
            Source = signatureLine,
            Offset = offset
        };

        if (name.Contains("::"))
        {
            // Parent resolution happens in SourceParser or post-processing
        }

        // Build FunctionSignatureModel
        var signatureModel = new FunctionSignatureModel
        {
            Name = name,
            ReturnType = returnType,
            CallingConvention = callingConvention,
            Source = signatureLine,
            File = file,
            LineNumber = lineNumber,
            // Create TypeReference for return type
            ReturnTypeReference = TypeResolver.CreateTypeReference(returnType)
        };

        signatureModel.Parameters =
            FunctionParamParser.ParseFunctionParameters(paramString, file, lineNumber, signatureLine);

        // Generate normalized fully qualified name
        string fullyQualifiedName = ParsingUtilities.GenerateNormalizedFunctionSignature(
            returnType, callingConvention, name, signatureModel.Parameters);

        signatureModel.FullyQualifiedName = fullyQualifiedName;
        // FunctionBodyModel.FullyQualifiedName should be the scoped name (e.g. "Render::SetObjectScale")
        // The normalized signature (e.g. "void __cdecl Render::SetObjectScale(const Vector3 *)") is stored in signatureModel.FullyQualifiedName
        // FunctionBodyModel.FullyQualifiedName should be the scoped name (e.g. "Render::SetObjectScale")
        // The normalized signature (e.g. "void __cdecl Render::SetObjectScale(const Vector3 *)") is stored in signatureModel.FullyQualifiedName
        // model.FullyQualifiedName = fullyQualifiedName; // REMOVED: Keep original scoped name for ParentId resolution

        model.FunctionSignature = signatureModel;

        return model;
    }

    private static (string Before, string Identifier) ExtractRightmostIdentifier(string text)
    {
        // Logic similar to FunctionParamParser.ExtractRightmostIdentifier
        // Search backwards for the first identifier character

        int endPos = text.Length - 1;
        while (endPos >= 0 && char.IsWhiteSpace(text[endPos])) endPos--;

        if (endPos < 0) return (text, "");

        // If it's a pointer/ref, skip them? 
        // No, the function name typically isn't a pointer itself, 
        // e.g. int * func() -> return type "int *", name "func".

        // Scan backwards for identifier
        int startPos = endPos;
        while (startPos >= 0)
        {
            char c = text[startPos];
            if (char.IsLetterOrDigit(c) || c == '_' || c == ':' || c == '~' || c == '<' ||
                c == '>') // Allow templates in name? e.g. operator<
            {
                startPos--;
            }
            else
            {
                break;
            }
        }

        // Note: The simple scan above might be too aggressive with < > if they are part of a template return type.
        // But usually return type is separated by space.
        // e.g. "std::vector<int> func()"
        // scan back from 'c': func -> space -> stop.

        // Corner case: "operator<<"
        // Corner case: "MyType::Func"

        // Let's refine: scan explicitly for valid C++ identifier chars including :: and ~ (destructor)
        // Ignoring template args in name for now as function names (unless it's a template specializtion).
        // BUT, what if the return type is "Something<A, B>"?
        // "Something<A, B> Name()"
        // If we scan back, we hit 'e' of Name, then space.

        startPos++; // Adjust to first char

        string identifier = text.Substring(startPos, endPos - startPos + 1);
        string before = text.Substring(0, startPos).Trim();

        return (before, identifier);
    }
}
