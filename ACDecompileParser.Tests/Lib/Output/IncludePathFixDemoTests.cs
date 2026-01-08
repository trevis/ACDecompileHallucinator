using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Lib.Parser;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Output;

public class IncludePathFixDemoTests
{
    [Fact]
    public void DemonstrateIncludePathFix()
    {
        // Create an instance of StructOutputGenerator to access the GetCleanBaseName method
        var generator = new StructOutputGenerator();
        
        // Test 1: VTable name
        string vtableName = "TestClass_vtbl";
        string cleanVtableName = generator.GetCleanBaseName(vtableName);
        Assert.Equal("TestClass", cleanVtableName);
        Console.WriteLine($"VTable test: '{vtableName}' -> '{cleanVtableName}'");

        // Test 2: Template with arguments
        string templateName = "AutoGrowHashTable<AsyncContext,AsyncCache::CCallbackHandler *>";
        string cleanTemplateName = generator.GetCleanBaseName(templateName);
        Assert.Equal("AutoGrowHashTable", cleanTemplateName);
        Console.WriteLine($"Template test: '{templateName}' -> '{cleanTemplateName}'");

        // Test 3: Template with namespace
        string templateWithNamespace = "NS::SomeTemplate<int, float>";
        string cleanTemplateWithNamespace = generator.GetCleanBaseName(templateWithNamespace);
        Assert.Equal("SomeTemplate", cleanTemplateWithNamespace);
        Console.WriteLine($"Template with namespace test: '{templateWithNamespace}' -> '{cleanTemplateWithNamespace}'");

        // Test 4: Simple name (no changes expected)
        string simpleName = "SimpleClass";
        string cleanSimpleName = generator.GetCleanBaseName(simpleName);
        Assert.Equal("SimpleClass", cleanSimpleName);
        Console.WriteLine($"Simple name test: '{simpleName}' -> '{cleanSimpleName}'");

        // Test 5: VTable with template (edge case)
        string vtableWithTemplate = "SomeClass_vtbl<int>";
        string cleanVtableWithTemplate = generator.GetCleanBaseName(vtableWithTemplate);
        Assert.Equal("SomeClass", cleanVtableWithTemplate);
        Console.WriteLine($"VTable with template test: '{vtableWithTemplate}' -> '{cleanVtableWithTemplate}'");
        
        // This demonstrates that the fix correctly handles both vtable suffixes and template arguments
        Console.WriteLine("All tests passed! The include path fix is working correctly.");
    }
}
