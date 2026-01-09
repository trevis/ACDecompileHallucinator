using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Parser;

public class DebugVtableNestedTest
{
    [Fact]
    public void DebugVtableNestedParsing()
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

        // Act
        parser.Parse();

        // Print information about parsed types before saving to database
        Console.WriteLine($"Parsed {parser.TypeModels.Count} type models:");
        foreach (var typeModel in parser.TypeModels)
        {
            Console.WriteLine($"  BaseName: '{typeModel.BaseName}', Namespace: '{typeModel.Namespace}', FullyQualifiedName: '{typeModel.FullyQualifiedName}'");
        }

        parser.SaveToDatabase(repo);

        // Print information about types after saving to database
        var allTypes = repo.GetAllTypes();
        Console.WriteLine($"After saving to DB, got {allTypes.Count} types:");
        foreach (var typeModel in allTypes)
        {
            Console.WriteLine($"  BaseName: '{typeModel.BaseName}', Namespace: '{typeModel.Namespace}', FullyQualifiedName: '{typeModel.FullyQualifiedName}', BaseTypePath: '{typeModel.BaseTypePath}'");
        }
    }
}
