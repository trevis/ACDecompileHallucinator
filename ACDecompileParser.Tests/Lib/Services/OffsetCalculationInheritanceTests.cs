using System.Collections.Generic;
using System.Linq;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Services;

public class OffsetCalculationInheritanceTests
{
    [Fact]
    public void CalculateAndApplyOffsets_StructWithBaseClass_CalculatesOffsetsIncludingBaseClassSize()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        
        // Create a base class: BaseStruct with char (1 byte) and int (4 bytes) = 8 bytes total with alignment
        var baseStructType = new TypeModel
        {
            Id = 1,
            BaseName = "BaseStruct",
            Type = TypeType.Struct
        };
        
        var baseMembers = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Id = 1,
                Name = "baseChar",
                TypeString = "char",
                StructTypeId = 1
            },
            new StructMemberModel
            {
                Id = 2,
                Name = "baseInt",
                TypeString = "int",
                StructTypeId = 1
            }
        };
        
        // Create a derived struct that inherits from BaseStruct
        var derivedStructType = new TypeModel
        {
            Id = 2,
            BaseName = "DerivedStruct",
            Type = TypeType.Struct
        };
        
        var derivedMembers = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Id = 3,
                Name = "derivedShort",
                TypeString = "short",
                StructTypeId = 2
            }
        };
        
        // Add types and members to the repository
        mockRepository.InsertType(baseStructType);
        mockRepository.InsertType(derivedStructType);
        
        foreach (var member in baseMembers)
        {
            mockRepository.InsertStructMember(member);
        }
        
        foreach (var member in derivedMembers)
        {
            mockRepository.InsertStructMember(member);
        }
        
        // Create inheritance relationship - make sure RelatedType is populated
        var inheritance = new TypeInheritance
        {
            ParentTypeId = 2, // DerivedStruct ID (the type that inherits)
            RelatedTypeId = 1, // BaseStruct ID (the base type)
            RelatedType = baseStructType // Set the actual related type
        };
        
        mockRepository.InsertTypeInheritance(inheritance);
        
        // Also add the primitive types that are needed for offset calculation
        var charType = new TypeModel { BaseName = "char", Type = TypeType.Primitive };
        var intType = new TypeModel { BaseName = "int", Type = TypeType.Primitive };
        var shortType = new TypeModel { BaseName = "short", Type = TypeType.Primitive };
        
        mockRepository.InsertType(charType);
        mockRepository.InsertType(intType);
        mockRepository.InsertType(shortType);
        
        var service = new OffsetCalculationService(mockRepository);
        
        // Act
        service.CalculateAndApplyOffsets();
        
        // Assert
        // BaseStruct: char at offset 0 (size 1), int at offset 4 (aligned to 4-byte boundary after char)
        // BaseStruct total size: 8 bytes (char at 0-0, padding 1-3, int at 4-7)
        // DerivedStruct: derivedShort should start at offset 8 (after base class)
        var updatedMembers = mockRepository.GetStructMembers(2);
        var derivedShortMember = updatedMembers.FirstOrDefault(m => m.Name == "derivedShort");
        Assert.NotNull(derivedShortMember);
        Assert.Equal(8, derivedShortMember.Offset);
    }
}
