using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class SourceParserTimingTest
{
    [Fact]
    public void SaveToDatabase_ShouldNotProduceTimingWarnings()
    {
        // This test specifically verifies the fix for the timing issue
        // where types weren't saved before lookups, causing warnings
        
        // Capture console output to verify no warnings are produced
        var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);
        
        // Create content that would trigger the lookup timing issue
        var sourceContent = @"
/* 1234 */
struct TestStruct
{
    int field1;
    char* field2;
};
";
        
        var parser = new SourceParser(new List<string> { sourceContent });
        parser.Parse();
        
        // Create an in-memory database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid().ToString());
        
        using var context = new TypeContext(optionsBuilder.Options);
        context.Database.EnsureCreated();
        using var repo = new TypeRepository(context);
        
        // This should not produce any warnings due to the timing issue fix
        parser.SaveToDatabase(repo);
        
        // Check that no timing-related warnings were produced
        var output = consoleOutput.ToString();
        Assert.DoesNotContain("Warning: Could not find TypeModel for struct with FQN: TestStruct", output);
        
        // Verify the type was properly saved
        var savedType = repo.GetTypeByFullyQualifiedName("TestStruct");
        Assert.NotNull(savedType);
        Assert.Equal("TestStruct", savedType.BaseName);
    }
}
