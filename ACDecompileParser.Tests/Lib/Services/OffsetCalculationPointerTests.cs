using System;
using System.Collections.Generic;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Services
{
    public class OffsetCalculationPointerTests
    {
        private readonly Mock<ITypeRepository> _repositoryMock;
        private readonly OffsetCalculationService _service;

        public OffsetCalculationPointerTests()
        {
            _repositoryMock = new Mock<ITypeRepository>();
            _service = new OffsetCalculationService(_repositoryMock.Object);
        }

        [Fact]
        public void GetSizeFromTypeString_PointerType_Returns4()
        {
            // Act
            int size = _service.GetSizeFromTypeString("char*");

            // Assert
            Assert.Equal(4, size);
        }

        [Fact]
        public void CalculateMemberSize_ArrayOfPointers_ReturnsCorrectSize()
        {
            // Arrange
            // Array of 10 pointers to char. Should be 40 bytes.
            // If bug exists, it calculates as 10 * sizeof(char) = 10 bytes.
            var member = new StructMemberModel
            {
                TypeReference = new TypeReference
                {
                    IsArray = true,
                    ArraySize = 10,
                    TypeString = "char*"
                }
            };

            // Act
            int size = _service.CalculateMemberSize(member);

            // Assert
            Assert.Equal(40, size);
        }
    }
}
