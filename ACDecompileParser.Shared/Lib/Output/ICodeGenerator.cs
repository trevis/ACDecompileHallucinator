using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;

namespace ACDecompileParser.Shared.Lib.Output;

public interface ICodeGenerator
{
    IEnumerable<CodeToken> Generate(TypeModel type);
    List<string> GetDependencies(TypeModel type);
}
