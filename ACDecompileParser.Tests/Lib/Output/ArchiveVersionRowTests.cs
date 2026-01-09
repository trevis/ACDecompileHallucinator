using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Output;

public class ArchiveVersionRowTests
{
    [Fact]
    public void ParseAndGenerateHeaderFiles_WithArchiveVersionRowExample_CreatesSingleFile()
    {
        // Arrange - Test the exact ArchiveVersionRow example
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 750 */",
                "struct __cppobj ArchiveVersionRow",
                "{",
                " ArchiveVersionRow_vtbl *__vftable /*VFT*/;",
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
                "  unsigned int tokVersion;",
                " unsigned int iVersion;",
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

            // Should only be one file: ArchiveVersionRow.h (containing all related types)
            Assert.Single(allFiles);

            string headerFilePath = allFiles[0];
            Assert.Contains("ArchiveVersionRow.h", headerFilePath);

            // Verify the file contains all struct definitions
            // With nested type output, nested types are rendered inside their parent
            string fileContent = File.ReadAllText(headerFilePath);
            Assert.Contains("struct ArchiveVersionRow", fileContent);

            // Vtable is now rendered inside the parent class as a nested struct
            Assert.Contains("struct ArchiveVersionRow_vtbl", fileContent);

            // Nested type is rendered with short name inside the parent
            // The format is now: struct VersionEntry { ... } inside ArchiveVersionRow
            Assert.Contains("struct VersionEntry", fileContent);

            // Verify function pointer in vtable
            Assert.Contains("GetVersionByToken", fileContent);

            // Verify member in main struct
            Assert.Contains("__vftable", fileContent);
            Assert.Contains("m_aVersions", fileContent);

            // Verify members in nested struct
            Assert.Contains("tokVersion", fileContent);
            Assert.Contains("iVersion", fileContent);
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
    public void Parse_WithArchiveVersionRowExample_SetsBaseTypePathCorrectly()
    {
        // Arrange - Test the exact ArchiveVersionRow example
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 750 */",
                "struct __cppobj ArchiveVersionRow",
                "{",
                " ArchiveVersionRow_vtbl *__vftable /*VFT*/;",
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
                "  unsigned int tokVersion;",
                "  unsigned int iVersion;",
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
        Assert.Equal(3,
            allTypes.Count); // 3 types: ArchiveVersionRow, ArchiveVersionRow_vtbl, ArchiveVersionRow::VersionEntry

        // Find all the types
        var archiveVersionRowType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "ArchiveVersionRow");
        var archiveVersionRowVtableType =
            allTypes.FirstOrDefault(t => t.FullyQualifiedName == "ArchiveVersionRow_vtbl");
        var versionEntryType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "ArchiveVersionRow::VersionEntry");

        Assert.NotNull(archiveVersionRowType);
        Assert.NotNull(archiveVersionRowVtableType);
        Assert.NotNull(versionEntryType);

        // All nested types and vtables should group with the root ArchiveVersionRow type
        Assert.Equal("ArchiveVersionRow", archiveVersionRowType.BaseTypePath);
        Assert.Equal("ArchiveVersionRow", archiveVersionRowVtableType.BaseTypePath);
        Assert.Equal("ArchiveVersionRow", versionEntryType.BaseTypePath);
    }
}
