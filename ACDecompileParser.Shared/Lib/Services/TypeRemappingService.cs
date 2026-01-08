using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Configuration;

namespace ACDecompileParser.Shared.Lib.Services;

public class TypeRemappingService
{
    private static readonly Lazy<List<KeyValuePair<string, string>>> _sortedMappings = new(() =>
    {
        // Sort by key length descending to ensure "unsigned int" is matched before "int"
        return TypeRemappingConfig.Mappings
            .OrderByDescending(x => x.Key.Length)
            .ToList();
    });

    private static readonly Lazy<Regex> _replacementRegex = new(() =>
    {
        var mappings = _sortedMappings.Value;
        // Create a pattern like \b(unsigned int|unsigned long|int)\b
        var pattern = string.Join("|", mappings.Select(m => Regex.Escape(m.Key)));
        return new Regex($@"\b({pattern})\b", RegexOptions.Compiled);
    });

    public string RemapTypeString(string typeString)
    {
        if (string.IsNullOrEmpty(typeString))
            return typeString;

        return _replacementRegex.Value.Replace(typeString, match =>
        {
            if (TypeRemappingConfig.Mappings.TryGetValue(match.Value, out var replacement))
            {
                return replacement;
            }
            return match.Value;
        });
    }
}
