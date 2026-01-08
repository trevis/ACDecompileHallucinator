using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class FunctionPointerReturnTypeTests
{
    #region Nested Function Pointer Detection Tests

    [Fact]
    public void HasFunctionPointerReturnType_DetectsNestedFunctionPointer()
    {
        // Arrange - A function pointer that returns a function pointer
        // HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))(IKeystoneWindow *, unsigned int, unsigned int, int)
        var declaration =
            "HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))(IKeystoneWindow *, unsigned int, unsigned int, int)";

        // Act
        var result = ParsingUtilities.HasFunctionPointerReturnType(declaration);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasFunctionPointerReturnType_ReturnsFalseForRegularFunctionPointer()
    {
        // Arrange - A regular function pointer
        var declaration = "void (__thiscall *SomeFunc)(IKeystoneDocument *this)";

        // Act
        var result = ParsingUtilities.HasFunctionPointerReturnType(declaration);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasFunctionPointerReturnType_DetectsGetSoundCallback()
    {
        // Arrange - From the example: HRESULT (__cdecl *(__thiscall *GetSoundCallback)(IKeystoneDocument *this))(const unsigned __int16 *)
        var declaration =
            "HRESULT (__cdecl *(__thiscall *GetSoundCallback)(IKeystoneDocument *this))(const unsigned __int16 *)";

        // Act
        var result = ParsingUtilities.HasFunctionPointerReturnType(declaration);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Nested Function Pointer Info Extraction Tests

    [Fact]
    public void ExtractNestedFunctionPointerInfo_ExtractsAllComponents()
    {
        // Arrange
        var declaration =
            "HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))(IKeystoneWindow *, unsigned int, unsigned int, int)";

        // Act
        var result = ParsingUtilities.ExtractNestedFunctionPointerInfo(declaration);

        // Assert
        Assert.NotNull(result.FunctionName);
        Assert.Equal("GetWndProc", result.FunctionName);
        Assert.Equal("HRESULT", result.OuterReturnType);
        Assert.Equal("__cdecl", result.OuterCallingConvention);
        Assert.Equal("__thiscall", result.InnerCallingConvention);
        Assert.Equal("IKeystoneDocument *this", result.InnerParams);
        Assert.Equal("IKeystoneWindow *, unsigned int, unsigned int, int", result.OuterParams);
    }

    [Fact]
    public void ExtractNestedFunctionPointerInfo_ExtractsGetSoundCallback()
    {
        // Arrange
        var declaration =
            "HRESULT (__cdecl *(__thiscall *GetSoundCallback)(IKeystoneDocument *this))(const unsigned __int16 *)";

        // Act
        var result = ParsingUtilities.ExtractNestedFunctionPointerInfo(declaration);

        // Assert
        Assert.Equal("GetSoundCallback", result.FunctionName);
        Assert.Equal("HRESULT", result.OuterReturnType);
        Assert.Equal("__cdecl", result.OuterCallingConvention);
        Assert.Equal("__thiscall", result.InnerCallingConvention);
        Assert.Equal("IKeystoneDocument *this", result.InnerParams);
        Assert.Equal("const unsigned __int16 *", result.OuterParams);
    }

    [Fact]
    public void ExtractNestedFunctionPointerInfo_ReturnsNullsForRegularFunctionPointer()
    {
        // Arrange
        var declaration = "void (__thiscall *SomeFunc)(IKeystoneDocument *this)";

        // Act
        var result = ParsingUtilities.ExtractNestedFunctionPointerInfo(declaration);

        // Assert
        Assert.Null(result.FunctionName);
        Assert.Null(result.OuterReturnType);
    }

    #endregion

    #region Member Parser Nested Function Pointer Tests

    [Fact]
    public void ParseMemberDeclaration_ParsesNestedFunctionPointer()
    {
        // Arrange
        var line =
            "HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))(IKeystoneWindow *, unsigned int, unsigned int, int);";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GetWndProc", result.Name);
        Assert.True(result.IsFunctionPointer);
        Assert.Equal("__thiscall", result.FunctionSignature!.CallingConvention);

        // Should have the return function signature
        Assert.NotNull(result.FunctionSignature!.ReturnFunctionSignature);
        Assert.Equal("HRESULT", result.FunctionSignature.ReturnFunctionSignature!.ReturnType);
        Assert.Equal("__cdecl", result.FunctionSignature.ReturnFunctionSignature!.CallingConvention);

        // Return function signature should have parameters
        Assert.NotEmpty(result.FunctionSignature!.ReturnFunctionSignature!.Parameters);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesGetSoundCallback()
    {
        // Arrange
        var line =
            "HRESULT (__cdecl *(__thiscall *GetSoundCallback)(IKeystoneDocument *this))(const unsigned __int16 *);";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GetSoundCallback", result.Name);
        Assert.True(result.IsFunctionPointer);
        Assert.Equal("__thiscall", result.FunctionSignature!.CallingConvention);
        Assert.NotNull(result.FunctionSignature.ReturnFunctionSignature);
        Assert.Equal("HRESULT", result.FunctionSignature.ReturnFunctionSignature!.ReturnType);

        // The inner function should have the IKeystoneDocument *this parameter
        Assert.Single(result.FunctionSignature!.Parameters);
        Assert.Equal("this", result.FunctionSignature.Parameters[0].Name);
    }

    [Fact]
    public void ParseMembers_ParsesFunctionTestStruct()
    {
        // Arrange - The exact example from the user input
        var source = @"
/* 1234 */
struct FunctionTestStruct
{
HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))(IKeystoneWindow *, unsigned int, unsigned int, int);
HRESULT (__cdecl *(__thiscall *GetSoundCallback)(IKeystoneDocument *this))(const unsigned __int16 *);
};";

        var structModel = new StructTypeModel();
        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        int structStartIndex = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("struct") && lines[i].Contains("FunctionTestStruct"))
            {
                structStartIndex = i;
                break;
            }
        }

        // Act
        StructParser.ParseNameAndInheritance(structModel, source);
        StructParser.ParseMembers(structModel, lines, structStartIndex);

        // Assert
        Assert.Equal(2, structModel.Members.Count);

        var getWndProc = structModel.Members.FirstOrDefault(m => m.Name == "GetWndProc");
        Assert.NotNull(getWndProc);
        Assert.True(getWndProc!.IsFunctionPointer);
        Assert.NotNull(getWndProc.FunctionSignature!.ReturnFunctionSignature);

        var getSoundCallback = structModel.Members.FirstOrDefault(m => m.Name == "GetSoundCallback");
        Assert.NotNull(getSoundCallback);
        Assert.True(getSoundCallback!.IsFunctionPointer);
        Assert.NotNull(getSoundCallback.FunctionSignature!.ReturnFunctionSignature);
    }

    #endregion
}
