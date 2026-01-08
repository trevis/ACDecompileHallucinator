using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Models;

public class FunctionReturnTypeParsingTests
{
    [Fact]
    public void ParseFunctionPointer_ReturnTypeReference_HasCorrectName()
    {
        // Test parsing a function pointer declaration and checking the return type reference
        string declaration = "struct Collection *(__thiscall *GetCollection)();";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);
        Assert.Equal("Collection*", member.FunctionSignature!.ReturnTypeReference?.TypeString ?? string.Empty);

        // Parse the return type to check individual properties
        var returnTypeReference = member.FunctionSignature!.ReturnTypeReference;
        Assert.NotNull(returnTypeReference);
        var returnTypeModel = TypeParser.ParseType(returnTypeReference.TypeString);
        Assert.Equal("Collection", returnTypeModel.BaseName);
    }

    [Fact]
    public void ParseFunctionPointer_ReturnTypeReference_IsPointerTrue()
    {
        // Test that the return type reference correctly identifies pointer properties
        string declaration = "struct Collection *(__thiscall *GetCollection)();";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);
        Assert.True(member.FunctionSignature!.ReturnTypeReference?.IsPointer ?? false);
        Assert.Equal(1, member.FunctionSignature!.ReturnTypeReference?.PointerDepth ?? 0);
    }

    [Fact]
    public void ParseFunctionPointer_ReturnTypeReference_HasCorrectNamespace()
    {
        // Test parsing a function pointer with namespaced return type
        string declaration = "struct Test::Test *(__thiscall *GetTest)();";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);

        // Parse the return type to check namespace
        var returnTypeReference = member.FunctionSignature!.ReturnTypeReference;
        Assert.NotNull(returnTypeReference);
        var returnTypeModel = TypeParser.ParseType(returnTypeReference.TypeString);
        Assert.Equal("Test", returnTypeModel.Namespace);
        Assert.Equal("Test", returnTypeModel.BaseName);
    }

    [Fact]
    public void ParseFunctionPointer_ReturnTypeReference_HasCorrectPropertiesForMultipleExamples()
    {
        // Test the examples from the user's request
        string[] declarations =
        {
            "struct Collection *(__thiscall *GetCollection)();",
            "struct Test::Test *(__thiscall *GetTest)();"
        };

        var members = declarations.Select(decl => MemberParser.ParseMemberDeclaration(decl)).ToList();

        Assert.Equal(2, members.Count);

        // Check first function pointer (GetCollection)
        var getCollectionMember = members[0];
        Assert.NotNull(getCollectionMember);
        Assert.True(getCollectionMember.IsFunctionPointer);
        Assert.NotNull(getCollectionMember.FunctionSignature!.ReturnTypeReference);

        var getCollectionReturnTypeRef = getCollectionMember.FunctionSignature!.ReturnTypeReference;
        Assert.NotNull(getCollectionReturnTypeRef);
        var getCollectionReturnType = TypeParser.ParseType(getCollectionReturnTypeRef.TypeString);
        Assert.Equal("Collection", getCollectionReturnType.BaseName);
        Assert.Equal("", getCollectionReturnType.Namespace); // No namespace in "Collection"
        Assert.True(getCollectionMember.FunctionSignature!.ReturnTypeReference?.IsPointer ?? false);

        // Check second function pointer (GetTest)
        var getTestMember = members[1];
        Assert.NotNull(getTestMember);
        Assert.True(getTestMember.IsFunctionPointer);
        Assert.NotNull(getTestMember.FunctionSignature!.ReturnTypeReference);

        var getTestReturnTypeRef = getTestMember.FunctionSignature!.ReturnTypeReference;
        Assert.NotNull(getTestReturnTypeRef);
        var getTestReturnType = TypeParser.ParseType(getTestReturnTypeRef.TypeString);
        Assert.Equal("Test", getTestReturnType.BaseName);
        Assert.Equal("Test", getTestReturnType.Namespace); // "Test" namespace for "Test::Test"
        Assert.True(getTestMember.FunctionSignature!.ReturnTypeReference?.IsPointer ?? false);
    }

    [Fact]
    public void ParseFunctionPointer_ReturnTypeReference_HandlesComplexDeclarations()
    {
        // Test parsing more complex function pointer declarations
        string declaration = "const struct DBOCache_vtbl *(__thiscall *SomeFunction)();";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);
        Assert.Equal("const DBOCache_vtbl*", member.FunctionSignature!.ReturnTypeReference.TypeString);
        Assert.True(member.FunctionSignature!.ReturnTypeReference.IsConst);
        Assert.True(member.FunctionSignature!.ReturnTypeReference.IsPointer);

        var returnTypeModel = TypeParser.ParseType(member.FunctionSignature!.ReturnTypeReference.TypeString);
        Assert.Equal("DBOCache_vtbl", returnTypeModel.BaseName);
        Assert.True(returnTypeModel.IsConst);
        Assert.True(returnTypeModel.IsPointer);
    }

    [Fact]
    public void ParseFunctionPointer_ReturnTypeReference_HandlesNonPointerReturnTypes()
    {
        // Test parsing function pointer with non-pointer return type
        string declaration = "int (__thiscall *GetIntValue)();";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);
        Assert.Equal("int", member.FunctionSignature!.ReturnTypeReference.TypeString);
        Assert.False(member.FunctionSignature!.ReturnTypeReference.IsPointer);
        Assert.Equal(0, member.FunctionSignature!.ReturnTypeReference.PointerDepth);

        var returnTypeModel = TypeParser.ParseType(member.FunctionSignature!.ReturnTypeReference.TypeString);
        Assert.Equal("int", returnTypeModel.BaseName);
        Assert.False(returnTypeModel.IsPointer);
    }

    [Fact]
    public void ParseFunctionPointer_CapturesCallingConvention()
    {
        // Test that the calling convention is properly captured
        string declaration = "struct Collection *(__thiscall *GetCollection)();";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.Equal("__thiscall", member.FunctionSignature!.CallingConvention);
    }

    [Fact]
    public void ParseFunctionPointer_HandlesMultiplePointerDepth()
    {
        // Test parsing function pointer with multiple pointer indirection
        string declaration = "struct Collection **(__thiscall *GetCollectionPtrPtr)();";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);
        Assert.Equal("Collection**", member.FunctionSignature.ReturnTypeReference!.TypeString);
        Assert.True(member.FunctionSignature.ReturnTypeReference.IsPointer);
        Assert.Equal(2, member.FunctionSignature.ReturnTypeReference.PointerDepth);

        var returnTypeRef = member.FunctionSignature.ReturnTypeReference;
        Assert.NotNull(returnTypeRef);
        var returnTypeModel2 = TypeParser.ParseType(returnTypeRef.TypeString);
        Assert.Equal("Collection", returnTypeModel2.BaseName);
        Assert.Equal(2, returnTypeModel2.PointerDepth);
    }

    [Fact]
    public void ParseFunctionPointer_ReturnTypeTriplePointerDepth()
    {
        // Test parsing function pointer with triple pointer return type (Collection ***)
        string declaration = "struct Collection ***(__thiscall *GetCollectionPtrPtrPtr)();";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);
        Assert.Equal("Collection***", member.FunctionSignature!.ReturnTypeReference.TypeString);
        Assert.True(member.FunctionSignature!.ReturnTypeReference.IsPointer);
        Assert.Equal(3, member.FunctionSignature!.ReturnTypeReference.PointerDepth);
    }

    [Fact]
    public void ParseFunctionPointer_ReturnTypeRenderVertexBufferDoublePointer()
    {
        // Test from the user's test case: RenderVertexBuffer **
        string declaration = "struct RenderVertexBuffer **(__thiscall *CreateVertexBuffer)(RenderDevice *this);";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);
        Assert.Equal("RenderVertexBuffer**", member.FunctionSignature!.ReturnTypeReference.TypeString);
        Assert.True(member.FunctionSignature!.ReturnTypeReference.IsPointer);
        Assert.Equal(2, member.FunctionSignature!.ReturnTypeReference.PointerDepth);
    }

    [Fact]
    public void ParseFunctionPointer_BoolReturnTypeWithPointer()
    {
        // Test from the user's test case: bool * return type
        string declaration = "bool *(__thiscall *RestoreResource)(GraphicsResource *this);";

        var member = MemberParser.ParseMemberDeclaration(declaration);

        Assert.NotNull(member);
        Assert.True(member.IsFunctionPointer);
        Assert.NotNull(member.FunctionSignature!.ReturnTypeReference);
        Assert.Equal("bool*", member.FunctionSignature!.ReturnTypeReference.TypeString);
        Assert.True(member.FunctionSignature!.ReturnTypeReference.IsPointer);
        Assert.Equal(1, member.FunctionSignature!.ReturnTypeReference.PointerDepth);
    }
}
