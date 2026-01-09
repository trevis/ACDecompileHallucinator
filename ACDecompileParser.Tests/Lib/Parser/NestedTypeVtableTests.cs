using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Parser;

public class NestedTypeVtableTests
{
    [Fact]
    public void ParseAndGenerateHeaderFiles_WithNestedTypeAndVtable_CreatesSingleFile()
    {
        // Arrange
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 123 */",
                "struct __cppobj Archive",
                "{",
                "  Archive_vtbl *__vftable /*VFT*/;",
                "  unsigned int m_flags;",
                " TResult m_hrError;",
                "  SmartBuffer m_buffer;",
                "  unsigned int m_currOffset;",
                "  HashTable<unsigned long,Interface *,0> *m_pcUserDataHash;",
                "  IArchiveVersionStack *m_pVersionStack;",
                "};",
                "",
                "/* 1234 */",
                "struct /*VFT*/ Archive_vtbl",
                "{",
                "  void (__thiscall *InitForPacking)(Archive *this, const ArchiveInitializer *, const SmartBuffer *);",
                "  void (__thiscall *InitForUnpacking)(Archive *this, const ArchiveInitializer *, const SmartBuffer *);",
                "  void (__thiscall *SetCheckpointing)(Archive *this, bool);",
                "  void (__thiscall *InitVersionStack)(Archive *this);",
                "  void (__thiscall *CreateVersionStack)(Archive *this);",
                "};",
                "",
                "/* 1236*/",
                "struct __cppobj Archive::SetVersionRow : ArchiveInitializer",
                "{",
                "  const ArchiveVersionRow *m_rInitialData;",
                "};",
                "",
                "/* 1238*/",
                "struct /*VFT*/ Archive::SetVersionRow_vtbl",
                "{",
                "  bool (__thiscall *InitializeArchive)(ArchiveInitializer *this, Archive *);",
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
            Assert.Contains("Archive.h", headerFilePath);

            // Verify the file contains all struct definitions
            // With nested type output, nested types are rendered inside their parent with short names
            string fileContent = File.ReadAllText(headerFilePath);
            Assert.Contains("struct Archive", fileContent);
            Assert.Contains("struct Archive_vtbl", fileContent);
            // Nested type rendered with short name inside parent
            Assert.Contains("struct SetVersionRow", fileContent);
            Assert.Contains("struct SetVersionRow_vtbl", fileContent);
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
    public void ParseAndGenerateHeaderFiles_WithActionStateExample_CreatesSingleFile()
    {
        // Arrange - Test the original example from the issue
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

    [Fact]
    public void Parse_WithNestedTypeAndVtable_SetsBaseTypePathCorrectly()
    {
        // Arrange
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 123 */",
                "struct __cppobj Archive",
                "{",
                "  Archive_vtbl *__vftable /*VFT*/;",
                "  unsigned int m_flags;",
                "  TResult m_hrError;",
                "  SmartBuffer m_buffer;",
                " unsigned int m_currOffset;",
                "  HashTable<unsigned long,Interface *,0> *m_pcUserDataHash;",
                "  IArchiveVersionStack *m_pVersionStack;",
                "};",
                "",
                "/* 1234 */",
                "struct /*VFT*/ Archive_vtbl",
                "{",
                " void (__thiscall *InitForPacking)(Archive *this, const ArchiveInitializer *, const SmartBuffer *);",
                " void (__thiscall *InitForUnpacking)(Archive *this, const ArchiveInitializer *, const SmartBuffer *);",
                " void (__thiscall *SetCheckpointing)(Archive *this, bool);",
                "  void (__thiscall *InitVersionStack)(Archive *this);",
                "  void (__thiscall *CreateVersionStack)(Archive *this);",
                "};",
                "",
                "/* 1236*/",
                "struct __cppobj Archive::SetVersionRow : ArchiveInitializer",
                "{",
                "  const ArchiveVersionRow *m_rInitialData;",
                "};",
                "",
                "/* 1238*/",
                "struct /*VFT*/ Archive::SetVersionRow_vtbl",
                "{",
                "  bool (__thiscall *InitializeArchive)(ArchiveInitializer *this, Archive *);",
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
        Assert.Equal(4, allTypes.Count);

        // Find all the types
        var archiveType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "Archive");
        var archiveVtableType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "Archive_vtbl");
        var setVersionRowType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "Archive::SetVersionRow");
        var setVersionRowVtableType =
            allTypes.FirstOrDefault(t => t.FullyQualifiedName == "Archive::SetVersionRow_vtbl");

        Assert.NotNull(archiveType);
        Assert.NotNull(archiveVtableType);
        Assert.NotNull(setVersionRowType);
        Assert.NotNull(setVersionRowVtableType);

        // Verify BaseTypePath assignments
        Assert.Equal("Archive", archiveType.BaseTypePath);
        Assert.Equal("Archive", archiveVtableType.BaseTypePath); // Vtables should group with parent
        Assert.Equal("Archive", setVersionRowType.BaseTypePath); // Nested types should group with root
        Assert.Equal("Archive", setVersionRowVtableType.BaseTypePath); // Vtables of nested types should group with root
    }
}
