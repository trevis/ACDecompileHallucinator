using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public class StaticsParser
{
    private static readonly Regex LineRegex =
        new Regex(@"^([0-9A-F]{16})\s+(.+?)\s+\d+\s+bytes\s+\[(.+?)\]$", RegexOptions.Compiled);

    public static List<StaticVariableModel> ParseFile(string filePath)
    {
        var results = new List<StaticVariableModel>();
        if (!File.Exists(filePath)) return results;

        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            var model = ParseLine(line);
            if (model != null)
            {
                results.Add(model);
            }
        }

        return results;
    }

    public static StaticVariableModel? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        if (line.Contains("vftable")) return null;

        var match = LineRegex.Match(line.Trim());
        if (!match.Success) return null;

        string address = match.Groups[1].Value;
        string fullSignature = match.Groups[2].Value.Trim();

        // 1. Strip modifiers
        string cleanSignature = fullSignature;
        string[] modifiers = { "public:", "protected:", "private:", "static", "const", "near", "far" };
        foreach (var mod in modifiers)
        {
            string escapedMod = Regex.Escape(mod);
            cleanSignature = Regex.Replace(cleanSignature, $@"\b{escapedMod}\b", "", RegexOptions.IgnoreCase).Trim();
            // Also try without word boundaries if it has non-word chars
            if (mod.Contains(":"))
            {
                cleanSignature = Regex.Replace(cleanSignature, $@"{escapedMod}\s*", "", RegexOptions.IgnoreCase).Trim();
            }
        }

        // Remove double spaces that might have been created
        cleanSignature = Regex.Replace(cleanSignature, @"\s+", " ");

        // 2. Split type and name
        // The name is the last part. If it has a parent class, it's Parent::Name.
        // We look for the last whitespace that is NOT inside angle brackets.

        int splitIndex = -1;
        int angleDepth = 0;
        for (int i = cleanSignature.Length - 1; i >= 0; i--)
        {
            char c = cleanSignature[i];
            if (c == '>') angleDepth++;
            else if (c == '<') angleDepth--;
            else if (c == ' ' && angleDepth == 0)
            {
                splitIndex = i;
                break;
            }
        }

        string typeString;
        string name;

        if (splitIndex != -1)
        {
            typeString = ParsingUtilities.NormalizeTypeString(cleanSignature.Substring(0, splitIndex).Trim());
            name = cleanSignature.Substring(splitIndex + 1).Trim();
        }
        else
        {
            // No whitespace outside templates? Probably just a name and no explicit type
            typeString = "int"; // Fallback? Or leave empty? Prompt says "I just want to parse the types".
            name = cleanSignature;
        }

        return new StaticVariableModel
        {
            Address = address,
            Name = name,
            TypeString = typeString,
            GlobalType = fullSignature
        };
    }
}
