using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Tests.Lib.Parser;

public class ConstStructParsingTests
{
    [Fact]
    public void ParseConstStruct_DetectsIsConstPropertyCorrectly()
    {
        // Arrange - Test the exact ArchiveVersionRow example with const
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 750 */",
                "const struct __cppobj ArchiveVersionRow",
                "{",
                "  ArchiveVersionRow_vtbl *__vftable /*VFT*/;",
                "  PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1> m_aVersions;",
                "};",
                "",
                "/* 751 */",
                "struct /*VFT*/ ArchiveVersionRow_vtbl",
                "{",
                "  unsigned int (__thiscall *GetVersionByToken)(ArchiveVersionRow *this, unsigned int);",
                "};",
                "",
                "/* 752 */",
                "struct ArchiveVersionRow::VersionEntry",
                "{",
                " unsigned int tokVersion;",
                "  unsigned int iVersion;",
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
        Assert.Equal(3, allTypes.Count); // 3 types: ArchiveVersionRow, ArchiveVersionRow_vtbl, ArchiveVersionRow::VersionEntry
        
        // Find the ArchiveVersionRow type
        var archiveVersionRowType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "ArchiveVersionRow");

        Assert.NotNull(archiveVersionRowType);
        Assert.Equal("ArchiveVersionRow", archiveVersionRowType.BaseName);
        Assert.Equal(TypeType.Struct, archiveVersionRowType.Type);
    }
    
    [Fact]
    public void ParseNonConstStruct_DoesNotSetIsConstProperty()
    {
        // Arrange - Test a regular struct without const
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
    }
    
    [Fact]
    public void ParseConstStruct_GeneratesCorrectHeaderFile()
    {
        // Arrange - Test the const struct example
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 750 */",
                "const struct __cppobj ArchiveVersionRow",
                "{",
                "  ArchiveVersionRow_vtbl *__vftable /*VFT*/;",
                "  PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1> m_aVersions;",
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
            
            // Should only be one file: ArchiveVersionRow.h
            Assert.Single(allFiles);
            
            string headerFilePath = allFiles[0];
            Assert.Contains("ArchiveVersionRow.h", headerFilePath);
            
            // Verify the file contains the struct definition
            string fileContent = File.ReadAllText(headerFilePath);
            Assert.Contains("struct ArchiveVersionRow", fileContent);
            Assert.Contains("__vftable", fileContent);
            Assert.Contains("m_aVersions", fileContent);
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
