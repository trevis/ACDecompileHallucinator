using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using System.Collections.Generic;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Output;

public class FileOutputGeneratorNamespaceTests
{
    [Fact]
    public void GenerateHeaderFiles_HandlesNestedTypesInSeparateFiles()
    {
        // Arrange
        var generator = new FileOutputGenerator();
        var typeModels = new List<TypeModel>
        {
            // Simulate AC1Legacy::Vector3 scenario - nested types that should be in separate files
            // Each type has its own BaseName but shares the same namespace
            new TypeModel
            {
                BaseName = "Vector3",
                Namespace = "AC1Legacy",
                BaseTypePath = "Vector3",  // This indicates it should be in its own group (not grouped with AC1Legacy)
                Type = TypeType.Struct,
                Source = "struct AC1Legacy::Vector3 { float x, y, z; };"
            },
            new TypeModel
            {
                BaseName = "SmartArray",
                Namespace = "AC1Legacy", 
                BaseTypePath = "SmartArray",  // This indicates it should be in its own group
                Type = TypeType.Struct,
                Source = "struct AC1Legacy::SmartArray { int* data; };"
            }
        };

        // Create a temporary directory for testing
        string tempDir = Path.Combine(Path.GetTempPath(), "test_output_" + System.Guid.NewGuid().ToString());
        
        try
        {
            // Act
            generator.GenerateHeaderFiles(typeModels, tempDir);

            // Assert - Files should be in AC1Legacy directory with simple names
            string vector3Path = Path.Combine(tempDir, "AC1Legacy", "Vector3.h");
            string smartArrayPath = Path.Combine(tempDir, "AC1Legacy", "SmartArray.h");
            
            Assert.True(File.Exists(vector3Path), $"Vector3.h should exist at {vector3Path}");
            Assert.True(File.Exists(smartArrayPath), $"SmartArray.h should exist at {smartArrayPath}");
            
            // Verify file contents contain the correct type
            string vector3Content = File.ReadAllText(vector3Path);
            Assert.Contains("Vector3", vector3Content);
            
            string smartArrayContent = File.ReadAllText(smartArrayPath);
            Assert.Contains("SmartArray", smartArrayContent);
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
    public void GenerateHeaderFiles_HandlesRelatedTypesInSameFile()
    {
        // Arrange
        var generator = new FileOutputGenerator();
        var typeModels = new List<TypeModel>
        {
            // Simulate AsyncCache scenario - related types that should be grouped together
            new TypeModel
            {
                BaseName = "AsyncCache",
                Namespace = "",  // Root type with empty namespace
                BaseTypePath = "AsyncCache",  // Points to itself
                Type = TypeType.Struct,
                Source = "struct AsyncCache { int x; };"
            },
            new TypeModel
            {
                BaseName = "CCallbackHandler", 
                Namespace = "AsyncCache",  // Nested in AsyncCache namespace
                BaseTypePath = "AsyncCache",  // Should be grouped with AsyncCache
                Type = TypeType.Struct,
                Source = "struct AsyncCache::CCallbackHandler { int y; };"
            }
        };

        // Create a temporary directory for testing
        string tempDir = Path.Combine(Path.GetTempPath(), "test_output_" + System.Guid.NewGuid().ToString());
        
        try
        {
            // Act
            generator.GenerateHeaderFiles(typeModels, tempDir);

            // Assert - Both types should be in the same file in root directory
            string asyncCachePath = Path.Combine(tempDir, "AsyncCache.h");
            
            Assert.True(File.Exists(asyncCachePath), $"AsyncCache.h should exist at {asyncCachePath}");
            
            // Verify file contains both types
            string fileContent = File.ReadAllText(asyncCachePath);
            Assert.Contains("AsyncCache", fileContent);
            Assert.Contains("CCallbackHandler", fileContent);
            
            // Verify there's no duplicate file in subdirectory
            string subDirPath = Path.Combine(tempDir, "AsyncCache");
            Assert.False(Directory.Exists(subDirPath), "There should be no AsyncCache subdirectory");
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
