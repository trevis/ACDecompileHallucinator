using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;
using Xunit.Abstractions;

namespace ACDecompileParser.Tests.Lib.Parser;

public class ReproductionTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public ReproductionTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ParseMemberDeclaration_FunctionPointerWithDestructorName_ParsesCorrectly()
    {
        string input = "void (__thiscall *~IInputActionCallback)(IInputActionCallback *this);";
        var result = MemberParser.ParseMemberDeclaration(input);

        Assert.NotNull(result);
        Assert.True(result.IsFunctionPointer);
        Assert.NotNull(result.FunctionSignature);
        Assert.Equal("~IInputActionCallback", result.Name);
        Assert.Single(result.FunctionSignature.Parameters);
        Assert.Equal("IInputActionCallback*", result.FunctionSignature.Parameters[0].ParameterType);
        Assert.Equal("this", result.FunctionSignature.Parameters[0].Name);
    }

    [Fact]
    public void ParseMemberDeclaration_FunctionPointerWithMultipleParams_ParsesCorrectly()
    {
        string input = "bool (__thiscall *OnAction)(IInputActionCallback *this, const InputEvent *);";
        var result = MemberParser.ParseMemberDeclaration(input);

        Assert.NotNull(result);
        Assert.True(result.IsFunctionPointer);
        Assert.NotNull(result.FunctionSignature);
        Assert.Equal("OnAction", result.Name);
        Assert.Equal(2, result.FunctionSignature.Parameters.Count);
        Assert.Equal("IInputActionCallback*", result.FunctionSignature.Parameters[0].ParameterType);
        Assert.Equal("this", result.FunctionSignature.Parameters[0].Name);
        Assert.Equal("const InputEvent*", result.FunctionSignature.Parameters[1].ParameterType);
    }

    [Fact]
    public void ParseMemberDeclaration_FunctionPointerWithAlignment_ParsesCorrectly()
    {
        string input =
            "UIElementMessageListenResult (__thiscall *ListenToElementMessage)(UIListener *this, unsigned int, UIElement *, unsigned int, int) __declspec(align(8));";
        var result = MemberParser.ParseMemberDeclaration(input);

        Assert.NotNull(result);
        Assert.True(result.IsFunctionPointer);
        Assert.NotNull(result.FunctionSignature);
        Assert.Equal("ListenToElementMessage", result.Name);
        Assert.Equal(5, result.FunctionSignature.Parameters.Count);
        Assert.Equal("UIListener*", result.FunctionSignature.Parameters[0].ParameterType);
        Assert.Equal("this", result.FunctionSignature.Parameters[0].Name);
        Assert.Equal("unsigned int", result.FunctionSignature.Parameters[1].ParameterType);
        Assert.Equal("UIElement*", result.FunctionSignature.Parameters[2].ParameterType);
        Assert.Equal("unsigned int", result.FunctionSignature.Parameters[3].ParameterType);
        Assert.Equal("int", result.FunctionSignature.Parameters[4].ParameterType);
        Assert.Equal(8, result.Alignment);
    }

    [Fact]
    public void ParseMemberDeclaration_UIRegionVtbl_ParsesCorrectly()
    {
        // Test case from user report
        string input =
            "void (__thiscall *DrawHere)(UIRegion *this, const Box2D *, const Box2D *, const SmartArray<Box2D,1> *, UISurface *);";
        var result = MemberParser.ParseMemberDeclaration(input);

        Assert.NotNull(result);
        Assert.True(result.IsFunctionPointer);
        Assert.NotNull(result.FunctionSignature);
        Assert.Equal("DrawHere", result.Name);
        Assert.Equal(5, result.FunctionSignature.Parameters.Count);

        Assert.Equal("UIRegion*", result.FunctionSignature.Parameters[0].ParameterType);
        Assert.Equal("this", result.FunctionSignature.Parameters[0].Name);

        Assert.Equal("const Box2D*", result.FunctionSignature.Parameters[1].ParameterType);
        Assert.Equal("const Box2D*", result.FunctionSignature.Parameters[2].ParameterType);
        Assert.Equal("const SmartArray<Box2D,1>*", result.FunctionSignature.Parameters[3].ParameterType);
        Assert.Equal("UISurface*", result.FunctionSignature.Parameters[4].ParameterType);
    }

    [Fact]
    public void ParseFunctionBody_HashErrorLine_SkipsFunction()
    {
        var lines = new List<string>
        {
            "//----- (004112D0) --------------------------------------------------------",
            "#error \"41132D: call analysis failed (funcsize=27)\"",
            "{",
            "}"
        };

        var results = FunctionBodyParser.Parse(lines);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseFunctionBody_VectorDeletingDestructor_ParsesNameCorrectly()
    {
        string signature =
            "IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIElement * (__cdecl*)(LayoutDesc const &,ElementDesc const &)> *,0> *__thiscall IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIElement * (__cdecl *)(LayoutDesc const &,ElementDesc const &)> *,0>::vector deleting destructor(IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIElement * (__cdecl*)(LayoutDesc const &,ElementDesc const &)> *,0> *this, unsigned int a2)";
        var lines = new List<string>
        {
            "//----- (00459AD0) --------------------------------------------------------",
            signature,
            "{",
            "}"
        };
        var results = FunctionBodyParser.Parse(lines);

        // We now skip deleting destructors, so result should be empty
        Assert.Empty(results);
    }
}
