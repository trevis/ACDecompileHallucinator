using System.Collections.Generic;
using System.Linq;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Services;

public class OffsetCalculationIntegrationTests
{
    [Fact]
    public void CalculateAndApplyOffsets_SimpleStructWithMultipleMembers_CalculatesCorrectOffsets()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        
        // Create a simple struct with multiple members
        var structType = new TypeModel
        {
            Id = 1,
            BaseName = "SimpleStruct",
            Type = TypeType.Struct
        };
        
        // Create members: char (1 byte), int (4 bytes), short (2 bytes)
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Id = 1,
                Name = "charMember",
                TypeString = "char",
                StructTypeId = 1
            },
            new StructMemberModel
            {
                Id = 2,
                Name = "intMember", 
                TypeString = "int",
                StructTypeId = 1
            },
            new StructMemberModel
            {
                Id = 3,
                Name = "shortMember",
                TypeString = "short", 
                StructTypeId = 1
            }
        };
        
        // Add data to repository
        mockRepository.InsertType(structType);
        
        foreach (var member in members)
        {
            mockRepository.InsertStructMember(member);
        }
        
        // Add primitive types for the test
        var charType = new TypeModel { BaseName = "char", Type = TypeType.Primitive };
        var intType = new TypeModel { BaseName = "int", Type = TypeType.Primitive };
        var shortType = new TypeModel { BaseName = "short", Type = TypeType.Primitive };
        
        mockRepository.InsertType(charType);
        mockRepository.InsertType(intType);
        mockRepository.InsertType(shortType);
        
        var service = new OffsetCalculationService(mockRepository);
        
        // Act
        service.CalculateAndApplyOffsets();
        
        // Assert - members should have offsets: charMember=0, intMember=4 (aligned to 4-byte boundary), shortMember=8
        var updatedMembers = mockRepository.GetStructMembers(1);
        
        // The char member should be at offset 0
        var charMember = updatedMembers.First(m => m.Name == "charMember");
        Assert.Equal(0, charMember.Offset);
        
        // The int member should be at offset 4 (char at 0 takes 1 byte, aligned to 4-byte boundary)
        var intMember = updatedMembers.First(m => m.Name == "intMember");
        Assert.Equal(4, intMember.Offset);
        
        // The short member should be at offset 8 (int at 4 takes 4 bytes)
        var shortMember = updatedMembers.First(m => m.Name == "shortMember");
        Assert.Equal(8, shortMember.Offset);
    }
    
    [Fact]
    public void CalculateAndApplyOffsets_StructWithPointers_CalculatesCorrectPointerOffsets()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        
        // Create a struct with pointer members
        var structType = new TypeModel
        {
            Id = 1,
            BaseName = "PointerStruct",
            Type = TypeType.Struct
        };
        
        // Create members: int pointer (4 bytes), char pointer (4 bytes)
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Id = 1,
                Name = "intPtr",
                TypeString = "int*",
                StructTypeId = 1,
                TypeReference = new TypeReference
                {
                    TypeString = "int*",
                    IsPointer = true,
                    PointerDepth = 1
                }
            },
            new StructMemberModel
            {
                Id = 2,
                Name = "charPtr",
                TypeString = "char*",
                StructTypeId = 1,
                TypeReference = new TypeReference
                {
                    TypeString = "char*",
                    IsPointer = true,
                    PointerDepth = 1
                }
            }
        };
        
        // Add data to repository
        mockRepository.InsertType(structType);
        
        foreach (var member in members)
        {
            mockRepository.InsertStructMember(member);
        }
        
        var service = new OffsetCalculationService(mockRepository);
        
        // Act
        service.CalculateAndApplyOffsets();
        
        // Assert - both pointers should be 4 bytes each, so offsets should be 0 and 4
        var updatedMembers = mockRepository.GetStructMembers(1);
        
        var intPtrMember = updatedMembers.First(m => m.Name == "intPtr");
        Assert.Equal(0, intPtrMember.Offset);
        
        var charPtrMember = updatedMembers.First(m => m.Name == "charPtr");
        Assert.Equal(4, charPtrMember.Offset);
    }
    
    [Fact]
    public void CalculateAndApplyOffsets_StructWithArrays_CalculatesCorrectArrayOffsets()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        
        // Create a struct with array members
        var structType = new TypeModel
        {
            Id = 1,
            BaseName = "ArrayStruct",
            Type = TypeType.Struct
        };
        
        // Create members: char[5] (5 bytes), int[2] (8 bytes)
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Id = 1,
                Name = "charArray",
                TypeString = "char",
                StructTypeId = 1,
                TypeReference = new TypeReference
                {
                    TypeString = "char",
                    IsArray = true,
                    ArraySize = 5
                }
            },
            new StructMemberModel
            {
                Id = 2,
                Name = "intArray",
                TypeString = "int",
                StructTypeId = 1,
                TypeReference = new TypeReference
                {
                    TypeString = "int",
                    IsArray = true,
                    ArraySize = 2
                }
            }
        };
        
        // Add data to repository
        mockRepository.InsertType(structType);
        
        foreach (var member in members)
        {
            mockRepository.InsertStructMember(member);
        }
        
        // Add primitive types for the test
        var charType = new TypeModel { BaseName = "char", Type = TypeType.Primitive };
        var intType = new TypeModel { BaseName = "int", Type = TypeType.Primitive };
        
        mockRepository.InsertType(charType);
        mockRepository.InsertType(intType);
        
        var service = new OffsetCalculationService(mockRepository);
        
        // Act
        service.CalculateAndApplyOffsets();
        
        // Assert - char[5] at offset 0 (size 5), int[2] at offset 8 (aligned to 4-byte boundary after size 5)
        var updatedMembers = mockRepository.GetStructMembers(1);
        
        var charArrayMember = updatedMembers.First(m => m.Name == "charArray");
        Assert.Equal(0, charArrayMember.Offset);
        
        var intArrayMember = updatedMembers.First(m => m.Name == "intArray");
        Assert.Equal(8, intArrayMember.Offset); // 5 bytes + 3 padding to align to 4-byte boundary
    }
}
