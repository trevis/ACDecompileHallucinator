using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using Microsoft.EntityFrameworkCore;

namespace ACDecompileParser.Tests.Lib.Parser;

public class OverloadedFunctionTests
{
    #region Overloaded Function Detection Tests

    [Fact]
    public void ParseMembers_DetectsOverloadedFunctions()
    {
        // Arrange - VTable with two InitForPacking overloads
        var source = @"
/* 2641 */
struct /*VFT*/ AutoStoreVersionArchive_vtbl
{
void (__thiscall *InitForPacking)(Archive *this, const ArchiveInitializer *, const SmartBuffer *);
void (__thiscall *InitForPacking)(AutoStoreVersionArchive *this, const SmartBuffer *);
};";

        var structModel = new StructTypeModel();
        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        // Find struct definition
        int structStartIndex = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("struct"))
            {
                structStartIndex = i;
                break;
            }
        }

        // Act
        StructParser.ParseNameAndInheritance(structModel, source);
        StructParser.ParseMembers(structModel, lines, structStartIndex);

        // Assert
        Assert.Equal(2, structModel.Members.Count);

        // Both members should be named InitForPacking
        Assert.All(structModel.Members, m => Assert.Equal("InitForPacking", m.Name));

        // First overload should have OverloadIndex 0
        Assert.Equal(0, structModel.Members[0].OverloadIndex);

        // Second overload should have OverloadIndex 1
        Assert.Equal(1, structModel.Members[1].OverloadIndex);

        // Verify they have different parameter counts
        Assert.Equal(3, structModel.Members[0].FunctionSignature?.Parameters.Count); // Archive *this, const ArchiveInitializer *, const SmartBuffer *
        Assert.Equal(2, structModel.Members[1].FunctionSignature?.Parameters.Count); // AutoStoreVersionArchive *this, const SmartBuffer *
    }

    [Fact]
    public void ParseMembers_NonOverloadedFunctionsHaveZeroIndex()
    {
        // Arrange
        var source = @"
struct TestVtbl
{
void (__thiscall *Method1)(Test *this);
void (__thiscall *Method2)(Test *this);
void (__thiscall *Method3)(Test *this);
};";

        var structModel = new StructTypeModel();
        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        int structStartIndex = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("struct"))
            {
                structStartIndex = i;
                break;
            }
        }

        // Act
        StructParser.ParseNameAndInheritance(structModel, source);
        StructParser.ParseMembers(structModel, lines, structStartIndex);

        // Assert
        Assert.Equal(3, structModel.Members.Count);
        Assert.All(structModel.Members, m => Assert.Equal(0, m.OverloadIndex));
    }

    [Fact]
    public void ParseMembers_MixedOverloadedAndNonOverloadedFunctions()
    {
        // Arrange
        var source = @"
struct MixedVtbl
{
void (__thiscall *Init)(Test *this);
void (__thiscall *Process)(Test *this, int value);
void (__thiscall *Init)(Test *this, int param);
void (__thiscall *Cleanup)(Test *this);
void (__thiscall *Init)(Test *this, const char *name);
};";

        var structModel = new StructTypeModel();
        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        int structStartIndex = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("struct"))
            {
                structStartIndex = i;
                break;
            }
        }

        // Act
        StructParser.ParseNameAndInheritance(structModel, source);
        StructParser.ParseMembers(structModel, lines, structStartIndex);

        // Assert
        Assert.Equal(5, structModel.Members.Count);

        // Init overloads
        var initMembers = structModel.Members.Where(m => m.Name == "Init").ToList();
        Assert.Equal(3, initMembers.Count);
        Assert.Equal(0, initMembers[0].OverloadIndex);
        Assert.Equal(1, initMembers[1].OverloadIndex);
        Assert.Equal(2, initMembers[2].OverloadIndex);

        // Non-overloaded functions
        var processMembers = structModel.Members.Where(m => m.Name == "Process").ToList();
        Assert.Single(processMembers);
        Assert.Equal(0, processMembers[0].OverloadIndex);

        var cleanupMembers = structModel.Members.Where(m => m.Name == "Cleanup").ToList();
        Assert.Single(cleanupMembers);
        Assert.Equal(0, cleanupMembers[0].OverloadIndex);
    }

    #endregion

    #region Database Integration Tests

    [Fact]
    public void SaveToDatabase_SavesAllOverloadedFunctionParameters()
    {
        // Arrange - VTable with two InitForPacking overloads
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 2641 */",
                "struct /*VFT*/ AutoStoreVersionArchive_vtbl",
                "{",
                "void (__thiscall *InitForPacking)(Archive *this, const ArchiveInitializer *, const SmartBuffer *);",
                "void (__thiscall *InitForPacking)(AutoStoreVersionArchive *this, const SmartBuffer *);",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Act
        parser.SaveToDatabase(repo);

        // Assert - Verify both overloads are saved with their parameters
        var allTypes = repo.GetAllTypes();
        Assert.Single(allTypes);

        var vtblType = allTypes[0];
        Assert.Equal("AutoStoreVersionArchive_vtbl", vtblType.BaseName);

        // Get struct members from the database
        var structMembers = repo.GetStructMembers(vtblType.Id);
        Assert.Equal(2, structMembers.Count);

        // Both should have the same name but different OverloadIndex
        Assert.All(structMembers, m => Assert.Equal("InitForPacking", m.Name));
        Assert.Contains(structMembers, m => m.OverloadIndex == 0);
        Assert.Contains(structMembers, m => m.OverloadIndex == 1);

        // Verify function parameters are saved for BOTH overloads
        var overload0 = structMembers.First(m => m.OverloadIndex == 0);
        var overload1 = structMembers.First(m => m.OverloadIndex == 1);

        Assert.NotNull(overload0.FunctionSignature);
        Assert.NotNull(overload1.FunctionSignature);

        var params0 = overload0.FunctionSignature.Parameters;
        var params1 = overload1.FunctionSignature.Parameters;

        // First overload: Archive *this, const ArchiveInitializer *, const SmartBuffer *
        Assert.Equal(3, params0.Count);

        // Second overload: AutoStoreVersionArchive *this, const SmartBuffer *
        Assert.Equal(2, params1.Count);
    }

    [Fact]
    public void SaveToDatabase_SavesTripleOverloadedFunctionParameters()
    {
        // Arrange - VTable with three Init overloads
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 1000 */",
                "struct MixedVtbl",
                "{",
                "void (__thiscall *Init)(Test *this);",
                "void (__thiscall *Process)(Test *this, int value);",
                "void (__thiscall *Init)(Test *this, int param);",
                "void (__thiscall *Cleanup)(Test *this);",
                "void (__thiscall *Init)(Test *this, const char *name);",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Act
        parser.SaveToDatabase(repo);

        // Assert
        var allTypes = repo.GetAllTypes();
        Assert.Single(allTypes);

        var structMembers = repo.GetStructMembers(allTypes[0].Id);
        Assert.Equal(5, structMembers.Count);

        // Verify all three Init overloads have their parameters saved
        var initMembers = structMembers.Where(m => m.Name == "Init").ToList();
        Assert.Equal(3, initMembers.Count);

        foreach (var initMember in initMembers)
        {
            Assert.NotNull(initMember.FunctionSignature);
            var funcParams = initMember.FunctionSignature.Parameters;
            Assert.NotEmpty(funcParams);

            // Verify overload index determines the correct parameter count
            switch (initMember.OverloadIndex)
            {
                case 0: // Init(Test *this)
                    Assert.Single(funcParams);
                    break;
                case 1: // Init(Test *this, int param)
                    Assert.Equal(2, funcParams.Count);
                    break;
                case 2: // Init(Test *this, const char *name)
                    Assert.Equal(2, funcParams.Count);
                    break;
            }
        }

        // Verify non-overloaded functions also have their parameters saved
        var processMembers = structMembers.Where(m => m.Name == "Process").ToList();
        Assert.Single(processMembers);
        Assert.NotNull(processMembers[0].FunctionSignature);
        var processParams = processMembers[0].FunctionSignature!.Parameters;
        Assert.Equal(2, processParams.Count); // Test *this, int value

        var cleanupMembers = structMembers.Where(m => m.Name == "Cleanup").ToList();
        Assert.Single(cleanupMembers);
        Assert.NotNull(cleanupMembers[0].FunctionSignature);
        var cleanupParams = cleanupMembers[0].FunctionSignature!.Parameters;
        Assert.Single(cleanupParams); // Test *this
    }

    #endregion
}
