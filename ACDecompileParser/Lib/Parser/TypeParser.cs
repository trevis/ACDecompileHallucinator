using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;
using System.Text;
using System.Text.RegularExpressions;

namespace ACDecompileParser.Lib.Parser;

public class TypeParser
{
    public static List<EnumTypeModel> ParseEnums(string source, string? file = null)
    {
        var lines = source.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        return ParseEnums(lines, file);
    }

    public static List<EnumTypeModel> ParseEnums(List<string> sourceLines, string? file = null)
    {
        var models = new List<EnumTypeModel>();

        for (int i = 0; i < sourceLines.Count; i++)
        {
            var line = sourceLines[i];
            var match = Regex.Match(line, @"\/\*\s*(\d+)\s*\*\/");
            if (match.Success)
            {
                line = sourceLines[++i];
                int lineNumber = i + 1;
                if (line.StartsWith("enum"))
                {
                    var model = new EnumTypeModel { LineNumber = lineNumber, File = file };
                    if (ParseEnumInternal(model, sourceLines, i))
                    {
                        models.Add(model);
                    }
                }
            }
        }

        return models;
    }

    private static bool ParseEnumInternal(EnumTypeModel model, List<string> lines, int i)
    {
        model.Source = CollectBodySource(lines, i);

        if (!model.Source.Contains("{"))
        {
            return false; // Forward declaration
        }

        EnumParser.ParseName(model, model.Source);

        // Validate that we successfully parsed an enum name
        if (string.IsNullOrEmpty(model.Name))
        {
            Console.WriteLine($"Warning: Failed to parse enum name from source starting at line {i + 1}:");
            // Show first few lines of the source for context
            var sourceLines = model.Source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < Math.Min(3, sourceLines.Length); j++)
            {
                Console.WriteLine($"  {sourceLines[j]}");
            }

            return false;
        }

