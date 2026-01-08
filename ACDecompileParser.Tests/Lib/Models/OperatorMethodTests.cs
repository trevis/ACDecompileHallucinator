using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Models;

public class OperatorMethodTests
{
  [Fact]
  public void CanParseOperatorMethodsInVTable()
  {
    string source = @"
/* 763 */
struct /*VFT*/ BasePropertyValue_vtbl
{
  void (__thiscall *operator+=)(BasePropertyValue *this, const BasePropertyValue *);
  void (__thiscall *operator-=)(BasePropertyValue *this, const BasePropertyValue *);
  void (__thiscall *operator*=)(BasePropertyValue *this, const BasePropertyValue *);
  void (__thiscall *operator/=)(BasePropertyValue *this, const BasePropertyValue *);
  bool (__thiscall *operator>)(BasePropertyValue *this, const BasePropertyValue *);
  bool (__thiscall *operator>=)(BasePropertyValue *this, const BasePropertyValue *);
  bool (__thiscall *operator<)(BasePropertyValue *this, const BasePropertyValue *);
  bool (__thiscall *operator<=)(BasePropertyValue *this, const BasePropertyValue *);
  bool (__thiscall *operator!=)(BasePropertyValue *this, const BasePropertyValue *);
  bool (__thiscall *operator==)(BasePropertyValue *this, const BasePropertyValue *);
  void (__thiscall *operator&=)(BasePropertyValue *this, const BasePropertyValue *);
  void (__thiscall *operator|=)(BasePropertyValue *this, const BasePropertyValue *);
  void (__thiscall *operator^=)(BasePropertyValue *this, const BasePropertyValue *);
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var structModel = structs[0];
    Assert.Equal("BasePropertyValue_vtbl", structModel.Name);
    Assert.Equal(13, structModel.Members.Count);

    // Check the first operator
    var member = structModel.Members[0];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator+=", member.Name);
    Assert.Equal("void", member.FunctionSignature!.ReturnTypeReference?.TypeString);
    Assert.Equal("__thiscall", member.FunctionSignature!.CallingConvention);
    Assert.Equal(2, member.FunctionSignature!.Parameters.Count);

    // Check the second operator
    member = structModel.Members[1];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator-=", member.Name);
    Assert.Equal("void", member.FunctionSignature!.ReturnTypeReference?.TypeString);
    Assert.Equal(2, member.FunctionSignature!.Parameters.Count);

    // Check the comparison operators
    member = structModel.Members[4];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator>", member.Name);
    Assert.Equal("bool", member.FunctionSignature!.ReturnTypeReference?.TypeString);
    Assert.Equal(2, member.FunctionSignature!.Parameters.Count);

    // Check assignment operators
    member = structModel.Members[10];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator&=", member.Name);
    Assert.Equal("void", member.FunctionSignature!.ReturnTypeReference?.TypeString);
    Assert.Equal(2, member.FunctionSignature!.Parameters.Count);
  }

  [Fact]
  public void CanParseIndividualOperatorMethods()
  {
    // Test individual operator methods to isolate issues
    string source = @"
/* 763 */
struct TestStruct
{
  bool (__thiscall *operator==)(TestStruct *this, const TestStruct *test);
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var structModel = structs[0];
    Assert.Single(structModel.Members);

    var member = structModel.Members[0];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator==", member.Name);
    Assert.Equal("bool", member.FunctionSignature!.ReturnTypeReference?.TypeString);
    Assert.Equal("__thiscall", member.FunctionSignature!.CallingConvention);
    Assert.Equal(2, member.FunctionSignature!.Parameters.Count);

    Assert.Equal("TestStruct*", member.FunctionSignature!.Parameters[0].ParameterType);
    Assert.Equal("this", member.FunctionSignature!.Parameters[0].Name);
    Assert.True(member.FunctionSignature!.Parameters[0].TypeReference?.IsPointer);

    Assert.Equal("const TestStruct*", member.FunctionSignature!.Parameters[1].ParameterType);
    Assert.Equal("test", member.FunctionSignature!.Parameters[1].Name);
    Assert.True(member.FunctionSignature!.Parameters[1].TypeReference?.IsPointer);
    Assert.True(member.FunctionSignature!.Parameters[1].TypeReference?.IsConst);
  }

  [Fact]
  public void CanParseVariousOperatorTypes()
  {
    string source = @"
/* 763 */
struct TestOperators
{
  void (__thiscall *operator+=)(TestOperators *this, const TestOperators *);
  void (__thiscall *operator[])(TestOperators *this, int index);
  bool (__thiscall *operator!)(TestOperators *this);
  TestOperators* (__thiscall *operator->)(TestOperators *this);
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var structModel = structs[0];
    Assert.Equal(4, structModel.Members.Count);

    // Check operator+=
    var member = structModel.Members[0];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator+=", member.Name);

    // Check operator[]
    member = structModel.Members[1];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator[]", member.Name);

    // Check operator!
    member = structModel.Members[2];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator!", member.Name);

    // Check operator->
    member = structModel.Members[3];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator->", member.Name);
  }
}
