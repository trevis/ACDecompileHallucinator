using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class TypedefParserTests
{
    [Fact]
    public void ParseTypedefs_SimpleTypedef_ParsesCorrectly()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 10423 */",
            "typedef unsigned __int16 flowqueueInterval_t;"
        };

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("flowqueueInterval_t", result[0].Name);
        Assert.Equal(string.Empty, result[0].Namespace);
        Assert.NotNull(result[0].TypeReference);
        Assert.Contains("unsigned __int16", result[0].TypeReference?.TypeString ?? string.Empty);
    }

    [Fact]
    public void ParseTypedefs_PointerTypedef_ParsesCorrectly()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 10414 */",
            "typedef void *RPC_IF_HANDLE;"
        };

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("RPC_IF_HANDLE", result[0].Name);
        Assert.Equal(TypeType.Typedef, TypeType.Typedef);
        Assert.NotNull(result[0].TypeReference);
        Assert.True(result[0].TypeReference?.IsPointer ?? false);
        Assert.Equal(1, result[0].TypeReference?.PointerDepth ?? 0);
        Assert.Contains("void", result[0].TypeReference?.TypeString ?? string.Empty);
    }

    [Fact]
    public void ParseTypedefs_MultiPointerTypedef_ParsesCorrectly()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 7942 */",
            "typedef HKEY__ **PHKEY;"
        };

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("PHKEY", result[0].Name);
        Assert.Equal(TypeType.Typedef, TypeType.Typedef);
        Assert.NotNull(result[0].TypeReference);
        Assert.True(result[0].TypeReference?.IsPointer ?? false);
        Assert.Equal(2, result[0].TypeReference?.PointerDepth ?? 0);
        Assert.Contains("HKEY__", result[0].TypeReference?.TypeString ?? string.Empty);
    }

    [Fact]
    public void ParseTypedefs_FunctionPointerTypedef_ParsesCorrectly()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 7991 */",
            "typedef int (__stdcall *FARPROC)();"
        };

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("FARPROC", result[0].Name);
        Assert.Equal(TypeType.Typedef, TypeType.Typedef);
        Assert.NotNull(result[0].FunctionSignature);
        Assert.Equal("FARPROC", result[0].FunctionSignature?.Name ?? string.Empty);
        Assert.Equal("int", result[0].FunctionSignature?.ReturnType ?? string.Empty);
        Assert.Equal("__stdcall", result[0].FunctionSignature?.CallingConvention ?? string.Empty);
        Assert.Empty(result[0].FunctionSignature?.Parameters ?? new List<FunctionParamModel>());
    }

    [Fact]
    public void ParseTypedefs_FunctionPointerWithParams_ParsesCorrectly()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 7945 */",
            "typedef int (__stdcall *VERIFY_SIGNATURE_FN)(_SecHandle *, _SecBufferDesc *, unsigned int, unsigned int *);"
        };

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Single(result);
        var typedef = result[0];
        Assert.Equal("VERIFY_SIGNATURE_FN", typedef.Name);
        Assert.Equal(TypeType.Typedef, TypeType.Typedef);
        Assert.NotNull(typedef.FunctionSignature);
        Assert.Equal("VERIFY_SIGNATURE_FN", typedef.FunctionSignature!.Name);
        Assert.Equal("int", typedef.FunctionSignature.ReturnType);
        Assert.Equal("__stdcall", typedef.FunctionSignature.CallingConvention);
        Assert.Equal(4, typedef.FunctionSignature.Parameters.Count);

        // Verify first parameter
        Assert.Contains("_SecHandle", typedef.FunctionSignature.Parameters[0].ParameterType);
        Assert.True(typedef.FunctionSignature!.Parameters[0].TypeReference?.IsPointer);

        // Verify second parameter
        Assert.Contains("_SecBufferDesc", typedef.FunctionSignature.Parameters[1].ParameterType);
        Assert.True(typedef.FunctionSignature!.Parameters[1].TypeReference?.IsPointer);

        // Verify third parameter
        Assert.Contains("unsigned int", typedef.FunctionSignature.Parameters[2].ParameterType);

        // Verify fourth parameter
        Assert.Contains("unsigned int", typedef.FunctionSignature.Parameters[3].ParameterType);
        Assert.True(typedef.FunctionSignature!.Parameters[3].TypeReference?.IsPointer);
    }

    [Fact]
    public void ParseTypedefs_FunctionSignatureTypedef_ParsesCorrectly()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 8553 */",
            "typedef bool __cdecl InputFilter(unsigned __int16);"
        };

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Single(result);
        var typedef = result[0];
        Assert.Equal("InputFilter", typedef.Name);
        Assert.Equal(TypeType.Typedef, TypeType.Typedef);
        Assert.NotNull(typedef.FunctionSignature);
        Assert.Equal("InputFilter", typedef.FunctionSignature?.Name ?? string.Empty);
        Assert.Equal("bool", typedef.FunctionSignature?.ReturnType ?? string.Empty);
        Assert.Equal("__cdecl", typedef.FunctionSignature?.CallingConvention ?? string.Empty);
        Assert.Single(typedef.FunctionSignature?.Parameters ?? new List<FunctionParamModel>());
        Assert.Contains("unsigned __int16", typedef.FunctionSignature?.Parameters?[0].ParameterType ?? string.Empty);
    }

    [Fact]
    public void ParseTypedefs_EmptySource_ReturnsEmptyList()
    {
        // Arrange
        var source = new List<string>();

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTypedefs_NoTypedefs_ReturnsEmptyList()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 123 */",
            "struct SomeStruct",
            "{",
            "    int value;",
            "};"
        };

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTypedefs_MultipleTypedefs_ParsesAll()
    {
        // Arrange
        var source = new List<string>
        {
            "/* 1 */",
            "typedef int TypeA;",
            "/* 2 */",
            "typedef void *TypeB;",
            "/* 3 */",
            "typedef int (__stdcall *TypeC)();"
        };

        // Act
        var result = TypedefParser.ParseTypedefs(source);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("TypeA", result[0].Name);
        Assert.Equal("TypeB", result[1].Name);
        Assert.Equal("TypeC", result[2].Name);
    }
}
