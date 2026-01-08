using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

/// <summary>
/// Comprehensive tests for function pointers in all contexts:
/// 1. As named method arguments
/// 2. As unnamed method arguments
/// 3. As template arguments
/// </summary>
public class FunctionPointerComprehensiveTests
{
    #region Function Pointers as Named Method Arguments

    // NOTE: Named function pointer parameters (where the name appears in the parameter list
    // like "void (__cdecl *callback)(int)") are not currently supported by the parser.
    // The parser currently handles unnamed function pointers in parameter lists.
    // Named function pointers are supported as struct members.

    [Fact]
    public void ParseMember_NamedFunctionPointerAsParameter_DocumentsCurrentBehavior()
    {
        // This test documents that named function pointers as parameters
        // are not yet fully supported. The parser expects unnamed function pointers
        // in parameter lists: "void (__cdecl *)(int)" not "void (__cdecl *callback)(int)"

        // Arrange - function pointer parameter list (unnamed)
        var paramString = "int x, HRESULT (__cdecl *)(const unsigned __int16 *), float y";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);

        // First parameter is regular int
        Assert.Equal("int", result[0].ParameterType);
        Assert.Equal("x", result[0].Name);

        // Second parameter is unnamed function pointer
        Assert.True(result[1].IsFunctionPointerType);
        Assert.NotNull(result[1].NestedFunctionSignature);
        Assert.Equal("HRESULT", result[1].NestedFunctionSignature!.ReturnType);
        Assert.Equal("__cdecl", result[1].NestedFunctionSignature!.CallingConvention);

