using System;
using System.Collections.Generic;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Tests.Lib.Models;

public class StructMemberModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var structMember = new StructMemberModel();

        // Assert
        Assert.Equal(string.Empty, structMember.TypeString);
        Assert.Null(structMember.Offset);
        Assert.Equal(0, structMember.StructTypeId);
        Assert.Null(structMember.TypeReferenceId);
        Assert.Null(structMember.BitFieldWidth);
        Assert.False(structMember.IsFunctionPointer);
        Assert.Null(structMember.FunctionSignatureId);
        Assert.Null(structMember.ParentType);
        Assert.Null(structMember.TypeReference);
        Assert.Null(structMember.FunctionSignature);
        Assert.Equal(string.Empty, structMember.Name);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var typeModel = new TypeModel { BaseName = "int" };
        var typeReference = new TypeReference { Id = 1, TypeString = "int" };
        var functionParam = new FunctionParamModel { Name = "param1", ParameterType = "int" };
        var functionSignature = new FunctionSignatureModel
        {
            Id = 101,
            Name = "__sig_test",
            ReturnType = "int",
            CallingConvention = "__cdecl",
            Parameters = new List<FunctionParamModel> { functionParam }
        };

        var structMember = new StructMemberModel
        {
            Name = "myMember",
            TypeString = "int",
            Offset = 16,
            StructTypeId = 123,
            TypeReferenceId = 789,
            IsFunctionPointer = true,
            FunctionSignatureId = 101,
            FunctionSignature = functionSignature,
            ParentType = typeModel,
            TypeReference = typeReference
        };

        // Assert
        Assert.Equal("myMember", structMember.Name);
        Assert.Equal("int", structMember.TypeString);
        Assert.Equal(16, structMember.Offset);
        Assert.Equal(123, structMember.StructTypeId);
        Assert.Equal(789, structMember.TypeReferenceId);
        Assert.True(structMember.IsFunctionPointer);
        Assert.Equal(101, structMember.FunctionSignatureId);
        Assert.NotNull(structMember.FunctionSignature);
        Assert.Equal("__cdecl", structMember.FunctionSignature.CallingConvention);
        Assert.Single(structMember.FunctionSignature.Parameters);
        Assert.Equal("param1", structMember.FunctionSignature.Parameters[0].Name);
        Assert.NotNull(structMember.ParentType);
        Assert.Equal("int", structMember.ParentType.BaseName);
        Assert.NotNull(structMember.TypeReference);
    }

    [Fact]
    public void TypeString_Property_DefaultsToEmptyString()
    {
        // Act
        var structMember = new StructMemberModel();

        // Assert
        Assert.Equal(string.Empty, structMember.TypeString);
    }

    [Fact]
    public void TypeString_Property_CanBeSet()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            TypeString = "MyClass*"
        };

        // Assert
        Assert.Equal("MyClass*", structMember.TypeString);
    }

    [Fact]
    public void Offset_Property_CanBeNull()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            Offset = null
        };

        // Assert
        Assert.Null(structMember.Offset);
    }

    [Fact]
    public void Offset_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            Offset = 32
        };

        // Assert
        Assert.Equal(32, structMember.Offset);
    }

    [Fact]
    public void StructTypeId_Property_DefaultsToZero()
    {
        // Act
        var structMember = new StructMemberModel();

        // Assert
        Assert.Equal(0, structMember.StructTypeId);
    }

    [Fact]
    public void StructTypeId_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            StructTypeId = 999
        };

        // Assert
        Assert.Equal(999, structMember.StructTypeId);
    }


    [Fact]
    public void TypeReferenceId_Property_CanBeNull()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            TypeReferenceId = null
        };

        // Assert
        Assert.Null(structMember.TypeReferenceId);
    }

    [Fact]
    public void TypeReferenceId_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            TypeReferenceId = 777
        };

        // Assert
        Assert.Equal(777, structMember.TypeReferenceId);
    }

    [Fact]
    public void IsFunctionPointer_Property_DefaultsToFalse()
    {
        // Act
        var structMember = new StructMemberModel();

        // Assert
        Assert.False(structMember.IsFunctionPointer);
    }

    [Fact]
    public void IsFunctionPointer_Property_CanBeSetToTrue()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            IsFunctionPointer = true
        };

        // Assert
        Assert.True(structMember.IsFunctionPointer);
    }

    [Fact]
    public void FunctionSignatureId_Property_CanBeNull()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            FunctionSignatureId = null
        };

        // Assert
        Assert.Null(structMember.FunctionSignatureId);
    }

    [Fact]
    public void FunctionSignatureId_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            FunctionSignatureId = 202
        };

        // Assert
        Assert.Equal(202, structMember.FunctionSignatureId);
    }

    [Fact]
    public void FunctionSignature_Property_CanBeSet()
    {
        // Arrange
        var functionSignature = new FunctionSignatureModel
        {
            Name = "__sig_test",
            ReturnType = "int",
            CallingConvention = "__stdcall",
            Parameters = new List<FunctionParamModel>
            {
                new FunctionParamModel { Name = "a", ParameterType = "int" },
                new FunctionParamModel { Name = "b", ParameterType = "float" }
            }
        };

        var structMember = new StructMemberModel
        {
            FunctionSignature = functionSignature
        };

        // Assert
        Assert.NotNull(structMember.FunctionSignature);
        Assert.Equal("__stdcall", structMember.FunctionSignature.CallingConvention);
        Assert.Equal(2, structMember.FunctionSignature.Parameters.Count);
        Assert.Equal("a", structMember.FunctionSignature.Parameters[0].Name);
        Assert.Equal("int", structMember.FunctionSignature.Parameters[0].ParameterType);
        Assert.Equal("b", structMember.FunctionSignature.Parameters[1].Name);
        Assert.Equal("float", structMember.FunctionSignature.Parameters[1].ParameterType);
    }

    [Fact]
    public void NavigationProperties_CanBeNull()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            ParentType = null,
            TypeReference = null,
            FunctionSignature = null
        };

        // Assert
        Assert.Null(structMember.ParentType);
        Assert.Null(structMember.TypeReference);
        Assert.Null(structMember.FunctionSignature);
    }

    [Fact]
    public void InheritanceFromBaseMemberModel()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            Name = "testMember",
            TypeString = "int",
            Offset = 8
        };

        // Assert - Should inherit Name property from BaseMemberModel
        Assert.Equal("testMember", structMember.Name);
    }

    #region BitFieldWidth Tests

    [Fact]
    public void BitFieldWidth_Property_DefaultsToNull()
    {
        // Act
        var structMember = new StructMemberModel();

        // Assert
        Assert.Null(structMember.BitFieldWidth);
    }

    [Fact]
    public void BitFieldWidth_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            BitFieldWidth = 1
        };

        // Assert
        Assert.Equal(1, structMember.BitFieldWidth);
    }

    [Fact]
    public void BitFieldWidth_Property_CanBeSetToLargerValue()
    {
        // Arrange - for a 30-bit field like "unsigned __int32 Reserved : 30;"
        var structMember = new StructMemberModel
        {
            BitFieldWidth = 30
        };

        // Assert
        Assert.Equal(30, structMember.BitFieldWidth);
    }

    [Fact]
    public void BitFieldWidth_Property_CanBeNull()
    {
        // Arrange
        var structMember = new StructMemberModel
        {
            BitFieldWidth = null
        };

        // Assert
        Assert.Null(structMember.BitFieldWidth);
    }

    #endregion
}
