using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Parser;

public class DebugNestedTypeTest
{
    [Fact]
    public void DebugNestedTypeParsing()
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
