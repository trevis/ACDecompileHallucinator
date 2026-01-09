using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

/// <summary>
/// Tests to verify that template arguments are being parsed correctly
/// and stored correctly in the database
/// </summary>
public class TemplateArgumentParsingTests
{
    [Fact]
    public void ParseType_SimpleTemplate_ParsesCorrectNumberOfArguments()
    {
        // Arrange
        var typeString = "IDClass<_tagVersionHandle,32,32>";

        // Act
        var parsed = TypeParser.ParseType(typeString);

        // Assert
        Assert.True(parsed.IsGeneric);
        Assert.Equal(3, parsed.TemplateArguments.Count);
        Assert.Equal("_tagVersionHandle", parsed.TemplateArguments[0].BaseName);
        Assert.Equal("32", parsed.TemplateArguments[1].BaseName);
        Assert.Equal("32", parsed.TemplateArguments[2].BaseName);
    }

    [Fact]
    public void ParseType_TemplateWithMultipleArgs_ParsesCorrectCount()
    {
        // Arrange
        var typeString = "Vector<int,float,double>";

        // Act
        var parsed = TypeParser.ParseType(typeString);

        // Assert
        Assert.True(parsed.IsGeneric);
        Assert.Equal(3, parsed.TemplateArguments.Count);
        Assert.Equal("int", parsed.TemplateArguments[0].BaseName);
        Assert.Equal("float", parsed.TemplateArguments[1].BaseName);
        Assert.Equal("double", parsed.TemplateArguments[2].BaseName);
    }

    [Fact]
    public void ParseType_NestedTemplate_ParsesCorrectly()
    {
        // Arrange
        var typeString = "HashMap<int,Vector<float,double>>";

        // Act
        var parsed = TypeParser.ParseType(typeString);

        // Assert
        Assert.True(parsed.IsGeneric);
        Assert.Equal(2, parsed.TemplateArguments.Count);
        Assert.Equal("int", parsed.TemplateArguments[0].BaseName);

        // Check nested template
        Assert.True(parsed.TemplateArguments[1].IsGeneric);
        Assert.Equal("Vector", parsed.TemplateArguments[1].BaseName);
        Assert.Equal(2, parsed.TemplateArguments[1].TemplateArguments.Count);
    }

    [Fact]
    public void ParseStruct_TemplateStruct_StoresCorrectArgumentCount()
    {
        // Arrange
        string source = @"
/* 1000 */
struct IDClass<_tagVersionHandle,32,32>
{
  unsigned int m_Key;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];
        Assert.Equal("IDClass", structModel.Name);
        Assert.True(structModel.IsGeneric);
        Assert.Equal(3, structModel.TemplateArguments.Count);

        // Verify each template argument (StructTypeModel uses List<TypeReference> for TemplateArguments)
        Assert.Equal("_tagVersionHandle", structModel.TemplateArguments[0].TypeString);
        Assert.Equal("32", structModel.TemplateArguments[1].TypeString);
        Assert.Equal("32", structModel.TemplateArguments[2].TypeString);
    }

    [Fact]
    public void ParseStruct_TemplateWithManyArgs_DoesNotCreateDuplicates()
    {
        // Arrange - simulating a case where many args might be incorrectly parsed
        string source = @"
/* 1000 */
struct MultiArgTemplate<int,float,double,char,bool>
{
  int value;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];
        Assert.Equal("MultiArgTemplate", structModel.Name);
        Assert.True(structModel.IsGeneric);
        Assert.Equal(5, structModel.TemplateArguments.Count);

        // Verify arguments are stored in order (positions are implicit by list index)
        Assert.Equal("int", structModel.TemplateArguments[0].TypeString);
        Assert.Equal("float", structModel.TemplateArguments[1].TypeString);
        Assert.Equal("double", structModel.TemplateArguments[2].TypeString);
        Assert.Equal("char", structModel.TemplateArguments[3].TypeString);
        Assert.Equal("bool", structModel.TemplateArguments[4].TypeString);
    }

    [Fact]
    public void ParseStruct_TemplateMemberType_ParsesCorrectly()
    {
        // Arrange
        string source = @"
/* 1000 */
struct ContainerStruct
{
  IDClass<_tagVersionHandle,32,32> m_id;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];
        Assert.Single(structModel.Members);

        var member = structModel.Members[0];
        Assert.Equal("m_id", member.Name);
        Assert.NotNull(member.TypeReference);

        // Parse the member's type to check template arguments
        var memberType = TypeParser.ParseType(member.TypeReference.TypeString);
        Assert.True(memberType.IsGeneric);
        Assert.Equal("IDClass", memberType.BaseName);
        Assert.Equal(3, memberType.TemplateArguments.Count);
    }

