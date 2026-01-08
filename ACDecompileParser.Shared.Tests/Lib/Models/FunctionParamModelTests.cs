using System;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Tests.Lib.Models;

public class FunctionParamModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var functionParam = new FunctionParamModel();

        // Assert
        Assert.Equal(string.Empty, functionParam.ParameterType);
        Assert.Equal(0, functionParam.Position);
        Assert.Null(functionParam.ParentFunctionSignatureId);
        Assert.Null(functionParam.NestedFunctionSignatureId);
        Assert.Null(functionParam.TypeReferenceId);
        Assert.Null(functionParam.TypeReference);
        Assert.Null(functionParam.NestedFunctionSignature);
        Assert.Equal(string.Empty, functionParam.Name);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var typeReference = new TypeReference { Id = 1, TypeString = "int" };

        var functionParam = new FunctionParamModel
        {
            Name = "param1",
            ParameterType = "int",
            Position = 2,
            ParentFunctionSignatureId = 456,
            TypeReferenceId = 789,
            TypeReference = typeReference
        };

        // Assert
        Assert.Equal("param1", functionParam.Name);
        Assert.Equal("int", functionParam.ParameterType);
        Assert.Equal(2, functionParam.Position);
        Assert.Equal(456, functionParam.ParentFunctionSignatureId);
        Assert.Equal(789, functionParam.TypeReferenceId);
        Assert.NotNull(functionParam.TypeReference);
        Assert.Equal(1, functionParam.TypeReference.Id);
    }

    [Fact]
    public void ParameterType_Property_DefaultsToEmptyString()
    {
        // Act
        var functionParam = new FunctionParamModel();

        // Assert
        Assert.Equal(string.Empty, functionParam.ParameterType);
    }

    [Fact]
    public void ParameterType_Property_CanBeSet()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            ParameterType = "const char*"
        };

        // Assert
        Assert.Equal("const char*", functionParam.ParameterType);
    }

    [Fact]
    public void Position_Property_DefaultsToZero()
    {
        // Act
        var functionParam = new FunctionParamModel();

        // Assert
        Assert.Equal(0, functionParam.Position);
    }

    [Fact]
    public void Position_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            Position = 5
        };

        // Assert
        Assert.Equal(5, functionParam.Position);
    }

    [Fact]
    public void TypeReferenceId_Property_CanBeNull()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            TypeReferenceId = null
        };

        // Assert
        Assert.Null(functionParam.TypeReferenceId);
    }

    [Fact]
    public void TypeReferenceId_Property_CanBeSetToPositiveValue()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            TypeReferenceId = 777
        };

        // Assert
        Assert.Equal(777, functionParam.TypeReferenceId);
    }

    [Fact]
    public void TypeReference_Property_CanBeNull()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            TypeReference = null
        };

        // Assert
        Assert.Null(functionParam.TypeReference);
    }

    [Fact]
    public void TypeReference_Property_CanBeSet()
    {
        // Arrange
        var typeReference = new TypeReference { Id = 42, TypeString = "float" };
        var functionParam = new FunctionParamModel
        {
            TypeReference = typeReference
        };

        // Assert
        Assert.NotNull(functionParam.TypeReference);
        Assert.Equal(42, functionParam.TypeReference.Id);
        Assert.Equal("float", functionParam.TypeReference.TypeString);
    }

    [Fact]
    public void InheritanceFromBaseMemberModel()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            Name = "testParam",
            ParameterType = "int"
        };

        // Assert - Should inherit Name property from BaseMemberModel
        Assert.Equal("testParam", functionParam.Name);
    }

    [Fact]
    public void ParentFunctionSignatureId_Property_DefaultsToNull()
    {
        // Act
        var functionParam = new FunctionParamModel();

        // Assert
        Assert.Null(functionParam.ParentFunctionSignatureId);
    }

    [Fact]
    public void ParentFunctionSignatureId_Property_CanBeSet()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            ParentFunctionSignatureId = 100
        };

        // Assert
        Assert.Equal(100, functionParam.ParentFunctionSignatureId);
    }

    [Fact]
    public void NestedFunctionSignatureId_Property_DefaultsToNull()
    {
        // Act
        var functionParam = new FunctionParamModel();

        // Assert
        Assert.Null(functionParam.NestedFunctionSignatureId);
    }

    [Fact]
    public void NestedFunctionSignatureId_Property_CanBeSet()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            NestedFunctionSignatureId = 200
        };

        // Assert
        Assert.Equal(200, functionParam.NestedFunctionSignatureId);
    }

    [Fact]
    public void NestedFunctionSignature_Property_CanBeNull()
    {
        // Arrange
        var functionParam = new FunctionParamModel
        {
            NestedFunctionSignature = null
        };

        // Assert
        Assert.Null(functionParam.NestedFunctionSignature);
    }

    [Fact]
    public void NestedFunctionSignature_Property_CanBeSet()
    {
        // Arrange
        var funcSig = new FunctionSignatureModel
        {
            Id = 42,
            Name = "testSig",
            ReturnType = "int",
            CallingConvention = "__cdecl"
        };
        var functionParam = new FunctionParamModel
        {
            NestedFunctionSignature = funcSig
        };

        // Assert
        Assert.NotNull(functionParam.NestedFunctionSignature);
        Assert.Equal(42, functionParam.NestedFunctionSignature.Id);
        Assert.Equal("testSig", functionParam.NestedFunctionSignature.Name);
    }

    [Fact]
    public void SignatureParam_CanHaveParentFunctionSignatureIdSet()
    {
        // Arrange - Test that params belonging to function signatures work
        var functionParam = new FunctionParamModel
        {
            ParameterType = "int",
            Name = "value",
            ParentFunctionSignatureId = 456
        };

        // Assert
        Assert.Equal(456, functionParam.ParentFunctionSignatureId);
    }
}
