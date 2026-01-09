using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Parser;

public class SourceParserNestedTypeTests
{
    [Fact]
    public void Parse_WithNestedType_SetsBaseTypePathCorrectly()
    {
        // Arrange
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 123 */",
                "struct __cppobj ActionState : IntrusiveHashData<unsigned long,ActionState *>",
                "{",
                "  long double m_timeActionBegan;",
                "  unsigned int m_cRepeatCount;",
                " unsigned int m_toggle;",
                "  IInputActionCallback *m_pcCallback;",
                "  SmartArray<ActionState::SingleKeyInfo,1> m_rgKeys;",
                "};",
                "/* 124 */",
                "struct __cppobj ActionState::SingleKeyInfo",
                "{",
                "  ControlSpecification key;",
                "  float extent;",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Act
        parser.Parse();
        parser.SaveToDatabase(repo);

        // Assert
        var allTypes = repo.GetAllTypes();
        Assert.Equal(2, allTypes.Count);

        // Find the ActionState and ActionState::SingleKeyInfo types
        var actionStateType = allTypes.FirstOrDefault(t => t.BaseName == "ActionState");
        var singleKeyInfoType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "ActionState::SingleKeyInfo");

        Assert.NotNull(actionStateType);
        Assert.NotNull(singleKeyInfoType);

        // The nested type should have its BaseTypePath set to the parent type's fully qualified name
        Assert.Equal(actionStateType.FullyQualifiedName, singleKeyInfoType.BaseTypePath);

        // The parent type should have its own BaseTypePath (which should be its own FQN)
        Assert.Equal(actionStateType.FullyQualifiedName, actionStateType.BaseTypePath);
    }

    [Fact]
    public void ParseAndGenerateHeaderFiles_WithNestedType_CreatesSingleFile()
    {
        // Arrange
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 123 */",
                "struct __cppobj ActionState : IntrusiveHashData<unsigned long,ActionState *>",
                "{",
                "  long double m_timeActionBegan;",
                " unsigned int m_cRepeatCount;",
                " unsigned int m_toggle;",
                "  IInputActionCallback *m_pcCallback;",
                "  SmartArray<ActionState::SingleKeyInfo,1> m_rgKeys;",
                "};",
                "/* 14 */",
                "struct __cppobj ActionState::SingleKeyInfo",
                "{",
                "  ControlSpecification key;",
                " float extent;",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

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
            Assert.Single(allFiles); // Should only be one file

            string headerFilePath = allFiles[0];
            Assert.Contains("ActionState.h", headerFilePath);

            // Verify the file contains both struct definitions
            // With nested type output, nested types are rendered inside their parent with short names
            string fileContent = File.ReadAllText(headerFilePath);
            Assert.Contains("struct ActionState", fileContent);
            // Nested type rendered with short name inside parent
            Assert.Contains("struct SingleKeyInfo", fileContent);
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
}
