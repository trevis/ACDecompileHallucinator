using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using System.Collections.Generic;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Output;

public class FileOutputGeneratorTemplateArgsTest
{
    [Fact]
    public void GenerateHeaderFiles_DoesNotIncludeTemplateArgsInFilename()
    {
        // Arrange
        var generator = new FileOutputGenerator();
        var typeModels = new List<TypeModel>
        {
            new TypeModel
            {
                BaseName = "PSRefBuffer",
                Namespace = "AC1Legacy",
                Type = TypeType.Struct,
                Source = @"/* 10423 */
struct __cppobj __declspec(align(4)) AC1Legacy::PSRefBuffer<char> : ReferenceCountTemplate<268435456,0>
{
  unsigned int m_len;
  unsigned int m_size;
  unsigned int m_hash;
  char m_data[1];
};",
                BaseTypePath = "AC1Legacy::PSRefBuffer" // Should not include template args
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
            
            string filePath = allFiles[0]!;
            string fileName = Path.GetFileName(filePath!);
            
            // The filename should be "PSRefBuffer.h", not "PSRefBuffer<char>.h"
            Assert.Equal("PSRefBuffer.h", fileName);
            
            // Verify file is in the correct directory
            string directoryName = Path.GetDirectoryName(filePath!)!;
            string expectedDirName = Path.Combine(tempDir!, "AC1Legacy");
            Assert.Equal(expectedDirName, directoryName);
            
            // Verify file contents are correct
            string fileContent = File.ReadAllText(filePath!);
            Assert.Contains("PSRefBuffer", fileContent);
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
    public void GenerateHeaderFiles_DoesNotIncludeTemplateArgsInFilenameForMultipleTemplateArgs()
    {
        // Arrange
        var generator = new FileOutputGenerator();
        var typeModels = new List<TypeModel>
        {
            new TypeModel
            {
                BaseName = "SmartArray",
                Namespace = "GameEngine",
                Type = TypeType.Struct,
                Source = @"struct GameEngine::SmartArray<int,5>
{
  int data[5];
};",
                BaseTypePath = "GameEngine::SmartArray" // Should not include template args
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
            
            string filePath = allFiles[0]!;
            string fileName = Path.GetFileName(filePath!);
            
            // The filename should be "SmartArray.h", not "SmartArray<int,5>.h"
            Assert.Equal("SmartArray.h", fileName);
            
            // Verify file is in the correct directory
            string directoryName = Path.GetDirectoryName(filePath!)!;
            string expectedDirName = Path.Combine(tempDir!, "GameEngine");
            Assert.Equal(expectedDirName, directoryName);
            
            // Verify file contents are correct
            string fileContent = File.ReadAllText(filePath!);
            Assert.Contains("SmartArray", fileContent);
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
    public void GenerateHeaderFiles_DoesNotIncludeTemplateArgsInFilenameForComplexTemplateArgs()
    {
        // Arrange
        var generator = new FileOutputGenerator();
        var typeModels = new List<TypeModel>
        {
            new TypeModel
            {
                BaseName = "HashTable",
                Namespace = "System",
                Type = TypeType.Struct,
                Source = @"struct System::HashTable<AsyncContext,AsyncCache::CCallbackHandler *>
{
  void* data;
};",
                BaseTypePath = "System::HashTable" // Should not include template args
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
            
            string filePath = allFiles[0]!;
            string fileName = Path.GetFileName(filePath!);
            
            // The filename should be "HashTable.h", not "HashTable<AsyncContext,AsyncCache.h" (which was the old bug)
            Assert.Equal("HashTable.h", fileName);
            
            // Verify file is in the correct directory
            string directoryName = Path.GetDirectoryName(filePath!)!;
            string expectedDirName = Path.Combine(tempDir!, "System");
            Assert.Equal(expectedDirName, directoryName);
            
            // Verify file contents are correct
            string fileContent = File.ReadAllText(filePath!);
            Assert.Contains("HashTable", fileContent);
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
