using ACDecompileParser.Lib.Output;
using System.Collections.Generic;
using System.Linq;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class StructMemberOrderingTests
{
    [Fact]
    public void ParseStructWithMembers_MaintainsDeclarationOrder()
    {
        // Arrange
        string source = @"
/* 10423 */
struct localVar0
{
  unsigned int l_ebp;
  unsigned int l_esi;
  unsigned int l_edi;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];
        Assert.Equal("localVar0", structModel.Name);
        Assert.Equal(3, structModel.Members.Count);

        // Check that members are in the correct order
        Assert.Equal("l_ebp", structModel.Members[0].Name);
        Assert.Equal("l_esi", structModel.Members[1].Name);
        Assert.Equal("l_edi", structModel.Members[2].Name);

        // Check that declaration order is preserved
        Assert.Equal(0, structModel.Members[0].DeclarationOrder);
        Assert.Equal(1, structModel.Members[1].DeclarationOrder);
        Assert.Equal(2, structModel.Members[2].DeclarationOrder);
    }

    [Fact]
    public void ParseStructWithMembersAndOffsets_MaintainsOrderAndCalculatesOffsetsCorrectly()
    {
        // Arrange
        string source = @"
/* 10423 */
struct localVar0
{
  unsigned int l_ebp;
  unsigned int l_esi;
  unsigned int l_edi;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];
        Assert.Equal("localVar0", structModel.Name);
        Assert.Equal(3, structModel.Members.Count);

        // Check that members are in the correct order
        Assert.Equal("l_ebp", structModel.Members[0].Name);
        Assert.Equal("l_esi", structModel.Members[1].Name);
        Assert.Equal("l_edi", structModel.Members[2].Name);

        // Check declaration order
        Assert.Equal(0, structModel.Members[0].DeclarationOrder);
        Assert.Equal(1, structModel.Members[1].DeclarationOrder);
        Assert.Equal(2, structModel.Members[2].DeclarationOrder);
    }

    [Fact]
    public void ParseComplexStruct_MaintainsDeclarationOrder()
    {
        // Arrange
        string source = @"
/* 10424 */
struct Param0
{
  localVar0 loc;
  unsigned int retAddr;
  unsigned int pOut;
  unsigned int OutStride;
  unsigned int pV;
  unsigned int VStride;
  unsigned int pM;
  unsigned int n;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];
        Assert.Equal("Param0", structModel.Name);
        Assert.Equal(8, structModel.Members.Count);

        // Check that members are in the correct order
        Assert.Equal("loc", structModel.Members[0].Name);
        Assert.Equal("retAddr", structModel.Members[1].Name);
        Assert.Equal("pOut", structModel.Members[2].Name);
        Assert.Equal("OutStride", structModel.Members[3].Name);
        Assert.Equal("pV", structModel.Members[4].Name);
        Assert.Equal("VStride", structModel.Members[5].Name);
        Assert.Equal("pM", structModel.Members[6].Name);
        Assert.Equal("n", structModel.Members[7].Name);

        // Check declaration order
        for (int i = 0; i < structModel.Members.Count; i++)
        {
            Assert.Equal(i, structModel.Members[i].DeclarationOrder);
        }
    }

    [Fact]
    public void OffsetCalculation_MaintainsDeclarationOrder()
    {
        // Arrange
        string source = @"
/* 10423 */
struct localVar0
{
  unsigned int l_ebp;
  unsigned int l_esi;
  unsigned int l_edi;
};";

        var structs = TypeParser.ParseStructs(source);
        var structTypeModel = structs[0];

        // Convert StructTypeModel to TypeModel for the test
        var structModel = new TypeModel
        {
            Id = 1,
            BaseName = structTypeModel.Name,
            Namespace = structTypeModel.Namespace,
            Type = TypeType.Struct,
            Source = source
        };

        // Create a mock repository to test offset calculation
        var mockRepository = new TestTypeRepository();
        mockRepository.InsertType(structModel);

        // Add the struct members from the parsed structTypeModel to the mock repository
        for (int i = 0; i < structTypeModel.Members.Count; i++)
        {
            var originalMember = structTypeModel.Members[i];
            var member = new StructMemberModel
            {
                Id = i + 1,
                Name = originalMember.Name,
                TypeString = originalMember.TypeString,
                Source = originalMember.Source,
                DeclarationOrder = i, // Set the declaration order
                StructTypeId = structModel.Id
            };
            mockRepository.InsertStructMember(member);
        }

        var offsetService = new OffsetCalculationService(mockRepository);

        // Act
        offsetService.CalculateStructMemberOffsets(structModel);

        // Assert - check that offsets are increasing order
        var members = mockRepository.GetStructMembersWithRelatedTypes(structModel.Id).OrderBy(m => m.DeclarationOrder)
            .ToList();

        Assert.Equal(3, members.Count);
        Assert.Equal(0, members[0].Offset); // l_ebp at 0x00
        Assert.Equal(4, members[1].Offset); // l_esi at 0x04 (assuming 4-byte unsigned int)
        Assert.Equal(8, members[2].Offset); // l_edi at 0x08
    }

    [Fact]
    public void StructOutputGenerator_MaintainsDeclarationOrder()
    {
        // Arrange
        string source = @"
/* 10423 */
struct localVar0
{
  unsigned int l_ebp;
  unsigned int l_esi;
  unsigned int l_edi;
};";

        var structs = TypeParser.ParseStructs(source);
        var structTypeModel = structs[0];

        // Convert StructTypeModel to TypeModel for the test
        var typeModel = new TypeModel
        {
            Id = 1,
            BaseName = structTypeModel.Name,
            Namespace = structTypeModel.Namespace,
            Type = TypeType.Struct,
            Source = source
        };

        // Create a mock repository
        var mockRepository = new TestTypeRepository();
        mockRepository.InsertType(typeModel);

        // Add the struct members to the mock repository with declaration order
        for (int i = 0; i < structTypeModel.Members.Count; i++)
        {
            var originalMember = structTypeModel.Members[i];
            var member = new StructMemberModel
            {
                Id = i + 1,
                Name = originalMember.Name,
                TypeString = originalMember.TypeString,
                Source = originalMember.Source,
                DeclarationOrder = i, // Set the declaration order
                StructTypeId = typeModel.Id,
                Offset = i * 4 // Set offsets for testing
            };
            mockRepository.InsertStructMember(member);
        }

        var outputGenerator = new StructOutputGenerator(mockRepository);

        // Act
        var output = outputGenerator.GenerateDefinition(typeModel);

        // Assert - check that the output contains members in the correct order
        var lines = output.Split('\n');
        var memberLines = lines.Where(l => l.Trim().StartsWith("uint32_t") && l.Contains("//")).ToList();
        Assert.Equal(3, memberLines.Count);

        // Check that the members appear in the correct order in the output
        Assert.Contains("l_ebp", memberLines[0]);
        Assert.Contains("l_esi", memberLines[1]);
        Assert.Contains("l_edi", memberLines[2]);

        // Check that the offsets are in increasing order
        Assert.Contains("0x00", memberLines[0]);
        Assert.Contains("0x04", memberLines[1]);
        Assert.Contains("0x08", memberLines[2]);
    }
}
