using ACDecompileParser.Shared.Lib.Utilities;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Utilities;

public class FunctionPointerTests
{
    [Fact]
    public void IsFunctionPointerParameter_IdentifiesSinglePointer()
    {
        Assert.True(ParsingUtilities.IsFunctionPointerParameter("bool (__cdecl *tableID)()"));
    }

    [Fact]
    public void IsFunctionPointerParameter_IdentifiesDoublePointer()
    {
        // This is expected to FAIL before fix
        Assert.True(ParsingUtilities.IsFunctionPointerParameter("bool (__cdecl **tableID)()"));
    }

    [Fact]
    public void ExtractFunctionPointerParameterInfo_HandlesSinglePointer()
    {
        var (returnType, callingConvention, parameters, name, pointerDepth) =
            ParsingUtilities.ExtractFunctionPointerParameterInfo("bool (__cdecl *tableID)()");

        Assert.Equal("bool", returnType);
        Assert.Equal("__cdecl", callingConvention);
        Assert.Equal("", parameters);
        Assert.Equal("tableID", name);
        Assert.Equal(1, pointerDepth);
    }

    [Fact]
    public void ExtractFunctionPointerParameterInfo_HandlesDoublePointer()
    {
        var (returnType, callingConvention, parameters, name, pointerDepth) =
            ParsingUtilities.ExtractFunctionPointerParameterInfo("bool (__cdecl **tableID)()");

        Assert.Equal("bool", returnType);
        Assert.Equal("__cdecl", callingConvention);
        Assert.Equal("", parameters);
        Assert.Equal("tableID", name);
        Assert.Equal(2, pointerDepth);
    }
}
