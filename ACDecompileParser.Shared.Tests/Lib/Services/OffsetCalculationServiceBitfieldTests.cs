using System.Collections.Generic;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Services;

public class OffsetCalculationServiceBitfieldTests
{
    [Fact]
    public void CalculateStructMemberOffsets_PacksBitfieldsCorrectly()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        var service = new OffsetCalculationService(mockRepository);

        var structModel = new TypeModel
        {
            Id = 1,
            BaseName = "UIRegion",
            Type = TypeType.Struct
        };

        // Create bitfield members matching the user's example
        // unsigned __int32 m_mouseOverTop : 1;
        // unsigned __int32 m_visible : 1;
        var m_mouseOverTop = new StructMemberModel
        {
            Name = "m_mouseOverTop",
            TypeString = "unsigned __int32",
            BitFieldWidth = 1,
            DeclarationOrder = 0
        };

        var m_visible = new StructMemberModel
        {
            Name = "m_visible",
            TypeString = "unsigned __int32",
            BitFieldWidth = 1,
            DeclarationOrder = 1
        };

        var m_transparent = new StructMemberModel
        {
            Name = "m_transparent",
            TypeString = "unsigned __int32",
            BitFieldWidth = 1,
            DeclarationOrder = 2
        };

        // Mock the repository to return these members
        m_mouseOverTop.StructTypeId = structModel.Id;
        m_visible.StructTypeId = structModel.Id;
        m_transparent.StructTypeId = structModel.Id;

        mockRepository.InsertStructMember(m_mouseOverTop);
        mockRepository.InsertStructMember(m_visible);
        mockRepository.InsertStructMember(m_transparent);

        // Act
        service.CalculateStructMemberOffsets(structModel);

        // Assert
        // All three bitfields should be packed into the same 4-byte integer (assuming they start at 0)
        Assert.Equal(0, m_mouseOverTop.Offset);
        Assert.Equal(0, m_visible.Offset); // Currently fails: likely 4
        Assert.Equal(0, m_transparent.Offset); // Currently fails: likely 8
    }

    [Fact]
    public void CalculateStructMemberOffsets_BitfieldsAfterNormalMember()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        var service = new OffsetCalculationService(mockRepository);

        var structModel = new TypeModel
        {
            Id = 2,
            BaseName = "TestStruct",
            Type = TypeType.Struct
        };

        var normalMember = new StructMemberModel
        {
            Name = "Data",
            TypeString = "int",
            DeclarationOrder = 0
        };

        var bitfield1 = new StructMemberModel
        {
            Name = "Flag1",
            TypeString = "int",
            BitFieldWidth = 1,
            DeclarationOrder = 1
        };

        var bitfield2 = new StructMemberModel
        {
            Name = "Flag2",
            TypeString = "int",
            BitFieldWidth = 1,
            DeclarationOrder = 2
        };

        normalMember.StructTypeId = structModel.Id;
        bitfield1.StructTypeId = structModel.Id;
        bitfield2.StructTypeId = structModel.Id;

        mockRepository.InsertStructMember(normalMember);
        mockRepository.InsertStructMember(bitfield1);
        mockRepository.InsertStructMember(bitfield2);

        // Act
        service.CalculateStructMemberOffsets(structModel);

        // Assert
        Assert.Equal(0, normalMember.Offset);
        Assert.Equal(4, bitfield1.Offset); // Should start at 4
        Assert.Equal(4, bitfield2.Offset); // Should also be at 4 (packed), currently likely 8
    }
}
