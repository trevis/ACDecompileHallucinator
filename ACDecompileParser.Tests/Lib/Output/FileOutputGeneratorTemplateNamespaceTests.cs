using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using System.Collections.Generic;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Output;

public class FileOutputGeneratorTemplateNamespaceTests
{
    [Fact]
    public void GenerateHeaderFiles_CreatesCorrectFilenameForTemplateWithNamespaceInArguments()
    {
        // Arrange
        var generator = new FileOutputGenerator();
        var typeModels = new List<TypeModel>
        {
            new TypeModel
            {
                BaseName = "AutoGrowHashTable", // This should be the correct BaseName
                Namespace = "",
                Type = TypeType.Struct,
                Source = "/* 123 */\nstruct __cppobj AutoGrowHashTable<AsyncContext,AsyncCache::CCallbackHandler *> : HashTable<AsyncContext,AsyncCache::CCallbackHandler *,1>\n{\n};",
                TemplateArguments = new List<TypeTemplateArgument>
                {
                    new TypeTemplateArgument { Position = 0, TypeString = "AsyncContext" },
                    new TypeTemplateArgument { Position = 1, TypeString = "AsyncCache::CCallbackHandler*" }
                }
            }
        };

        // Create a temporary directory for testing
        string tempDir = Path.Combine(Path.GetTempPath(), "test_output_" + System.Guid.NewGuid().ToString());
        
        try
        {
            // Act
            generator.GenerateHeaderFiles(typeModels, tempDir);

            // Assert - Check what files were actually created
            var allFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
            
            // There should be exactly one file
            Assert.Single(allFiles);
            
            string filePath = allFiles[0];
            string fileName = Path.GetFileName(filePath);
            
            // The filename should be "AutoGrowHashTable.h", not "AutoGrowHashTable<AsyncContext,AsyncCache.h"
            Assert.Equal("AutoGrowHashTable.h", fileName);
            
            // Verify file contents are correct
            string fileContent = File.ReadAllText(filePath);
            Assert.Contains("AutoGrowHashTable", fileContent);
            Assert.Contains("AsyncContext", fileContent);
            Assert.Contains("AsyncCache", fileContent);
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
