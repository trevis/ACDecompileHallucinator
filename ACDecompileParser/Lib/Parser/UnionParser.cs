using ACDecompileParser.Shared.Lib.Models;
using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public class UnionParser
{
    public static void ParseNameAndInheritance(UnionTypeModel unionModel, string source)
    {
        // Find the line that contains the union keyword, since it might not start with "union"
        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string defLine = "";

        foreach (var line in lines)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\bunion\b"))
            {
                defLine = line;
                break;
            }
        }

        if (string.IsNullOrEmpty(defLine))
        {
            Console.WriteLine($"Warning: Could not find union definition line in source:");
            // Show first few lines for context
            for (int i = 0; i < Math.Min(3, lines.Length); i++)
            {
                Console.WriteLine($"  {lines[i]}");
            }

            return;
        }

        // Check if the original definition line starts with 'const union' or 'volatile union' before cleaning
        string originalDefLine = defLine.Trim();
        // Handle cases like "const union __cppobj TypeName", "volatile union TypeName", etc.
        if (originalDefLine.StartsWith("const union"))
        {
            // Remove the "const " prefix for further processing
            defLine = originalDefLine.Substring(6).Trim(); // Remove "const "
        }
        else if (originalDefLine.StartsWith("volatile union"))
        {
            // Remove the "volatile " prefix for further processing
            defLine = originalDefLine.Substring(9).Trim(); // Remove "volatile "
            unionModel.IsVolatile = true; // Mark the union as volatile
        }

        defLine = ParsingUtilities.CleanDefinitionLine(defLine, "union");

        defLine = TypeParser.EatType(defLine, out var typeName);

        var parsedType = TypeParser.ParseType(typeName);
        unionModel.Name = parsedType.BaseName;
        unionModel.Namespace = parsedType.Namespace;

        // Convert parsed template arguments to TypeReference list
        var templateArgs = new List<TypeReference>();
        for (int i = 0; i < parsedType.TemplateArguments.Count; i++)
        {
            var templateArg = parsedType.TemplateArguments[i];
            // Create a TypeReference from the parsed template argument
            var typeReference = new TypeReference
            {
                TypeString = templateArg.FullTypeString,
                IsConst = templateArg.IsConst,
                IsPointer = templateArg.IsPointer,
                IsReference = templateArg.IsReference,
                PointerDepth = templateArg.PointerDepth
            };
            templateArgs.Add(typeReference);
        }

        unionModel.TemplateArguments.AddRange(templateArgs);

        var baseTypes = ParseInheritance(ref defLine);
        // Convert base types to the appropriate format
        foreach (var baseType in baseTypes)
        {
            var parsedBaseType = TypeParser.ParseType(baseType);
            unionModel.BaseTypes.Add(parsedBaseType.BaseName);
        }
    }

    public static void ParseMembers(UnionTypeModel unionModel, List<string> lines, int unionStartIndex,
        string? file = null)
    {
        // Find the opening brace of the union
        int braceStart = -1;
        for (int i = unionStartIndex; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Contains('{'))
            {
                braceStart = i;
                break;
            }
        }

        if (braceStart == -1) return;

        // Find the closing brace
        int braceEnd = -1;
        int braceCount = 0;
        for (int i = braceStart; i < lines.Count; i++)
        {
            var line = lines[i];
            braceCount += line.Count(c => c == '{');
            braceCount -= line.Count(c => c == '}');

            if (braceCount <= 0 && line.Contains('}'))
            {
                braceEnd = i;
                break;
            }
        }

        if (braceEnd == -1) return;

        // Parse members between the braces
        // Track member name counts for detecting overloads
        var memberNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int declarationOrder = 0; // Track the declaration order

        for (int i = braceStart + 1; i < braceEnd; i++)
        {
            var line = lines[i].Trim();

            // Skip empty lines and closing brace
            if (string.IsNullOrEmpty(line) || line.Contains('}'))
                continue;

            // Skip pure comment lines (but not lines with member declarations that have comments)
            if (line.StartsWith("//"))
                continue;

            // Skip lines that are pure comments (start with /* and end with */)
            if (line.StartsWith("/*") && line.EndsWith("*/") && !line.Contains(';'))
                continue;

            // Look for member declarations
            int? memberLineNumber = unionModel.LineNumber.HasValue
                ? unionModel.LineNumber.Value + (i - unionStartIndex)
                : (int?)null;

            var member = ParseMember(line, memberLineNumber, file);
            if (member != null)
            {
                // Validate the parsed member data
                if (string.IsNullOrEmpty(member.Name))
                {
                    Console.WriteLine(
                        $"Warning: Union member has empty name in '{unionModel.FullyQualifiedName}': {line}");
                }
                // Destructor names can contain spaces due to template arguments, so skip space check for them
                else if (!member.Name.StartsWith("~") && member.Name.Contains(' '))
                {
                    Console.WriteLine(
                        $"Warning: Union member name contains spaces in '{unionModel.FullyQualifiedName}': '{member.Name}' from: {line}");
                }

                if (string.IsNullOrWhiteSpace(member.TypeString))
                {
                    Console.WriteLine(
                        $"Warning: Union member has empty type in '{unionModel.FullyQualifiedName}': {line}");
                }

                // Track overloads - if we've seen this name before, increment the overload index
                if (memberNameCounts.TryGetValue(member.Name, out int count))
                {
                    member.OverloadIndex = count;
                    memberNameCounts[member.Name] = count + 1;
                }
                else
                {
                    member.OverloadIndex = 0;
                    memberNameCounts[member.Name] = 1;
                }

                // Assign the declaration order to preserve the original sequence
                member.DeclarationOrder = declarationOrder++;

                unionModel.Members.Add(member);
            }
            else
            {
                // Log warning when member parsing fails
                Console.WriteLine(
                    $"Warning: Failed to parse union member in '{unionModel.FullyQualifiedName}': {line}");
            }
        }
    }

    private static StructMemberModel? ParseMember(string line, int? lineNumber = null, string? file = null)
    {
        return MemberParser.ParseMemberDeclaration(line, lineNumber, file);
    }

    private static List<string> ParseInheritance(ref string defLine)
    {
        var parents = new List<string>();

        if (!defLine.StartsWith(":"))
            return parents;

        defLine = defLine.Substring(1).Trim();

        while (defLine.Length > 0)
        {
            defLine = TypeParser.EatType(defLine, out var baseTypeStr);
            parents.Add(baseTypeStr);

            if (defLine.StartsWith(","))
            {
                defLine = defLine.Substring(1).Trim();
            }
            else
            {
                break;
            }
        }

        return parents;
    }
}
