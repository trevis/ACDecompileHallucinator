using System.Collections.Generic;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Services;

public class OffsetCalculationServiceTests
{
    [Fact]
    public void AlignOffset_AlignsToBoundaryCorrectly()
    {
        // Arrange - Create a simple repository mock for testing
        var mockRepository = new TestTypeRepository();
        var service = new OffsetCalculationService(mockRepository);

        // Act
        var alignedOffset = service.AlignOffset(5, 4);

        // Assert
        Assert.Equal(8, alignedOffset); // 5 aligned to 4-byte boundary should be 8
    }

    [Fact]
    public void CalculateMemberSize_Pointer_ReturnsCorrectSize()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        var service = new OffsetCalculationService(mockRepository);

        var member = new StructMemberModel
        {
            TypeString = "int*",
            TypeReference = new TypeReference
            {
                TypeString = "int*",
                IsPointer = true,
                PointerDepth = 1
            }
        };

        // Act
        var size = service.CalculateMemberSize(member);

        // Assert
        Assert.Equal(4, size); // Pointers should be 4 bytes on x86
    }

    [Fact]
    public void CalculateMemberSize_Array_ReturnsCorrectSize()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        var service = new OffsetCalculationService(mockRepository);

        var member = new StructMemberModel
        {
            TypeString = "char",
            TypeReference = new TypeReference
            {
                TypeString = "char",
                IsArray = true,
                ArraySize = 10
            }
        };

        // Act
        var size = service.CalculateMemberSize(member);

        // Assert
        Assert.Equal(10, size); // char[10] should be 10 bytes
    }

    [Fact]
    public void CalculateMemberSize_FunctionPointer_ReturnsCorrectSize()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        var service = new OffsetCalculationService(mockRepository);

        var member = new StructMemberModel
        {
            TypeString = "int",
            IsFunctionPointer = true
        };

        // Act
        var size = service.CalculateMemberSize(member);

        // Assert
        Assert.Equal(4, size); // Function pointers should be 4 bytes on x86
    }

    [Fact]
    public void CalculateMemberSize_MultiLevelPointer_ReturnsCorrectSize()
    {
        // Arrange
        var mockRepository = new TestTypeRepository();
        var service = new OffsetCalculationService(mockRepository);

        var member = new StructMemberModel
        {
            TypeString = "int**",
            TypeReference = new TypeReference
            {
                TypeString = "int**",
                IsPointer = true,
                PointerDepth = 2
            }
        };

        // Act
        var size = service.CalculateMemberSize(member);

        // Assert
        Assert.Equal(4, size); // int** is still just a pointer (4 bytes)
    }
}
