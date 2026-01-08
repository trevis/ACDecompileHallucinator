using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using System.Collections.Generic;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Output;

public class FileOutputGeneratorVtableNamespaceTest
{
    [Fact]
    public void GenerateHeaderFiles_VtablesInCorrectNamespaceDirectories()
    {
        // Arrange
        var generator = new FileOutputGenerator();
        var typeModels = new List<TypeModel>
        {
            // PSRefBuffer<char> in root namespace
            new TypeModel
            {
                BaseName = "PSRefBuffer",
                Namespace = "",
                Type = TypeType.Struct,
                Source = @"struct PSRefBuffer<char> : PSRefBufferStatistics<char>, PSRefBufferCharData<char>
{
};",
                BaseTypePath = "PSRefBuffer", // Points to itself
                TemplateArguments = new List<TypeTemplateArgument>
                {
                    new TypeTemplateArgument { Position = 0, TypeString = "char" }
                }
            },
            // PSRefBuffer<char>_vtbl in root namespace (should be grouped with PSRefBuffer)
            new TypeModel
            {
                BaseName = "PSRefBuffer_vtbl",
                Namespace = "",
                Type = TypeType.Struct,
                Source = @"struct /*VFT*/ PSRefBuffer<char>_vtbl
{
  void (__thiscall *~ReferenceCountTemplate<268435456,0>)(ReferenceCountTemplate<268435456,0> *this);
};",
                BaseTypePath = "PSRefBuffer", // Should be grouped with PSRefBuffer
                IsVTable = true
            },
            // AC1Legacy::PSRefBuffer<char> in AC1Legacy namespace
            new TypeModel
            {
                BaseName = "PSRefBuffer",
                Namespace = "AC1Legacy",
                Type = TypeType.Struct,
                Source = @"struct __cppobj __declspec(align(4)) AC1Legacy::PSRefBuffer<char> : ReferenceCountTemplate<268435456,0>
{
  unsigned int m_len;
  unsigned int m_size;
  unsigned int m_hash;
  char m_data[1];
};",
                BaseTypePath = "AC1Legacy::PSRefBuffer", // Points to itself
                TemplateArguments = new List<TypeTemplateArgument>
                {
                    new TypeTemplateArgument { Position = 0, TypeString = "char" }
                }
            },
            // AC1Legacy::PSRefBuffer<char>_vtbl in AC1Legacy namespace (should be grouped with AC1Legacy::PSRefBuffer)
            new TypeModel
            {
                BaseName = "PSRefBuffer_vtbl",
                Namespace = "AC1Legacy",
                Type = TypeType.Struct,
                Source = @"struct /*VFT*/ AC1Legacy::PSRefBuffer<char>_vtbl
{
  void (__thiscall *~ReferenceCountTemplate<268435456,0>)(ReferenceCountTemplate<268435456,0> *this);
};",
                BaseTypePath = "AC1Legacy::PSRefBuffer", // Should be grouped with AC1Legacy::PSRefBuffer
                IsVTable = true
            }
        };

        // Create a temporary directory for testing
        string tempDir = Path.Combine(Path.GetTempPath(), "test_output_" + System.Guid.NewGuid().ToString());
        
        try
        {
            // Act
            generator.GenerateHeaderFiles(typeModels, tempDir);

            // Assert - Check what files were actually created
            var allFiles = Directory.GetFiles(tempDir, "*.h", SearchOption.AllDirectories);
            
            // Should have 2 files:
            // 1. include/PSRefBuffer.h (for root namespace PSRefBuffer<char> and PSRefBuffer<char>_vtbl)
            // 2. include/AC1Legacy/PSRefBuffer.h (for AC1Legacy::PSRefBuffer<char> and AC1Legacy::PSRefBuffer<char>_vtbl)
            Assert.Equal(2, allFiles.Length);
            
            string rootFile = Path.Combine(tempDir, "PSRefBuffer.h");
            string ac1LegacyFile = Path.Combine(tempDir, "AC1Legacy", "PSRefBuffer.h");
            
            Assert.True(File.Exists(rootFile), $"Root file should exist at {rootFile}");
            Assert.True(File.Exists(ac1LegacyFile), $"AC1Legacy file should exist at {ac1LegacyFile}");
            
            // Verify root file contains root namespace types
            string rootContent = File.ReadAllText(rootFile);
            Assert.Contains("PSRefBuffer", rootContent.Substring(50));
            Assert.Contains("PSRefBuffer_vtbl", rootContent);
            Assert.DoesNotContain("AC1Legacy", rootContent); // Should not contain AC1Legacy types
            
            // Verify AC1Legacy file contains AC1Legacy namespace types
            string ac1LegacyContent = File.ReadAllText(ac1LegacyFile);
            Assert.Contains("AC1Legacy::PSRefBuffer", ac1LegacyContent);
            Assert.Contains("AC1Legacy::PSRefBuffer_vtbl", ac1LegacyContent);
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
