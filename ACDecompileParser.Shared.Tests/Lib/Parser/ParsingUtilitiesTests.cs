using ACDecompileParser.Shared.Lib.Utilities;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Parser;

public class ParsingUtilitiesTests
{
    [Fact]
    public void ExtractBaseTypeName_ExtractsTemplateArguments()
    {
        // Test extracting base name from template arguments
        string result = ParsingUtilities.ExtractBaseTypeName("AutoGrowHashTable<AsyncContext,AsyncCache::CCallbackHandler *>");
        Assert.Equal("AutoGrowHashTable", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_ExtractsNamespace()
    {
        // Test extracting base name from namespace-qualified type
        string result = ParsingUtilities.ExtractBaseTypeName("NS::SomeTemplate");
        Assert.Equal("SomeTemplate", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_ExtractsNamespaceFromTemplate()
    {
        // Test extracting base name from namespace-qualified template
        string result = ParsingUtilities.ExtractBaseTypeName("NS::SomeTemplate<int, float>");
        Assert.Equal("SomeTemplate", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_HandlesSimpleName()
    {
        // Test with simple name (no changes expected)
        string result = ParsingUtilities.ExtractBaseTypeName("SimpleClass");
        Assert.Equal("SimpleClass", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesConstModifier()
    {
        // Test removing const modifier
        string result = ParsingUtilities.ExtractBaseTypeName("const SomeType");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesPointerModifier()
    {
        // Test removing pointer modifier
        string result = ParsingUtilities.ExtractBaseTypeName("SomeType*");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesReferenceModifier()
    {
        // Test removing reference modifier
        string result = ParsingUtilities.ExtractBaseTypeName("SomeType&");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesMultiplePointers()
    {
        // Test removing multiple pointer modifiers
        string result = ParsingUtilities.ExtractBaseTypeName("SomeType**");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesPointerAndTemplate()
    {
        // Test removing pointer and extracting template base name
        string result = ParsingUtilities.ExtractBaseTypeName("SomeType<int>*");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesConstAndTemplate()
    {
        // Test removing const and extracting template base name
        string result = ParsingUtilities.ExtractBaseTypeName("const SomeType<int>");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesConstNamespaceAndTemplate()
    {
        // Test removing const, namespace and extracting template base name
        string result = ParsingUtilities.ExtractBaseTypeName("const NS::SomeType<int>");
        Assert.Equal("SomeType", result);
    }
}
