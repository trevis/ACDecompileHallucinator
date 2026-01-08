using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public abstract class BaseParser
{
    /// <summary>
    /// Parses the name of the type from the source code
    /// </summary>
    public static void ParseName(BaseTypeModel model, string source, string keyword)
    {
        var defLine = ParsingUtilities.GetDefinitionLine(source, keyword);
        if (string.IsNullOrEmpty(defLine)) return;

        defLine = ParsingUtilities.CleanDefinitionLine(defLine, keyword);
        defLine = TypeParser.EatType(defLine, out var typeName);
        
        var parsedType = TypeParser.ParseType(typeName);
        model.Name = parsedType.BaseName;
        model.Namespace = parsedType.Namespace;
    }
}
