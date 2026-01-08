using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

/// <summary>
/// Integration tests that run the full pipeline to detect template argument duplication bugs
/// </summary>
public class TemplateArgumentDuplicationIntegrationTests
{
    [Fact]
    public void FullPipeline_PrimitiveInplaceArray_NoTemplateDuplication()
    {
        // Arrange - Full source with inheritance
        string source = @"
/* 1000 */
struct __cppobj SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  void* m_pData;
};

/* 1001 */
struct __cppobj PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1> : SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  char m_aPrimitiveInplaceMemory[64];
};";

        // Act - Run through full pipeline
        var parser = new SourceParser(new List<string> { source }, new List<string> { "test.h" });
        parser.Parse();

        // Debug: Check parsed structs BEFORE database save
        Console.WriteLine("\n=== BEFORE DATABASE SAVE ===");
        foreach (var structModel in parser.StructModels)
        {
            Console.WriteLine($"Struct: {structModel.Name}");
            Console.WriteLine($"  Template Args Count: {structModel.TemplateArguments.Count}");
            for (int i = 0; i < structModel.TemplateArguments.Count; i++)
            {
                Console.WriteLine($"    [{i}] TypeString: '{structModel.TemplateArguments[i].TypeString}'");
            }
        }

        // Create in-memory database and save
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        context.Database.EnsureCreated();
        using var repo = new TypeRepository(context);

        // Save to database (this is where duplication might occur)
        parser.SaveToDatabase(repo);

        // Retrieve all types from database
        var allTypes = repo.GetAllTypes();

        // Verify PrimitiveInplaceArray
        var primitiveArray = allTypes.FirstOrDefault(t => t.BaseName == "PrimitiveInplaceArray");
        Assert.NotNull(primitiveArray);

        // Check template arguments count
        Assert.Equal(3, primitiveArray.TemplateArguments.Count);

        // Verify each template argument by position
        var orderedArgs = primitiveArray.TemplateArguments.OrderBy(ta => ta.Position).ToList();
        Assert.Equal(0, orderedArgs[0].Position);
        Assert.Equal("ArchiveVersionRow::VersionEntry", orderedArgs[0].TypeString);

        Assert.Equal(1, orderedArgs[1].Position);
        Assert.Equal("8", orderedArgs[1].TypeString);

        Assert.Equal(2, orderedArgs[2].Position);
        Assert.Equal("1", orderedArgs[2].TypeString);

        // Verify NameWithTemplates
        var nameWithTemplates = primitiveArray.NameWithTemplates;
        Console.WriteLine($"NameWithTemplates: {nameWithTemplates}");

        Assert.Equal("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>", nameWithTemplates);

        // Count commas - MUST be exactly 2 for 3 arguments
        var commaCount = nameWithTemplates.Count(c => c == ',');
        Assert.Equal(2, commaCount);
        Assert.True(commaCount < 10, $"Found {commaCount} commas - template arguments are being duplicated!");
    }

    [Fact]
    public void FullPipeline_IDClass_NoTemplateDuplication()
    {
        // Arrange - The user's IDClass example
        string source = @"
/* 1000 */
struct __cppobj IDClass<_tagVersionHandle,32,32>
{
  unsigned int m_Key;
};";

        // Act - Run through full pipeline
        var parser = new SourceParser(new List<string> { source }, new List<string> { "test.h" });
        parser.Parse();

        // Create in-memory database and save
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        context.Database.EnsureCreated();
        using var repo = new TypeRepository(context);

        // Save to database
        parser.SaveToDatabase(repo);

        // Retrieve from database
        var allTypes = repo.GetAllTypes();

        // Verify IDClass
        var idClass = allTypes.FirstOrDefault(t => t.BaseName == "IDClass");
        Assert.NotNull(idClass);

        // Check template arguments count - should be exactly 3
        Assert.Equal(3, idClass.TemplateArguments.Count);

        // Verify each template argument
        var orderedArgs = idClass.TemplateArguments.OrderBy(ta => ta.Position).ToList();
        Assert.Equal("_tagVersionHandle", orderedArgs[0].TypeString);
        Assert.Equal("32", orderedArgs[1].TypeString);
        Assert.Equal("32", orderedArgs[2].TypeString);

        // Verify NameWithTemplates
        var nameWithTemplates = idClass.NameWithTemplates;
        Console.WriteLine($"IDClass NameWithTemplates: {nameWithTemplates}");

        Assert.Equal("IDClass<_tagVersionHandle,32,32>", nameWithTemplates);

        // CRITICAL: Count commas - should be exactly 2, NOT hundreds!
        var commaCount = nameWithTemplates.Count(c => c == ',');
        Assert.Equal(2, commaCount);
        Assert.True(commaCount < 10,
            $"BUG DETECTED: Found {commaCount} commas in '{nameWithTemplates}' - template arguments are being duplicated!");
    }

    [Fact]
    public void FullPipeline_WithBaseTypePath_NoTemplateDuplication()
    {
        // Arrange
        string source = @"
/* 1000 */
struct __cppobj SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  void* m_pData;
};