        EnumParser.ParseMembers(model, model.Source, model.LineNumber, model.File);
        return true;
    }

    public static List<StructTypeModel> ParseStructs(string source, string? file = null)
    {
        var lines = source.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        return ParseStructs(lines, file);
    }

    public static List<StructTypeModel> ParseStructs(List<string> sourceLines, string? file = null)
    {
        var models = new List<StructTypeModel>();

        for (int i = 0; i < sourceLines.Count; i++)
        {
            var line = sourceLines[i];
            var match = Regex.Match(line, @"\/\*\s*(\d+)\s*\*\/");
            if (match.Success)
            {
                line = sourceLines[++i];
                int lineNumber = i + 1;
                // Check for struct definitions that may have modifiers like "const struct", "volatile struct", "struct __cppobj", etc.
                if (line.TrimStart().StartsWith("typedef "))
                {
                    continue;
                }

                if (ContainsStructKeyword(line))
                {
                    var model = new StructTypeModel { LineNumber = lineNumber, File = file };
                    if (ParseStructInternal(model, sourceLines, i))
                    {
                        models.Add(model);
                    }
                }
            }
        }

        return models;
    }

    private static bool ParseStructInternal(StructTypeModel model, List<string> lines, int i)
    {
        model.Source = CollectBodySource(lines, i);

        // Use StructParser to handle the parsing
        StructParser.ParseNameAndInheritance(model, model.Source);

        // Validate that we successfully parsed a struct name
        if (string.IsNullOrEmpty(model.Name))
        {
            Console.WriteLine($"Warning: Failed to parse struct name from source starting at line {i + 1}:");
            // Show first few lines of the source for context
            var sourceLines = model.Source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < Math.Min(3, sourceLines.Length); j++)
            {
                Console.WriteLine($"  {sourceLines[j]}");
            }

            return false;
        }

        // Parse members only if the struct has a body (not a forward declaration)
        // This prevents ParseMembers from finding braces from the next type definition
        if (model.Source.Contains('{'))
        {
            StructParser.ParseMembers(model, lines, i, model.File);
        }

        return true;
    }

    private static string CollectBodySource(List<string> lines, int i)
    {
        var line = lines[i];
        var source = new StringBuilder();

        if (line.Contains(';'))
        {
            source.AppendLine(line);
            return source.ToString();
        }

        int braceCount = 0;
        while (i < lines.Count)
        {
            line = lines[i];
            source.AppendLine(line);

            braceCount += line.Count(c => c == '{');
            braceCount -= line.Count(c => c == '}');

            if (braceCount <= 0 && line.Contains('}'))
            {
                break;
            }

            i++;
        }

        return source.ToString();
    }

    /// <summary>
    /// Eats the type from the definition line, returning the remaining line and outputting the type string
    /// </summary>
    public static string EatType(string defLine, out string typeStr)
    {
        if (string.IsNullOrWhiteSpace(defLine))
        {
            typeStr = string.Empty;
            return string.Empty;
        }

        int pos = 0;
        int length = defLine.Length;

        while (pos < length && char.IsWhiteSpace(defLine[pos]))
            pos++;

        int start = pos;

        if (pos + 6 <= length && defLine.Substring(pos, 6) == "const ")
            pos += 6;
        else if (pos + 9 <= length && defLine.Substring(pos, 9) == "volatile ")
            pos += 9;

        int angleBracketDepth = 0;

        while (pos < length)
        {
            char c = defLine[pos];

            if (c == '<')
            {
                angleBracketDepth++;
                pos++;
            }
            else if (c == '>')
            {
                angleBracketDepth--;
                pos++;
            }
            else if (angleBracketDepth > 0)
            {
                pos++;
            }
            else if (c == '*' || c == '&')
            {
                pos++;

                int peekPos = pos;
                while (peekPos < length && char.IsWhiteSpace(defLine[peekPos]))
                    peekPos++;

                if (peekPos < length && (defLine[peekPos] == '*' || defLine[peekPos] == '&'))
                {
                    pos = peekPos;
                }
                else
                {
                    break;
                }
            }
            else if (char.IsWhiteSpace(c))
            {
                int peekPos = pos;
                while (peekPos < length && char.IsWhiteSpace(defLine[peekPos]))
                    peekPos++;

                if (peekPos < length && (defLine[peekPos] == '*' || defLine[peekPos] == '&'))
                {
                    pos = peekPos;
                }
                else
                {
                    break;
                }
            }
            else if (c == ',')
            {
                // Stop parsing if we encounter a comma outside of angle brackets
                // This is important for parsing function parameters
                break;
            }
            else if (char.IsLetterOrDigit(c) || c == '_' || c == ':' || c == '$')
            {
                pos++;
            }
            else
            {
                break;
            }
        }

        typeStr = defLine.Substring(start, pos - start).TrimEnd();
        typeStr = Regex.Replace(typeStr, @"\s+", " ");
        typeStr = typeStr
            .Replace(" *", "*")
            .Replace(" >", ">");

        return defLine.Substring(pos).TrimStart();
    }

    /// <summary>
    /// Parses a type string and returns a ParsedTypeInfo object.
    /// Delegates to TypeParsingUtilities for the actual implementation.
    /// </summary>
    public static ParsedTypeInfo ParseType(string typeString) => TypeParsingUtilities.ParseType(typeString);

    /// <summary>
    /// Checks if the line contains the 'struct' keyword (with possible modifiers like 'const')
    /// </summary>
    private static bool ContainsStructKeyword(string line)
    {
        // Check if the line contains the word 'struct' as a whole word (not as part of another word)
        var pattern = @"\bstruct\b";
        return Regex.IsMatch(line, pattern);
    }

    public static List<UnionTypeModel> ParseUnions(string source, string? file = null)
    {
        var lines = source.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        return ParseUnions(lines, file);
    }

    public static List<UnionTypeModel> ParseUnions(List<string> sourceLines, string? file = null)
    {
        var models = new List<UnionTypeModel>();

        for (int i = 0; i < sourceLines.Count; i++)
        {
            var line = sourceLines[i];
            var match = Regex.Match(line, @"\/\*\s*(\d+)\s*\*\/");
            if (match.Success)
            {
                line = sourceLines[++i];
                int lineNumber = i + 1;
                // Check for union definitions that may have modifiers like "const union", "volatile union", etc.
                if (line.TrimStart().StartsWith("typedef "))
                {
                    continue;
                }

                if (ContainsUnionKeyword(line))
                {
                    var model = new UnionTypeModel { LineNumber = lineNumber, File = file };
                    if (ParseUnionInternal(model, sourceLines, i))
                    {
                        models.Add(model);
                    }
                }
            }
        }

        return models;
    }

    private static bool ParseUnionInternal(UnionTypeModel model, List<string> lines, int i)
    {
        model.Source = CollectBodySource(lines, i);

        // Use UnionParser to handle the parsing
        UnionParser.ParseNameAndInheritance(model, model.Source);

        // Validate that we successfully parsed a union name
        if (string.IsNullOrEmpty(model.Name))
        {
            Console.WriteLine($"Warning: Failed to parse union name from source starting at line {i + 1}:");
            // Show first few lines of the source for context
            var sourceLines = model.Source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < Math.Min(3, sourceLines.Length); j++)
            {
                Console.WriteLine($"  {sourceLines[j]}");
            }

            return false;
        }

        // Parse members only if the union has a body (not a forward declaration)
        // This prevents ParseMembers from finding braces from the next type definition
        if (model.Source.Contains('{'))
        {
            UnionParser.ParseMembers(model, lines, i, model.File);
        }

        return true;
    }

    /// <summary>
    /// Checks if the line contains the 'union' keyword (with possible modifiers like 'const')
    /// </summary>
    private static bool ContainsUnionKeyword(string line)
    {
        // Check if the line contains the word 'union' as a whole word (not as part of another word)
        var pattern = @"\bunion\b";
        return Regex.IsMatch(line, pattern);
    }
}
