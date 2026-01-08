using System.Collections.Generic;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Models;

public class EnumModelTests
{
    [Fact]
    public void CanCreateBasicEnum()
    {
        // Arrange
        var enumModel = new EnumTypeModel
        {
            Name = "TestEnum",
            Namespace = string.Empty
        };

        var members = new List<EnumMemberModel>
        {
            new EnumMemberModel { Name = "VALUE_A", Value = string.Empty, EnumTypeId = 1 },
            new EnumMemberModel { Name = "VALUE_B", Value = string.Empty, EnumTypeId = 1 },
            new EnumMemberModel { Name = "VALUE_C", Value = string.Empty, EnumTypeId = 1 }
        };

        // Act & Assert
        Assert.Equal("TestEnum", enumModel.Name);
        Assert.Equal("TestEnum", enumModel.FullyQualifiedName);
        Assert.Equal(TypeType.Enum, enumModel.Type);
    }
    
    [Fact]
    public void CanCreateEnumWithMembers()
    {
        // Arrange
        var enumModel = new EnumTypeModel
        {
            Name = "TestEnum",
            Namespace = "TestNamespace"
        };

        var members = new List<EnumMemberModel>
        {
            new EnumMemberModel { Name = "VALUE_A", Value = "0", EnumTypeId = 1 },
            new EnumMemberModel { Name = "VALUE_B", Value = "1", EnumTypeId = 1 },
            new EnumMemberModel { Name = "VALUE_C", Value = "2", EnumTypeId = 1 }
        };

        // Act & Assert
        Assert.Equal("TestEnum", enumModel.Name);
        Assert.Equal("TestNamespace", enumModel.Namespace);
        Assert.Equal("TestNamespace::TestEnum", enumModel.FullyQualifiedName);
        Assert.Equal(TypeType.Enum, enumModel.Type);
        
        Assert.Equal(3, members.Count);
        Assert.Equal("VALUE_A", members[0].Name);
        Assert.Equal("0", members[0].Value);
        Assert.Equal("VALUE_B", members[1].Name);
        Assert.Equal("1", members[1].Value);
        Assert.Equal("VALUE_C", members[2].Name);
        Assert.Equal("2", members[2].Value);
    }
}
