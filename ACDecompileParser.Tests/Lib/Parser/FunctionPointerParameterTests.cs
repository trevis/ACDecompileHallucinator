using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Utilities;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class FunctionPointerParameterTests
{
    #region Function Pointer Parameter Detection Tests

    [Fact]
    public void IsFunctionPointerParameter_DetectsFunctionPointerType()
    {
        // Arrange
        var paramString = "HRESULT (__cdecl *)(const unsigned __int16 *)";

        // Act
        var result = ParsingUtilities.IsFunctionPointerParameter(paramString);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFunctionPointerParameter_DetectsFunctionPointerWithNoCallingConvention()
    {
        // Arrange
        var paramString = "void (*)(int, int)";

        // Act
        var result = ParsingUtilities.IsFunctionPointerParameter(paramString);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFunctionPointerParameter_ReturnsFalseForRegularType()
    {
        // Arrange
        var paramString = "const unsigned __int16 *";

        // Act
        var result = ParsingUtilities.IsFunctionPointerParameter(paramString);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFunctionPointerParameter_ReturnsFalseForPointerType()
    {
        // Arrange
        var paramString = "IKeystoneDocument *this";

        // Act
        var result = ParsingUtilities.IsFunctionPointerParameter(paramString);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Function Pointer Parameter Info Extraction Tests

    [Fact]
    public void ExtractFunctionPointerParameterInfo_ExtractsReturnType()
    {
        // Arrange
        var paramString = "HRESULT (__cdecl *)(const unsigned __int16 *)";

        // Act
        var (returnType, callingConvention, parameters, name, _) =
            ParsingUtilities.ExtractFunctionPointerParameterInfo(paramString);

        // Assert
        Assert.Equal("HRESULT", returnType);
        Assert.Equal("__cdecl", callingConvention);
        Assert.Equal("const unsigned __int16 *", parameters);
    }

    [Fact]
    public void ExtractFunctionPointerParameterInfo_ExtractsEmptyCallingConvention()
    {
        // Arrange
        var paramString = "void (*)(int, int)";

        // Act
        var (returnType, callingConvention, parameters, name, _) =
            ParsingUtilities.ExtractFunctionPointerParameterInfo(paramString);

        // Assert
        Assert.Equal("void", returnType);
        Assert.True(string.IsNullOrEmpty(callingConvention));
        Assert.Equal("int, int", parameters);
    }

    [Fact]
    public void ExtractFunctionPointerParameterInfo_ExtractsMultipleParameters()
    {
        // Arrange
        var paramString = "unsigned __int16 *(__cdecl *)(const unsigned __int16 *)";

        // Act
        var (returnType, callingConvention, parameters, name, _) =
            ParsingUtilities.ExtractFunctionPointerParameterInfo(paramString);

        // Assert
        Assert.Equal("unsigned __int16 *", returnType);
        Assert.Equal("__cdecl", callingConvention);
        Assert.Equal("const unsigned __int16 *", parameters);
    }

    [Fact]
    public void ExtractFunctionPointerParameterInfo_ReturnsNullForNonFunctionPointer()
    {
        // Arrange
        var paramString = "int value";

        // Act
        var (returnType, callingConvention, parameters, name, _) =
            ParsingUtilities.ExtractFunctionPointerParameterInfo(paramString);

        // Assert
        Assert.Null(returnType);
        Assert.Null(callingConvention);
        Assert.Null(parameters);
    }

    #endregion

    #region Function Parameter Parser Tests

    [Fact]
    public void ParseFunctionParameters_ParsesFunctionPointerParameter()
    {
        // Arrange - From SetSoundCallback: HRESULT (__cdecl *)(const unsigned __int16 *)
        var paramString = "IKeystoneDocument *this, HRESULT (__cdecl *)(const unsigned __int16 *)";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(2, result.Count);

        // First parameter is regular pointer
        Assert.Equal("IKeystoneDocument*", result[0].ParameterType);
        Assert.Equal("this", result[0].Name);
        Assert.False(result[0].IsFunctionPointerType);

        // Second parameter is function pointer
        Assert.True(result[1].IsFunctionPointerType);
        Assert.NotNull(result[1].NestedFunctionSignature);
        Assert.Equal("HRESULT", result[1].NestedFunctionSignature!.ReturnType);
        Assert.Equal("__cdecl", result[1].NestedFunctionSignature!.CallingConvention);
    }

    [Fact]
    public void ParseFunctionParameters_ParsesMultipleFunctionPointerParameters()
    {
        // Arrange - Use the format found in decompiled code (unnamed function pointers)
        var paramString = "void (*)(int), void (*)(float)";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.True(p.IsFunctionPointerType));
    }

    [Fact]
    public void ParseFunctionParameters_ParsesSetASPCallbackParameter()
    {
        // Arrange - From SetASPCallback: unsigned __int16 *(__cdecl *)(const unsigned __int16 *)
        var paramString = "IKeystoneDocument *this, unsigned __int16 *(__cdecl *)(const unsigned __int16 *)";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(2, result.Count);

        // First parameter is regular pointer
        Assert.False(result[0].IsFunctionPointerType);

        // Second parameter is function pointer with pointer return type
        Assert.True(result[1].IsFunctionPointerType);
        Assert.NotNull(result[1].NestedFunctionSignature);
        // The return type may contain pointer info
        Assert.Contains("unsigned __int16", result[1].NestedFunctionSignature!.ReturnType);
    }

    #endregion
}
