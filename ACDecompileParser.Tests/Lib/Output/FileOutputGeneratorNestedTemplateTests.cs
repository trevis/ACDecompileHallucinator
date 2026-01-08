using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Output;

public class FileOutputGeneratorNestedTemplateTests
{
    [Fact]
    public void ParseAndGenerateHeaderFiles_WithNestedTypeInTemplate_CreatesSingleFile()
    {
        // Arrange - Test the exact case from the issue
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 10423 */",
                "struct __cppobj AC1Legacy::PQueueArray<double>",
                "{",
                "  AC1Legacy::PQueueArray<double>_vtbl *__vftable /*VFT*/;",
                "  AC1Legacy::PQueueArray<double>::PQueueNode *A;",
                "  int curNumNodes;",
                "  int allocatedNodes;",
                "  int minAllocatedNodes;",
                "};",
                "",
                "/* 10423 */",
                "struct __declspec(align(8)) AC1Legacy::PQueueArray<double>::PQueueNode",
                "{",
                "  long double key;",
                "  void *data;",
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
            Assert.Single(allFiles); // Should only be one file

            string headerFilePath = allFiles[0];
            Assert.Contains("PQueueArray.h", headerFilePath);

            // Verify the file contains both struct definitions
            // With nested type output, nested types are rendered inside their parent with short names
            string fileContent = File.ReadAllText(headerFilePath);
            Assert.Contains("AC1Legacy::PQueueArray", fileContent);
            // Nested type is rendered with short name inside parent
            Assert.Contains("struct PQueueNode", fileContent);
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
    public void Parse_WithNestedTypeInTemplate_SetsBaseTypePathCorrectly()
    {
        // Arrange - Test the exact case from the issue
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 10423 */",
                "struct __cppobj AC1Legacy::PQueueArray<double>",
                "{",
                "  AC1Legacy::PQueueArray<double>_vtbl *__vftable /*VFT*/;",
                "  AC1Legacy::PQueueArray<double>::PQueueNode *A;",
                "  int curNumNodes;",
                "  int allocatedNodes;",
                " int minAllocatedNodes;",
                "};",
                "",
                "/* 10423 */",
                "struct __declspec(align(8)) AC1Legacy::PQueueArray<double>::PQueueNode",
                "{",
                " long double key;",
                "  void *data;",
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
        Assert.Equal(2, allTypes.Count);

        // Find the PQueueArray<double> and PQueueArray<double>::PQueueNode types
        var pqueueArrayType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "AC1Legacy::PQueueArray<double>");
        var pqueueNode =
            allTypes.FirstOrDefault(t => t.FullyQualifiedName == "AC1Legacy::PQueueArray<double>::PQueueNode");

        Assert.NotNull(pqueueArrayType);
        Assert.NotNull(pqueueNode);

        // The nested type should have its BaseTypePath set to the parent type's fully qualified name
        Assert.Equal(pqueueArrayType.FullyQualifiedName, pqueueNode.BaseTypePath);

        // The parent type should have its own BaseTypePath (which should be its own FQN)
        Assert.Equal(pqueueArrayType.FullyQualifiedName, pqueueArrayType.BaseTypePath);
    }
}
