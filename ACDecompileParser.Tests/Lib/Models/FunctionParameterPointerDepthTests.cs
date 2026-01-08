using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Models;

public class FunctionParameterPointerDepthTests
{
    [Fact]
    public void ParseFunctionParameters_SinglePointer_HasPointerDepthOne()
    {
        // Test parsing function parameter with single pointer
        string declaration = "bool (__thiscall *Create)(RenderVertexStreamD3D *this, const unsigned int *param);";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature);
        Assert.NotNull(member.FunctionSignature.Parameters);

        // Find the parameter with pointer
        var pointerParam =
            member.FunctionSignature!.Parameters.FirstOrDefault(p => p.ParameterType.Contains("unsigned int"));
        Assert.NotNull(pointerParam);
        Assert.NotNull(pointerParam.TypeReference);
        Assert.True(pointerParam.TypeReference.IsPointer);
        Assert.Equal(1, pointerParam.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseFunctionParameters_DoublePointer_HasPointerDepthTwo()
    {
        // Test parsing function parameter with double pointer
        string declaration = "bool (__thiscall *Create)(RenderVertexStreamD3D *this, const unsigned int **param);";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature);
        Assert.NotNull(member.FunctionSignature.Parameters);

        // Find the parameter with double pointer
        var pointerParam =
            member.FunctionSignature.Parameters.FirstOrDefault(p => p.ParameterType.Contains("unsigned int"));
        Assert.NotNull(pointerParam);
        Assert.NotNull(pointerParam.TypeReference);
        Assert.True(pointerParam.TypeReference.IsPointer);
        Assert.Equal(2, pointerParam.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseFunctionParameters_TriplePointer_HasPointerDepthThree()
    {
        // Test parsing function parameter with triple pointer
        string declaration = "bool (__thiscall *Create)(RenderVertexStreamD3D *this, const unsigned int ***param);";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature);
        Assert.NotNull(member.FunctionSignature.Parameters);

        // Find the parameter with triple pointer
        var pointerParam =
            member.FunctionSignature!.Parameters.FirstOrDefault(p => p.ParameterType.Contains("unsigned int"));
        Assert.NotNull(pointerParam);
        Assert.NotNull(pointerParam.TypeReference);
        Assert.True(pointerParam.TypeReference.IsPointer);
        Assert.Equal(3, pointerParam.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseFunctionParameters_AllPointerDepthVariations()
    {
        // Test parsing function with parameters of varying pointer depths
        string source = @"
/* 1451 */
struct FunctionParamsPointerDepthTestStruct
{
  bool (__thiscall *Create)(RenderVertexStreamD3D *this, const unsigned int, const unsigned int **, GraphicsResource *);
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Single(structs);

        var member = structs[0].Members[0];
        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature);
        Assert.NotNull(member.FunctionSignature.Parameters);

        // Should have 4 parameters
        Assert.Equal(4, member.FunctionSignature.Parameters.Count);

        // Parameter 1: RenderVertexStreamD3D *this (pointer)
        var param1 = member.FunctionSignature.Parameters[0];
        Assert.NotNull(param1.TypeReference);
        Assert.Equal(1, param1.TypeReference.PointerDepth);

        // Parameter 2: const unsigned int (no pointer)
        var param2 = member.FunctionSignature.Parameters[1];
        Assert.NotNull(param2.TypeReference);
        Assert.Equal(0, param2.TypeReference.PointerDepth);

        // Parameter 3: const unsigned int ** (double pointer)
        var param3 = member.FunctionSignature.Parameters[2];
        Assert.NotNull(param3.TypeReference);
        Assert.Equal(2, param3.TypeReference.PointerDepth);

        // Parameter 4: GraphicsResource * (single pointer)
        var param4 = member.FunctionSignature.Parameters[3];
        Assert.NotNull(param4.TypeReference);
        Assert.Equal(1, param4.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseFunctionParameters_UserTestCase_AllVariations()
    {
        // Test case from the user's requirement with all pointer depths
        string source = @"
/* 1451 */
struct FunctionParamsPointerDepthTestStruct
{
  bool (__thiscall *Create)(RenderVertexStreamD3D *this, const unsigned int, const unsigned int **, GraphicsResource *);
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.NotEmpty(structs);

        var member = structs[0].Members[0];
        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature);

        // Verify all parameters are parsed with correct pointer depths
        Assert.NotNull(member.FunctionSignature.Parameters);
        var paramsByIndex = member.FunctionSignature.Parameters.OrderBy(p => p.Position).ToList();

        // Verify we have all parameters
        Assert.Equal(4, paramsByIndex.Count);

        // Find the unsigned int ** parameter (parameter with double pointer depth)
        var doublePointerParam = paramsByIndex.FirstOrDefault(p => p.TypeReference?.PointerDepth == 2);
        Assert.NotNull(doublePointerParam);
        Assert.Equal(2, doublePointerParam.TypeReference?.PointerDepth ?? 0);
    }

    [Fact]
    public void ParseFunctionParameters_NoPointerParameter()
    {
        // Test parsing function parameter without pointer
        string declaration = "bool (__thiscall *Create)(RenderVertexStreamD3D *this, unsigned int value);";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature);
        Assert.NotNull(member.FunctionSignature.Parameters);

        // Find the parameter without pointer
        var nonPointerParam =
            member.FunctionSignature!.Parameters.FirstOrDefault(p => p.ParameterType.Contains("unsigned int"));
        Assert.NotNull(nonPointerParam);
        Assert.NotNull(nonPointerParam.TypeReference);
        Assert.False(nonPointerParam.TypeReference.IsPointer);
        Assert.Equal(0, nonPointerParam.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseFunctionParameters_StructPointerParameter()
    {
        // Test parsing function parameter with struct pointer
        string declaration =
            "void (__thiscall *Process)(MyClass *this, MyStruct *struct_ptr, MyStruct **struct_ptr_ptr);";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature);
        Assert.NotNull(member.FunctionSignature.Parameters);

        // Check single pointer to struct
        var singlePtrParam = member.FunctionSignature.Parameters.FirstOrDefault(p =>
            p.ParameterType.Contains("MyStruct") && !p.ParameterType.Contains("**"));
        if (singlePtrParam != null)
        {
            Assert.Equal(1, singlePtrParam.TypeReference?.PointerDepth ?? 0);
        }

        // Check double pointer to struct
        var doublePtrParam = member.FunctionSignature.Parameters.FirstOrDefault(p =>
            p.ParameterType.Contains("MyStruct") && p.ParameterType.Contains("**"));
        if (doublePtrParam != null)
        {
            Assert.Equal(2, doublePtrParam.TypeReference?.PointerDepth ?? 0);
        }
    }
}
