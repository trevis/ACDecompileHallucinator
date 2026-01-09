using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

/// <summary>
/// Tests to reproduce and fix the BaseTypePath bug where template arguments are duplicated
/// </summary>
public class BaseTypePathBugTests
{
    [Fact]
    public void ParseStruct_PrimitiveInplaceArray_TemplateArgsNotDuplicated()
    {
        // Arrange - This is the exact case from the user
        string source = @"
/* 1000 */
struct __cppobj PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1> : SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  char m_aPrimitiveInplaceMemory[64];
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];

        // Verify the struct name and template arguments
        Assert.Equal("PrimitiveInplaceArray", structModel.Name);
        Assert.True(structModel.IsGeneric);
        Assert.Equal(3, structModel.TemplateArguments.Count);

        // Verify each template argument
        Assert.Equal("ArchiveVersionRow::VersionEntry", structModel.TemplateArguments[0].TypeString);
        Assert.Equal("8", structModel.TemplateArguments[1].TypeString);
        Assert.Equal("1", structModel.TemplateArguments[2].TypeString);

        // Verify the fully qualified name with templates doesn't have duplicates
        var fqnWithTemplates = structModel.FullyQualifiedNameWithTemplates;
        Assert.Equal("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>", fqnWithTemplates);

        // Count commas - should be exactly 2 (for 3 arguments)
        var commaCount = fqnWithTemplates.Count(c => c == ',');
        Assert.Equal(2, commaCount);
    }

    [Fact]
    public void SaveAndRetrieve_PrimitiveInplaceArray_BaseTypePathCorrect()
    {
        // Arrange
        string source = @"
/* 1000 */
struct __cppobj SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  void* data;
};

/* 1001 */
struct __cppobj PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1> : SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  char m_aPrimitiveInplaceMemory[64];
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Equal(2, structs.Count);

        // Create in-memory database
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Convert to TypeModel and save
        foreach (var s in structs)
        {
            var typeModel = s.MakeTypeModel();
            repo.InsertType(typeModel);
        }
        repo.SaveChanges();

        // Retrieve all types
        var allTypes = repo.GetAllTypes();

        // Find PrimitiveInplaceArray
        var primitiveArray = allTypes.FirstOrDefault(t => t.BaseName == "PrimitiveInplaceArray");
        Assert.NotNull(primitiveArray);

        // Verify template arguments are correct
        Assert.Equal(3, primitiveArray.TemplateArguments.Count);

        // Verify the template arguments are in the correct order
        var orderedArgs = primitiveArray.TemplateArguments.OrderBy(ta => ta.Position).ToList();
        Assert.Equal("ArchiveVersionRow::VersionEntry", orderedArgs[0].TypeString);
        Assert.Equal("8", orderedArgs[1].TypeString);
        Assert.Equal("1", orderedArgs[2].TypeString);

        // Verify NameWithTemplates
        var nameWithTemplates = primitiveArray.NameWithTemplates;
        Assert.Equal("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>", nameWithTemplates);

        // Ensure no excessive duplication
        var commaCount = nameWithTemplates.Count(c => c == ',');
        Assert.Equal(2, commaCount); // Should be 2 commas for 3 arguments

        // This should NOT be hundreds of arguments!
        Assert.True(commaCount < 10, $"Found {commaCount} commas - template arguments are being duplicated!");
    }

    [Fact]
    public void SaveAndRetrieve_WithBaseTypePath_NoTemplateDuplication()
    {
        // Arrange
        string source = @"
/* 1000 */
struct __cppobj SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  void* data;
};

/* 1001 */
struct __cppobj PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1> : SmartArray<ArchiveVersionRow::VersionEntry,1>
{
  char m_aPrimitiveInplaceMemory[64];
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Equal(2, structs.Count);

        // Create in-memory database
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Convert to TypeModel and save
        foreach (var s in structs)
        {
            var typeModel = s.MakeTypeModel();
            repo.InsertType(typeModel);
        }
        repo.SaveChanges();

        // Retrieve all types
        var allTypes = repo.GetAllTypes();

        // Now compute BaseTypePaths (this is where the bug might manifest)
        repo.PopulateBaseTypePaths(allTypes);
        repo.SaveChanges();

        // Retrieve again to get updated BaseTypePath
        allTypes = repo.GetAllTypes();

        // Find PrimitiveInplaceArray
        var primitiveArray = allTypes.FirstOrDefault(t => t.BaseName == "PrimitiveInplaceArray");
        Assert.NotNull(primitiveArray);

        // Check BaseTypePath
        var baseTypePath = primitiveArray.BaseTypePath;
        Console.WriteLine($"BaseTypePath: {baseTypePath}");

        // The BaseTypePath should NOT have hundreds of template arguments
        if (!string.IsNullOrEmpty(baseTypePath))
        {
            var commaCount = baseTypePath.Count(c => c == ',');
            Assert.True(commaCount < 10,
                $"BaseTypePath has {commaCount} commas! This indicates template argument duplication. BaseTypePath: {baseTypePath}");
        }

        // Verify the struct itself still has correct template arguments
        Assert.Equal(3, primitiveArray.TemplateArguments.Count);
        var nameWithTemplates = primitiveArray.NameWithTemplates;
        Assert.Equal("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>", nameWithTemplates);
    }
}
