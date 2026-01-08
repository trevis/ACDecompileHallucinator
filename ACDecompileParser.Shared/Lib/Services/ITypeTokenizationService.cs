using ACDecompileParser.Shared.Lib.Output.Models;

namespace ACDecompileParser.Shared.Lib.Services;

public interface ITypeTokenizationService
{
    void SetLookupCache(TypeLookupCache cache);
    IEnumerable<CodeToken> TokenizeTypeString(string typeString, int? primaryReferencedTypeId = null, string? contextNamespace = null);
    string GetCleanBaseName(string typeString);
}
