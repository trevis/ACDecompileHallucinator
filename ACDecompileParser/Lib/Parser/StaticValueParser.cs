using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public static class StaticValueParser
{
    public static void ParseValues(List<StaticVariableModel> statics, List<List<string>> sourceFileContents,
        List<string> sourceFilePaths, IProgressReporter? reporter = null)
    {
        if (statics.Count == 0 || sourceFileContents.Count == 0) return;

        // Optimization: Build a lookup dictionary for faster matching
        // Map: LastWordOfName -> List<StaticVariableModel>
        // Example: "Render::s_rDegradeDistance" handles "s_rDegradeDistance"
        // This allows us to quickly identify relevant lines by checking the word before '='
        var staticsLookup = new Dictionary<string, List<StaticVariableModel>>();
        foreach (var s in statics)
        {
            // Extract the last identifier (Name or part after last ::)
            string key = s.Name;
            int lastColon = s.Name.LastIndexOf("::");
            if (lastColon != -1)
            {
                key = s.Name.Substring(lastColon + 2);
            }

            if (!staticsLookup.ContainsKey(key))
            {
                staticsLookup[key] = new List<StaticVariableModel>();
            }

            staticsLookup[key].Add(s);
        }

        int totalFiles = sourceFileContents.Count;
        reporter?.Start("Parsing static values", totalFiles);

        int filesProcessed = 0;
        int valuesFound = 0;

        // Iterate through all source code once
        for (int fileIdx = 0; fileIdx < totalFiles; fileIdx++)
        {
            var fileContent = sourceFileContents[fileIdx];

            for (int lineIdx = 0; lineIdx < fileContent.Count; lineIdx++)
            {
                string line = fileContent[lineIdx];
                if (line.Length > 0 && char.IsWhiteSpace(line[0])) continue; // Skip indented lines (scope assumption)

                int eqIndex = line.IndexOf('=');

                // Quick filter: must have '=' and be long enough to have a name
                if (eqIndex <= 0) continue;

                // Check if we have any candidates based on the word before '='
                // Get the potential identifier ending at eqIndex - 1
                string definitionLine = line.Substring(0, eqIndex).TrimEnd();
                if (string.IsNullOrEmpty(definitionLine)) continue;

                // Extract the last word (identifier) from definitionLine
                // We walk backwards from the end of definitionLine until we hit a non-identifier char
                int idEnd = definitionLine.Length - 1;
                int idStart = idEnd;

                while (idStart >= 0)
                {
                    char c = definitionLine[idStart];
                    // Allow alphanumeric and underscore. 
                    // Note: If we want to handle partial matches like "Parent::Name", we stop at ' ' or other separators.
                    // But our key is just "Name". 
                    // So we just find the last word.
                    if (!char.IsLetterOrDigit(c) && c != '_')
                    {
                        break;
                    }

                    idStart--;
                }

                idStart++; // Move back to first valid char

                if (idStart > idEnd) continue; // No identifier found

                string candidateKey = definitionLine.Substring(idStart, idEnd - idStart + 1);

                if (staticsLookup.TryGetValue(candidateKey, out var candidates))
                {
                    // Check each candidate to see if it matches the full qualified name context
                    foreach (var candidate in candidates)
                    {
                        if (candidate.Value != null) continue; // Already found

                        // Verify the full name is present in the line
                        // We search for "candidate.Name" ending at definitionLine end
                        // or simply: does definitionLine end with candidate.Name?
                        // candidate.Name could be "Parent::Name".
                        // definitionLine ends with "Name".
                        // We check if definitionLine ends with candidate.Name

                        // Handle potential whitespace differences?
                        // The original implementation used `line.IndexOf(searchingName)`.
                        // We should be at least as robust.
                        // Since we know the line contains '=' and we identified the word before it matches the suffix...

                        // Check if the line actually contains the full name
                        if (line.Contains(candidate.Name))
                        {
                            // Extract value
                            string afterEq = line.Substring(eqIndex + 1).TrimStart();
                            string extractedValue = ExtractValue(fileContent, lineIdx, afterEq);
                            candidate.Value = CleanValue(extractedValue);
                            valuesFound++;
                        }
                    }
                }
            }

            filesProcessed++;
            if (filesProcessed % 10 == 0 || filesProcessed == totalFiles)
            {
                reporter?.Report(filesProcessed, $"Found {valuesFound} values");
            }
        }

        reporter?.Finish($"Found values for {valuesFound} / {statics.Count} variables.");
    }

    private static string ExtractValue(List<string> fileLines, int startLineIndex, string startContext)
    {
        var sb = new System.Text.StringBuilder();

        // State Machine Variables
        int braces = 0;
        bool inDoubleQuote = false;
        bool inSingleQuote = false;
        bool inBlockComment = false;
        bool escaped = false; // For backslash escaping in quotes

        // Process the first line fragment (startContext)
        if (ProcessChunk(startContext, sb, ref braces, ref inDoubleQuote, ref inSingleQuote, ref inBlockComment,
                ref escaped))
        {
            return sb.ToString().Trim();
        }

        sb.Append(" "); // Safety space between lines

        // Process subsequent lines
        for (int i = startLineIndex + 1; i < fileLines.Count; i++)
        {
            string line = fileLines[i].Trim();

            // New line resets escaped state (assuming strings don't span lines with backslash in this parser context)
            escaped = false;

            if (ProcessChunk(line, sb, ref braces, ref inDoubleQuote, ref inSingleQuote, ref inBlockComment,
                    ref escaped))
            {
                return sb.ToString().Trim();
            }

            sb.Append(" ");
        }

        return sb.ToString().Trim();
    }

    private static bool ProcessChunk(string text, System.Text.StringBuilder sb,
        ref int braces, ref bool inDoubleQuote, ref bool inSingleQuote, ref bool inBlockComment, ref bool escaped)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char prev = i > 0 ? text[i - 1] : ' ';

            // 1. Handle Block Comments
            if (inBlockComment)
            {
                if (c == '*' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    inBlockComment = false;
                    sb.Append("*/");
                    i++; // Skip '/'
                }
                else
                {
                    sb.Append(c);
                }

                continue;
            }

            // 2. Handle Quotes (Double and Single)
            if (inDoubleQuote)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inDoubleQuote = false;
                }

                sb.Append(c);
                continue;
            }

            if (inSingleQuote)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '\'')
                {
                    inSingleQuote = false;
                }

                sb.Append(c);
                continue;
            }

            // 3. Not in comment or quote - Check for starts

            // Check for Block Comment Start
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                inBlockComment = true;
                sb.Append("/*");
                i++; // Skip '*'
                continue;
            }

            // Check for Line Comment
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                // Line comment consumes the rest of the line.
                // We stop processing this chunk.
                return false;
            }

            // Check for Double Quote Start
            if (c == '"')
            {
                inDoubleQuote = true;
                sb.Append(c);
                continue;
            }

            // Check for Single Quote Start
            if (c == '\'')
            {
                // Heuristic: If preceded by identifier char, treat as part of name (e.g. `vftable') and NOT quote start
                if (IsIdentifierChar(prev))
                {
                    sb.Append(c); // Just a char
                }
                else
                {
                    inSingleQuote = true;
                    sb.Append(c);
                }

                continue;
            }

            // 4. Structural Checks (Braces and Semicolon)
            if (c == '{')
            {
                braces++;
            }
            else if (c == '}')
            {
                braces--;
            }
            else if (c == ';' && braces == 0)
            {
                return true; // Found terminator!
            }

            sb.Append(c);
        }

        return false; // End of chunk, logic continues
    }

    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '`';
    }

    private static string CleanValue(string val)
    {
        // Replacements
        // 1. { &Position::`vftable' } -> { nullptr /* &Position::`vftable' */ }
        // Regex: &[\w:]+::`vftable'

        // Handle `vftable` references
        // Pattern: &TypeName::`vftable'
        // We want to replace it with: nullptr /* &TypeName::`vftable' */

        string processed =
            Regex.Replace(val, @"&([\w:]+)::`vftable'", match => { return $"nullptr /* {match.Value} */"; });

        // Handle `off_XXXXXX` references
        // Pattern: &off_([\da-fA-F]+)
        // Check if user request implied valid hex or any
        // Example: &off_79F0E8

        processed = Regex.Replace(processed, @"&off_([0-9a-fA-F]+)", match => { return $"nullptr /* {match.Value} */"; });

        // Normalize spaces
        // processed = Regex.Replace(processed, @"\s+", " ");

        // Note: We might want to keep newlines for formatting if it's a huge array?
        // For now user just said "store the values as strings".
        // If we collapse to one line, it might be extremely long.
        // But headers usually want it tidy.
        // Let's just trim around and leave internal structure if possible?
        // The ExtractValue logic uses StringBuilder and appends " " between lines.
        // So it naturally flattens.

        return processed.Trim();
    }
}
