using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public class EnumParser
{
    public static void ParseName(EnumTypeModel enumModel, string source)
    {
        var defLine = ParsingUtilities.GetDefinitionLine(source, "enum");
        if (string.IsNullOrEmpty(defLine))
        {
            Console.WriteLine($"Warning: Could not find enum definition line in source:");
            // Show first few lines for context
            var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < Math.Min(3, lines.Length); i++)
            {
                Console.WriteLine($"  {lines[i]}");
            }

            return;
        }

        defLine = ParsingUtilities.CleanDefinitionLine(defLine, "enum");

        // Check for __bitmask keyword
        if (defLine.Contains("__bitmask"))
        {
            enumModel.IsBitmask = true;
            defLine = defLine.Replace("__bitmask", "").Trim();
        }

        defLine = TypeParser.EatType(defLine, out var typeName);

        var parsedType = TypeParser.ParseType(typeName);
        enumModel.Name = parsedType.BaseName;
        enumModel.Namespace = parsedType.Namespace;
    }

    public static void ParseMembers(EnumTypeModel enumModel, string source, int? startLine = null, string? file = null)
    {
        // Extract content between curly braces
        int start = source.IndexOf('{');
        int end = source.LastIndexOf('}');

        if (start != -1 && end != -1 && end > start)
        {
            string body = source.Substring(start + 1, end - start - 1);

            // Track line numbers within the enum body
            string beforeBody = source.Substring(0, start + 1);
            int bodyStartLine = (startLine ?? 0) + beforeBody.Count(c => c == '\n');

            // Split by commas, but be careful about nested structures
            var memberStrings = SplitEnumMembers(body);

            int currentOffset = 0;
            foreach (var memberStr in memberStrings)
            {
                var trimmedMember = memberStr.Trim();
                if (string.IsNullOrEmpty(trimmedMember))
                {
                    currentOffset += memberStr.Length + 1; // +1 for the comma
                    continue;
                }

                // Calculate line number for this member
                string beforeMember = body.Substring(0, currentOffset);
                int memberLine = bodyStartLine + beforeMember.Count(c => c == '\n');

                // Parse name and value (if present)
                var member = ParseEnumMember(trimmedMember, memberLine, file);
                if (member != null)
                {
                    enumModel.Members.Add(member);
                }
                else
                {
                    // Log warning when enum member parsing fails
                    Console.WriteLine(
                        $"Warning: Failed to parse enum member in '{enumModel.FullyQualifiedName}': {trimmedMember}");
                }

                currentOffset += memberStr.Length + 1; // +1 for the comma
            }
        }
    }

    private static List<string> SplitEnumMembers(string body)
    {
        var members = new List<string>();
        int braceDepth = 0;
        int start = 0;

        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];

            if (c == '{')
            {
                braceDepth++;
            }
            else if (c == '}')
            {
                braceDepth--;
            }
            else if (c == ',' && braceDepth == 0)
            {
                members.Add(body.Substring(start, i - start));
                start = i + 1;
            }
        }

        // Add the last member
        if (start < body.Length)
        {
            members.Add(body.Substring(start));
        }

        return members;
    }

    private static EnumMemberModel? ParseEnumMember(string memberStr, int? lineNumber = null, string? file = null)
    {
        // Match patterns like: NAME = VALUE or just NAME
        var regex = new Regex(@"^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*(?:=\s*(.*?))?\s*$");
        var match = regex.Match(memberStr);

        if (match.Success)
        {
            var member = new EnumMemberModel
            {
                Name = match.Groups[1].Value,
                Source = memberStr.Trim(),
                Value = match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty,
                LineNumber = lineNumber,
                File = file
            };

            return member;
        }

        return null;
    }
}
