using System;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Tests.Lib.Models;

// Create a concrete implementation of BaseMemberModel for testing purposes
public class TestMemberModel : BaseMemberModel
{
    public string TestProperty { get; set; } = string.Empty;
}

public class BaseMemberModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var member = new TestMemberModel();

        // Assert
        Assert.Equal(0, member.Id);
        Assert.Equal(string.Empty, member.Name);
        Assert.Null(member.ParentType);
    }

    [Fact]
    public void Id_Property_DefaultsToZero()
    {
        // Act
        var member = new TestMemberModel();

        // Assert
        Assert.Equal(0, member.Id);
    }

    [Fact]
    public void Id_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var member = new TestMemberModel
        {
            Id = 42
        };

        // Assert
        Assert.Equal(42, member.Id);
    }

    [Fact]
    public void Name_Property_DefaultsToEmptyString()
    {
        // Act
        var member = new TestMemberModel();

        // Assert
        Assert.Equal(string.Empty, member.Name);
    }

    [Fact]
    public void Name_Property_CanBeSet()
    {
        // Arrange
        var member = new TestMemberModel
        {
            Name = "TestMember"
        };

        // Assert
        Assert.Equal("TestMember", member.Name);
    }

    [Fact]
    public void ParentType_Property_CanBeNull()
    {
        // Arrange
        var member = new TestMemberModel
        {
            ParentType = null
        };

        // Assert
        Assert.Null(member.ParentType);
    }

    [Fact]
    public void ParentType_Property_CanBeSet()
    {
        // Arrange
        var typeModel = new TypeModel { BaseName = "TestClass" };
        var member = new TestMemberModel
        {
            ParentType = typeModel
        };

        // Assert
        Assert.NotNull(member.ParentType);
        Assert.Equal("TestClass", member.ParentType.BaseName);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var typeModel = new TypeModel { BaseName = "ParentType" };
        var member = new TestMemberModel
        {
            Id = 99,
            Name = "MyMember",
            ParentType = typeModel
        };

        // Assert
        Assert.Equal(99, member.Id);
        Assert.Equal("MyMember", member.Name);
        Assert.NotNull(member.ParentType);
        Assert.Equal("ParentType", member.ParentType.BaseName);
    }
}
