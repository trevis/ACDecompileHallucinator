using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Tests.Lib.Parser;

public class VolatileStructParsingTests
{
    [Fact]
    public void ParseVolatileStruct_DetectsIsVolatilePropertyCorrectly()
    {
        // Arrange - Test the exact example from the issue
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 10423 */",
                "volatile struct TimeSource_QueryPerformanceCounter::StateData",
                "{",
                "  long double tLastTime;",
                "  unsigned int dwFlags;",
                "  long double tReference;",
                "  unsigned int dwReferenceTGT;",
                "  unsigned __int64 qwReferenceQPC;",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new TypeRepository(context);

        // Act
        parser.Parse();
        parser.SaveToDatabase(repo);

        // Assert
        var allTypes = repo.GetAllTypes();
        Assert.Single(allTypes); // 1 type: TimeSource_QueryPerformanceCounter::StateData
        
        // Find the StateData type
        var stateDataType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "TimeSource_QueryPerformanceCounter::StateData");

        Assert.NotNull(stateDataType);
        Assert.Equal("StateData", stateDataType.BaseName);
        Assert.Equal("TimeSource_QueryPerformanceCounter", stateDataType.Namespace);
        Assert.Equal(TypeType.Struct, stateDataType.Type);
        Assert.True(stateDataType.IsVolatile); // Verify the volatile property is set
    }
    
    [Fact]
    public void ParseNonVolatileStruct_DoesNotSetIsVolatileProperty()
    {
        // Arrange - Test a regular struct without volatile
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 123 */",
                "struct __cppobj RegularStruct",
                "{",
                "  int value;",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new TypeRepository(context);

        // Act
        parser.Parse();
        parser.SaveToDatabase(repo);

        // Assert
        var allTypes = repo.GetAllTypes();
        Assert.Single(allTypes);

        var regularStructType = allTypes.First();
        Assert.Equal("RegularStruct", regularStructType.FullyQualifiedName);
        Assert.False(regularStructType.IsVolatile); // Verify the volatile property is not set
    }
    
    [Fact]
    public void ParseVolatileStruct_WithNamespace_GeneratesCorrectHeaderFile()
    {
        // Arrange - Test the volatile struct example with namespace
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 10423 */",
                "volatile struct TimeSource_QueryPerformanceCounter::StateData",
                "{",
                "  long double tLastTime;",
                "  unsigned int dwFlags;",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new TypeRepository(context);

        // Create a temporary directory for header output
        string tempDir = Path.Combine(Path.GetTempPath(), "test_output_" + Guid.NewGuid().ToString());
        
        try
        {
            // Act
            parser.Parse();
            parser.SaveToDatabase(repo);
            parser.GenerateHeaderFiles(tempDir, repo);

            // Assert
            var allFiles = Directory.GetFiles(tempDir, "*.h", SearchOption.AllDirectories);
            
            // Should only be one file: StateData.h
            Assert.Single(allFiles);
            
            string headerFilePath = allFiles[0];
            Assert.Contains("StateData.h", headerFilePath);
            
            // Verify the file contains the struct definition
            string fileContent = File.ReadAllText(headerFilePath);
            Assert.Contains("struct TimeSource_QueryPerformanceCounter::StateData", fileContent);
            Assert.Contains("tLastTime", fileContent);
            Assert.Contains("dwFlags", fileContent);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
    
    [Fact]
    public void ParseVolatileStruct_WithoutNamespace_DetectsCorrectly()
    {
        // Arrange - Test a volatile struct without namespace
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 456 */",
                "volatile struct SimpleVolatileStruct",
                "{",
                "  int x;",
                " float y;",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new TypeRepository(context);

        // Act
        parser.Parse();
        parser.SaveToDatabase(repo);

        // Assert
        var allTypes = repo.GetAllTypes();
        Assert.Single(allTypes);

        var volatileStructType = allTypes.First();
        Assert.Equal("SimpleVolatileStruct", volatileStructType.FullyQualifiedName);
        Assert.Equal("SimpleVolatileStruct", volatileStructType.BaseName);
        Assert.Equal("", volatileStructType.Namespace);
        Assert.Equal(TypeType.Struct, volatileStructType.Type);
        Assert.True(volatileStructType.IsVolatile); // Verify the volatile property is set
    }
    
    [Fact]
    public void ParseBothConstAndVolatileStruct_DistinguishesCorrectly()
    {
        // Arrange - Test that we can distinguish between const and volatile
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 750 */",
                "const struct ConstStruct",
                "{",
                "  int value1;",
                "};",
                "",
                "/* 751 */",
                "volatile struct VolatileStruct",
                "{",
                "  int value2;",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new TypeRepository(context);

        // Act
        parser.Parse();
        parser.SaveToDatabase(repo);

        // Assert
        var allTypes = repo.GetAllTypes();
        Assert.Equal(2, allTypes.Count); // 2 types: ConstStruct and VolatileStruct
        
        // Find the const struct
        var constStructType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "ConstStruct");
        Assert.NotNull(constStructType);
        Assert.False(constStructType.IsVolatile); // Const struct should not be marked as volatile

        // Find the volatile struct
        var volatileStructType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "VolatileStruct");
        Assert.NotNull(volatileStructType);
        Assert.True(volatileStructType.IsVolatile); // Volatile struct should be marked as volatile
    }
}
