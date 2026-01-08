using System;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Tests.Lib.Models;

public class EnumMemberModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var enumMember = new EnumMemberModel();

        // Assert
        Assert.Equal(string.Empty, enumMember.Value);
        Assert.Equal(0, enumMember.EnumTypeId);
        Assert.Equal(string.Empty, enumMember.Name);
        Assert.Null(enumMember.ParentType);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var enumMember = new EnumMemberModel
        {
            Name = "TestMember",
            Value = "42",
            EnumTypeId = 123,
            ParentType = new TypeModel { BaseName = "TestEnum" }
        };

        // Assert
        Assert.Equal("TestMember", enumMember.Name);
        Assert.Equal("42", enumMember.Value);
        Assert.Equal(123, enumMember.EnumTypeId);
        Assert.NotNull(enumMember.ParentType);
        Assert.Equal("TestEnum", enumMember.ParentType.BaseName);
    }

    [Fact]
    public void Value_Property_DefaultsToEmptyString()
    {
        // Act
        var enumMember = new EnumMemberModel();

        // Assert
        Assert.Equal(string.Empty, enumMember.Value);
    }

    [Fact]
    public void Value_Property_CanBeSetToEmpty()
    {
        // Arrange
        var enumMember = new EnumMemberModel
        {
            Value = string.Empty
        };

        // Assert
        Assert.Equal(string.Empty, enumMember.Value);
    }

    [Fact]
    public void Value_Property_CanBeSetToNumericValue()
    {
        // Arrange
        var enumMember = new EnumMemberModel
        {
            Value = "100"
        };

        // Assert
        Assert.Equal("100", enumMember.Value);
    }

    [Fact]
    public void Value_Property_CanBeSetToHexValue()
    {
        // Arrange
        var enumMember = new EnumMemberModel
        {
            Value = "0xFF"
        };

        // Assert
        Assert.Equal("0xFF", enumMember.Value);
    }

    [Fact]
    public void Value_Property_CanBeSetToComplexExpression()
    {
        // Arrange
        var enumMember = new EnumMemberModel
        {
            Value = "(1 << 2)"
        };

        // Assert
        Assert.Equal("(1 << 2)", enumMember.Value);
    }

    [Fact]
    public void EnumTypeId_Property_DefaultsToZero()
    {
        // Act
        var enumMember = new EnumMemberModel();

        // Assert
        Assert.Equal(0, enumMember.EnumTypeId);
    }

    [Fact]
    public void EnumTypeId_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var enumMember = new EnumMemberModel
        {
            EnumTypeId = 42
        };

        // Assert
        Assert.Equal(42, enumMember.EnumTypeId);
    }

    [Fact]
    public void ParentType_Property_CanBeNull()
    {
        // Arrange
        var enumMember = new EnumMemberModel
        {
            ParentType = null
        };

        // Assert
        Assert.Null(enumMember.ParentType);
    }

    [Fact]
    public void InheritanceFromBaseMemberModel()
    {
        // Arrange
        var enumMember = new EnumMemberModel
        {
            Name = "TestMember",
            Value = "100",
            EnumTypeId = 5
        };

        // Assert - Should inherit Name property from BaseMemberModel
        Assert.Equal("TestMember", enumMember.Name);
    }
}