    [Fact]
    public void SaveAndRetrieve_TemplateStruct_PreservesArgumentCount()
    {
        // Arrange
        string source = @"
/* 1000 */
struct IDClass<_tagVersionHandle,32,32>
{
  unsigned int m_Key;
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Single(structs);
        var structModel = structs[0];

        // Convert to TypeModel for database storage
        var typeModel = structModel.MakeTypeModel();

        // Create an in-memory database
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Act - Save to database
        repo.InsertType(typeModel);
        repo.SaveChanges();

        // Retrieve from database
        var allTypes = repo.GetAllTypes();
        var retrieved = allTypes.FirstOrDefault(t => t.BaseName == "IDClass");

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsGeneric);
        Assert.Equal(3, retrieved.TemplateArguments.Count);

        // Verify the NameWithTemplates doesn't have duplicates
        var expectedName = "IDClass<_tagVersionHandle,32,32>";
        Assert.Equal(expectedName, retrieved.NameWithTemplates);

        // Count commas to verify argument count
        var commaCount = retrieved.NameWithTemplates.Count(c => c == ',');
        Assert.Equal(2, commaCount); // 3 arguments = 2 commas
    }

    [Fact]
    public void SaveAndRetrieve_ComplexTemplate_NoArgumentDuplication()
    {
        // Arrange
        string source = @"
/* 1000 */
struct SmartArray<UIMessageData,1>
{
  void* data;
};

/* 1001 */
struct AutoGrowHashTable<unsigned long,SmartArray<UIMessageData,1>>
{
  int size;
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Equal(2, structs.Count);

        // Create an in-memory database
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Act - Save to database
        foreach (var s in structs)
        {
            var typeModel = s.MakeTypeModel();
            repo.InsertType(typeModel);
        }
        repo.SaveChanges();

        // Retrieve from database
        var allTypes = repo.GetAllTypes();
        var retrieved = allTypes.FirstOrDefault(t => t.BaseName == "AutoGrowHashTable");

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsGeneric);
        Assert.Equal(2, retrieved.TemplateArguments.Count);

        // Verify the name format
        var name = retrieved.NameWithTemplates;
        Assert.Contains("AutoGrowHashTable<", name);
        Assert.Contains("unsigned long", name);
        Assert.Contains("SmartArray<UIMessageData,1>", name);

        // Ensure no excessive arguments
        var templateStart = name.IndexOf('<');
        var templateEnd = name.LastIndexOf('>');
        var templateContent = name.Substring(templateStart + 1, templateEnd - templateStart - 1);

        // Count top-level commas only (ignoring nested template commas)
        int commaCount = 0;
        int depth = 0;
        foreach (char c in templateContent)
        {
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0) commaCount++;
        }

        Assert.Equal(1, commaCount); // 2 arguments = 1 comma
    }

    [Fact]
    public void ParseType_LongNumericArgumentList_ParsesCorrectly()
    {
        // This tests whether numeric arguments separated by commas are parsed correctly
        // without creating hundreds of duplicate entries
        var typeString = "IDClass<_tagVersionHandle,32,32,0,0,0,0,0,0,0,0>";

        var parsed = TypeParser.ParseType(typeString);

        Assert.True(parsed.IsGeneric);
        // Should have exactly 11 arguments, not hundreds
        Assert.Equal(11, parsed.TemplateArguments.Count);

        // Verify first and last arguments
        Assert.Equal("_tagVersionHandle", parsed.TemplateArguments[0].BaseName);
        Assert.Equal("0", parsed.TemplateArguments[10].BaseName);
    }

    [Fact]
    public void StructNameWithTemplates_NoExcessiveArguments()
    {
        // Create a struct with a specific number of template arguments
        // Note: StructTypeModel uses List<TypeReference> for TemplateArguments
        var structModel = new StructTypeModel
        {
            Name = "IDClass",
            Namespace = "",
            TemplateArguments = new List<TypeReference>
            {
                new TypeReference { TypeString = "_tagVersionHandle" },
                new TypeReference { TypeString = "32" },
                new TypeReference { TypeString = "32" }
            }
        };

        var nameWithTemplates = structModel.FullyQualifiedNameWithTemplates;

        Assert.Equal("IDClass<_tagVersionHandle,32,32>", nameWithTemplates);

        // Count arguments - should be exactly 3
        var commaCount = nameWithTemplates.Count(c => c == ',');
        Assert.Equal(2, commaCount); // 3 args = 2 commas
    }
}