/* 1001 */
struct __cppobj PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1> : SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  char m_aPrimitiveInplaceMemory[64];
};";

        // Act - Run through full pipeline INCLUDING BaseTypePath population
        var parser = new SourceParser(new List<string> { source }, new List<string> { "test.h" });
        parser.Parse();

        // Create in-memory database and save
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        context.Database.EnsureCreated();
        using var repo = new TypeRepository(context);

        // Save to database
        parser.SaveToDatabase(repo);

        // Get all types
        var allTypes = repo.GetAllTypes();

        // Populate BaseTypePaths (this is where the user sees the bug)
        repo.PopulateBaseTypePaths(allTypes);
        repo.SaveChanges();

        // Retrieve again with updated BaseTypePaths
        allTypes = repo.GetAllTypes();

        // Verify PrimitiveInplaceArray
        var primitiveArray = allTypes.FirstOrDefault(t => t.BaseName == "PrimitiveInplaceArray");
        Assert.NotNull(primitiveArray);

        // Log the BaseTypePath
        Console.WriteLine($"BaseTypePath: {primitiveArray.BaseTypePath}");

        // Check if BaseTypePath has excessive template arguments
        if (!string.IsNullOrEmpty(primitiveArray.BaseTypePath))
        {
            var commaCount = primitiveArray.BaseTypePath.Count(c => c == ',');
            Console.WriteLine($"BaseTypePath comma count: {commaCount}");

            Assert.True(commaCount < 10,
                $"BUG DETECTED in BaseTypePath: Found {commaCount} commas in BaseTypePath '{primitiveArray.BaseTypePath}'");
        }

        // Also verify the NameWithTemplates hasn't been corrupted
        var nameWithTemplates = primitiveArray.NameWithTemplates;
        Console.WriteLine($"NameWithTemplates: {nameWithTemplates}");

        var nameCommaCount = nameWithTemplates.Count(c => c == ',');
        Assert.Equal(2, nameCommaCount);
        Assert.True(nameCommaCount < 10,
            $"BUG DETECTED in NameWithTemplates: Found {nameCommaCount} commas - template arguments duplicated!");
    }

    [Fact]
    public void DirectDatabaseQuery_TemplateArguments_NoDuplicates()
    {
        // This test directly queries the database to check for duplicate template argument records
        string source = @"
/* 1000 */
struct __cppobj IDClass<_tagVersionHandle,32,32>
{
  unsigned int m_Key;
};";

        var parser = new SourceParser(new List<string> { source }, new List<string> { "test.h" });
        parser.Parse();

        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        context.Database.EnsureCreated();
        using var repo = new TypeRepository(context);

        parser.SaveToDatabase(repo);

        // Get the IDClass type
        var allTypes = repo.GetAllTypes();
        var idClass = allTypes.FirstOrDefault(t => t.BaseName == "IDClass");
        Assert.NotNull(idClass);

        // Directly query the TypeTemplateArguments table
        var allTemplateArgs = repo.GetAllTypeTemplateArguments();
        var idClassTemplateArgs = allTemplateArgs.Where(ta => ta.ParentTypeId == idClass.Id).ToList();

        // Log all template arguments for this type
        Console.WriteLine($"Template arguments in database for IDClass (ParentTypeId={idClass.Id}):");
        foreach (var ta in idClassTemplateArgs)
        {
            Console.WriteLine($"  - Id={ta.Id}, Position={ta.Position}, TypeString='{ta.TypeString}'");
        }

        // Verify count
        Assert.Equal(3, idClassTemplateArgs.Count);

        // Check for duplicate positions
        var positionGroups = idClassTemplateArgs.GroupBy(ta => ta.Position);
        foreach (var group in positionGroups)
        {
            var count = group.Count();
            Assert.True(count == 1,
                $"BUG DETECTED: Position {group.Key} appears {count} times in database! There are duplicate template argument records.");
        }
    }
}