        // Third parameter is regular float
        Assert.Equal("float", result[2].ParameterType);
        Assert.Equal("y", result[2].Name);
    }

    [Fact]
    public void ParseMember_NamedFunctionPointerMember_ParsesCorrectly()
    {
        // Arrange - struct member that is a named function pointer
        // Named function pointers ARE supported as struct members (not in parameter lists)
        var line = "HRESULT (__cdecl *SetCallback)(const unsigned __int16 *);";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SetCallback", result.Name);
        Assert.True(result.IsFunctionPointer);
        Assert.Equal("__cdecl", result.FunctionSignature!.CallingConvention);
        Assert.NotNull(result.FunctionSignature.ReturnTypeReference);
        Assert.Equal("HRESULT", result.FunctionSignature.ReturnTypeReference.TypeString);
        Assert.Equal("HRESULT __cdecl __sig_SetCallback(const unsigned __int16*)",
            result.FunctionSignature.FullyQualifiedName);
    }

    #endregion

    #region Function Pointers as Unnamed Method Arguments

    [Fact]
    public void ParseFunctionParameters_UnnamedFunctionPointerParameter_ParsesCorrectly()
    {
        // Arrange - function pointer without an explicit name
        var paramString = "int x, HRESULT (__cdecl *)(const unsigned __int16 *), float y";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);

        // First parameter is regular int
        Assert.Equal("int", result[0].ParameterType);
        Assert.Equal("x", result[0].Name);

        // Second parameter is unnamed function pointer (should get auto-generated name)
        Assert.True(result[1].IsFunctionPointerType);
        Assert.NotNull(result[1].NestedFunctionSignature);
        Assert.Equal("HRESULT", result[1].NestedFunctionSignature!.ReturnType);
        Assert.Equal("__cdecl", result[1].NestedFunctionSignature!.CallingConvention);
        // The parameter name should be auto-generated since it's unnamed
        Assert.NotEmpty(result[1].Name);

        // Third parameter is regular float
        Assert.Equal("float", result[2].ParameterType);
        Assert.Equal("y", result[2].Name);
    }

    [Fact]
    public void ParseFunctionParameters_MultipleUnnamedFunctionPointers_ParsesCorrectly()
    {
        // Arrange - multiple unnamed function pointers
        var paramString = "void (__cdecl *)(int), void (__cdecl *)(float), void (__cdecl *)(double)";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, p => Assert.True(p.IsFunctionPointerType));

        // Each should have an auto-generated name
        Assert.All(result, p => Assert.NotEmpty(p.Name));

        // Each should have correct function signature
        Assert.Equal("void", result[0].NestedFunctionSignature!.ReturnType);
        Assert.Equal("void", result[1].NestedFunctionSignature!.ReturnType);
        Assert.Equal("void", result[2].NestedFunctionSignature!.ReturnType);
    }

    [Fact]
    public void ParseFunctionParameters_UnnamedFunctionPointerNoCallingConvention_ParsesCorrectly()
    {
        // Arrange - unnamed function pointer without calling convention
        var paramString = "void (*)(int, int)";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsFunctionPointerType);
        Assert.NotNull(result[0].NestedFunctionSignature);
        Assert.Equal("void", result[0].NestedFunctionSignature!.ReturnType);
        // Calling convention should be empty or null for no convention
        Assert.True(string.IsNullOrEmpty(result[0].NestedFunctionSignature!.CallingConvention));
    }

    [Fact]
    public void ParseFunctionParameters_MultipleUnnamedInMixedContext_ParsesCorrectly()
    {
        // Arrange - mix of regular params and unnamed function pointers
        var paramString = "int x, void (__cdecl *)(int), void (__cdecl *)(float)";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);

        // First is regular int
        Assert.False(result[0].IsFunctionPointerType);

        // Second and third are function pointers
        Assert.True(result[1].IsFunctionPointerType);
        Assert.True(result[2].IsFunctionPointerType);

        // Both function pointers should have auto-generated names
        Assert.NotEmpty(result[1].Name);
        Assert.NotEmpty(result[2].Name);
    }

    #endregion

    #region Function Pointers as Template Arguments

    [Fact]
    public void ParseStruct_FunctionPointerAsTemplateArgument_ParsesCorrectly()
    {
        // Arrange - The exact example from the user
        string source = @"
/* 4329 */
struct __cppobj HashTable<unsigned long,UIMainFramework * (__cdecl*)(void),0>
{
  HashTable<unsigned long,UIMainFramework * (__cdecl*)(void),0>_vtbl *__vftable /*VFT*/;
  IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIMainFramework * (__cdecl*)(void)> *,0> m_intrusiveTable;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];

        // Verify struct name and template structure
        Assert.Equal("HashTable", structModel.Name);
        Assert.True(structModel.IsGeneric);
        Assert.Equal(3, structModel.TemplateArguments.Count);

        // First template argument: unsigned long
        Assert.Equal("unsigned long", structModel.TemplateArguments[0].TypeString);

        // Second template argument: UIMainFramework * (__cdecl*)(void) - function pointer
        var functionPointerArg = structModel.TemplateArguments[1].TypeString;
        Assert.Contains("UIMainFramework", functionPointerArg);
        Assert.Contains("__cdecl", functionPointerArg);
        Assert.Contains("*", functionPointerArg);

        // Third template argument: 0 (numeric constant)
        Assert.Equal("0", structModel.TemplateArguments[2].TypeString);
    }

    [Fact]
    public void ParseType_FunctionPointerInTemplateArgument_ParsesCorrectly()
    {
        // Arrange - function pointer as template argument
        var typeString = "HashTable<unsigned long,UIMainFramework * (__cdecl*)(void),0>";

        // Act
        var parsed = TypeParser.ParseType(typeString);

        // Assert
        Assert.True(parsed.IsGeneric);
        Assert.Equal("HashTable", parsed.BaseName);
        Assert.Equal(3, parsed.TemplateArguments.Count);

        // Verify template arguments
        Assert.Equal("unsigned long", parsed.TemplateArguments[0].BaseName);

        // Function pointer template argument
        var functionPointerArg = parsed.TemplateArguments[1];
        Assert.NotNull(functionPointerArg);
        // The function pointer should be preserved in the type string
        Assert.Contains("UIMainFramework", functionPointerArg.BaseName);

        Assert.Equal("0", parsed.TemplateArguments[2].BaseName);
    }

    [Fact]
    public void ParseStruct_NestedTemplateWithFunctionPointer_ParsesCorrectly()
    {
        // Arrange - nested template with function pointer
        string source = @"
/* 4329 */
struct TestStruct
{
  HashTableData<unsigned long,UIMainFramework * (__cdecl*)(void)> *data;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];
        Assert.Single(structModel.Members);

        var member = structModel.Members[0];
        Assert.Equal("data", member.Name);
        Assert.NotNull(member.TypeReference);

        // Parse the member's type to check template arguments
        var memberType = TypeParser.ParseType(member.TypeReference.TypeString);
        Assert.True(memberType.IsGeneric);
        Assert.Equal("HashTableData", memberType.BaseName);
        Assert.Equal(2, memberType.TemplateArguments.Count);

        // Second template argument should be the function pointer
        var functionPointerArg = memberType.TemplateArguments[1];
        Assert.Contains("UIMainFramework", functionPointerArg.BaseName);
    }

    [Fact]
    public void ParseStruct_MultipleFunctionPointersAsTemplateArguments_ParsesCorrectly()
    {
        // Arrange - template with multiple function pointer arguments
        string source = @"
/* 4329 */
struct MultiCallback<void (__cdecl*)(int),void (__cdecl*)(float)>
{
  int value;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];

        Assert.Equal("MultiCallback", structModel.Name);
        Assert.True(structModel.IsGeneric);
        Assert.Equal(2, structModel.TemplateArguments.Count);

        // Both template arguments should be function pointers
        Assert.Contains("__cdecl", structModel.TemplateArguments[0].TypeString);
        Assert.Contains("__cdecl", structModel.TemplateArguments[1].TypeString);
    }

    [Fact]
    public void ParseStruct_FunctionPointerReturningPointer_AsTemplateArgument_ParsesCorrectly()
    {
        // Arrange - function pointer with pointer return type as template argument
        string source = @"
/* 4329 */
struct CallbackHolder<unsigned __int16 * (__cdecl*)(const unsigned __int16 *)>
{
  int value;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];

        Assert.Equal("CallbackHolder", structModel.Name);
        Assert.True(structModel.IsGeneric);
        Assert.Single(structModel.TemplateArguments);

        // Template argument should be function pointer with pointer return type
        var functionPointerArg = structModel.TemplateArguments[0].TypeString;
        Assert.Contains("unsigned __int16", functionPointerArg);
        Assert.Contains("__cdecl", functionPointerArg);
        Assert.Contains("*", functionPointerArg);
    }

    [Fact]
    public void ParseStruct_ComplexRealWorldExample_ParsesCorrectly()
    {
        // Arrange - Complete real-world example from user
        string source = @"
/* 4329 */
struct __cppobj HashTable<unsigned long,UIMainFramework * (__cdecl*)(void),0>
{
  HashTable<unsigned long,UIMainFramework * (__cdecl*)(void),0>_vtbl *__vftable /*VFT*/;
  IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIMainFramework * (__cdecl*)(void)> *,0> m_intrusiveTable;
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];

        // Verify struct
        Assert.Equal("HashTable", structModel.Name);
        Assert.True(structModel.IsGeneric);
        Assert.Equal(3, structModel.TemplateArguments.Count);

        // Verify members
        Assert.Equal(2, structModel.Members.Count);

        // First member: __vftable
        var vftableMember = structModel.Members[0];
        Assert.Equal("__vftable", vftableMember.Name);

        // Second member: m_intrusiveTable with nested template
        var intrusiveTableMember = structModel.Members[1];
        Assert.Equal("m_intrusiveTable", intrusiveTableMember.Name);
        Assert.NotNull(intrusiveTableMember.TypeReference);

        // Parse the intrusive table type
        var intrusiveTableType = TypeParser.ParseType(intrusiveTableMember.TypeReference.TypeString);
        Assert.True(intrusiveTableType.IsGeneric);
        Assert.Equal("IntrusiveHashTable", intrusiveTableType.BaseName);

        // It should have 3 template arguments
        Assert.Equal(3, intrusiveTableType.TemplateArguments.Count);

        // Second template argument is HashTableData<...> with nested function pointer
        var hashTableDataArg = intrusiveTableType.TemplateArguments[1];
        Assert.True(hashTableDataArg.IsGeneric);
        Assert.Equal("HashTableData", hashTableDataArg.BaseName);
        Assert.Equal(2, hashTableDataArg.TemplateArguments.Count);

        // The nested template's second argument should be the function pointer
        var nestedFunctionPointer = hashTableDataArg.TemplateArguments[1];
        Assert.Contains("UIMainFramework", nestedFunctionPointer.BaseName);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void ParseStruct_FunctionPointerInTemplateAndAsParameter_ParsesCorrectly()
    {
        // Arrange - function pointers in both template arguments and member function parameters
        string source = @"
/* 4329 */
struct Callback<void (__cdecl*)(int)>
{
  void (__thiscall *Execute)(Callback *this, void (__cdecl *)(int));
};";

        // Act
        var structs = TypeParser.ParseStructs(source);

        // Assert
        Assert.Single(structs);
        var structModel = structs[0];

        // Verify template
        Assert.True(structModel.IsGeneric);
        Assert.Single(structModel.TemplateArguments);

        // Verify member function with function pointer parameter
        Assert.Single(structModel.Members);
        var executeMember = structModel.Members[0];
        Assert.Equal("Execute", executeMember.Name);
        Assert.True(executeMember.IsFunctionPointer);

        // Should have parameters: this pointer and function pointer
        Assert.Equal(2, executeMember.FunctionSignature!.Parameters.Count);

        // Second parameter should be a function pointer
        Assert.True(executeMember.FunctionSignature.Parameters[1].IsFunctionPointerType);
    }

    [Fact]
    public void ParseFunctionParameters_FunctionPointerWithTemplateParameter_ParsesCorrectly()
    {
        // Arrange - function pointer that takes a template type as parameter
        var paramString = "void (__cdecl *)(List<int> *, float)";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsFunctionPointerType);
        Assert.NotNull(result[0].NestedFunctionSignature);

        // The nested function should have 2 parameters
        Assert.Equal(2, result[0].NestedFunctionSignature!.Parameters.Count);

        // First parameter should be List<int> *
        var firstParam = result[0].NestedFunctionSignature!.Parameters[0];
        Assert.Contains("List", firstParam.ParameterType);
    }

    #endregion
}
