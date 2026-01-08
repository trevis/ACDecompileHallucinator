using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Models;

public class TemplateArgumentPointerDepthTests
{
  [Fact]
  public void ParseTemplateArguments_SinglePointerType_HasPointerDepthOne()
  {
    // Test parsing template argument with single pointer (ListNode<SkillRecord *>)
    string source = @"
/* 1451 */
struct TemplateArgsPointerDepthTestStruct
{
  ListNode<SkillRecord *> *_tail;
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];
    Assert.Equal("_tail", member.Name);
    Assert.NotNull(member.TypeReference);

    // Parse the type to examine template arguments
    var typeModel = TypeParser.ParseType(member.TypeReference.TypeString);
    Assert.True(typeModel.IsGeneric);
    Assert.NotEmpty(typeModel.TemplateArguments);

    var templateArg = typeModel.TemplateArguments[0];
    Assert.Equal(1, templateArg.PointerDepth);
  }

  [Fact]
  public void ParseTemplateArguments_DoublePointerType_HasPointerDepthTwo()
  {
    // Test parsing template argument with double pointer (ListNode<SkillRecord **>)
    string source = @"
/* 1451 */
struct TemplateArgsPointerDepthTestStruct
{
  ListNode<SkillRecord **> items;
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];
    Assert.Equal("items", member.Name);

    var typeModel = TypeParser.ParseType(member.TypeReference?.TypeString ?? "");
    Assert.True(typeModel.IsGeneric);
    Assert.NotEmpty(typeModel.TemplateArguments);

    var templateArg = typeModel.TemplateArguments[0];
    Assert.Equal(2, templateArg.PointerDepth);
  }

  [Fact]
  public void ParseTemplateArguments_TriplePointerType_HasPointerDepthThree()
  {
    // Test parsing template argument with triple pointer (List<SkillRecord ***>)
    string source = @"
/* 1451 */
struct TemplateArgsPointerDepthTestStruct
{
  List<SkillRecord ***> items;
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];

    var typeModel = TypeParser.ParseType(member.TypeReference?.TypeString ?? "");
    Assert.True(typeModel.IsGeneric);
    Assert.NotEmpty(typeModel.TemplateArguments);

    var templateArg = typeModel.TemplateArguments[0];
    Assert.Equal(3, templateArg.PointerDepth);
  }

  [Fact]
  public void ParseTemplateArguments_NoPointerType_HasPointerDepthZero()
  {
    // Test parsing template argument without pointer (ListNode<int>)
    string source = @"
/* 1451 */
struct TemplateArgsPointerDepthTestStruct
{
  ListNode<int> items;
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];

    var typeModel = TypeParser.ParseType(member.TypeReference?.TypeString ?? "");
    Assert.True(typeModel.IsGeneric);
    Assert.NotEmpty(typeModel.TemplateArguments);

    var templateArg = typeModel.TemplateArguments[0];
    Assert.Equal(0, templateArg.PointerDepth);
  }

  [Fact]
  public void ParseTemplateArguments_MultipleArguments_AllHaveCorrectPointerDepth()
  {
    // Test parsing template with multiple arguments of different pointer depths
    string source = @"
/* 1451 */
struct TemplateArgsPointerDepthTestStruct
{
  Map<int, SkillRecord *> items;
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];

    var typeModel = TypeParser.ParseType(member.TypeReference?.TypeString ?? "");
    Assert.True(typeModel.IsGeneric);
    Assert.Equal(2, typeModel.TemplateArguments.Count);

    // First template argument (int) should have no pointer
    Assert.Equal(0, typeModel.TemplateArguments[0].PointerDepth);

    // Second template argument (SkillRecord *) should have pointer depth 1
    Assert.Equal(1, typeModel.TemplateArguments[1].PointerDepth);
  }

  [Fact]
  public void ParseFunctionPointer_TemplateArgumentDestructor_WithPointerDepth()
  {
    // Test from user's requirement: destructor with template argument containing pointers
    // void (__thiscall *~List<SkillRecord **>)(List<SkillRecord ***> *this);
    string declaration = "void (__thiscall *~List)(List<SkillRecord ***> *this);";

    var member = MemberParser.ParseMemberDeclaration(declaration);

    Assert.NotNull(member);
    Assert.True(member.IsFunctionPointer);

    // Check function parameters for template arguments
    Assert.NotNull(member.FunctionSignature);
    var thisParam = member.FunctionSignature!.Parameters.FirstOrDefault(p => p.ParameterType.Contains("List"));
    Assert.NotNull(thisParam);

    // Parse the parameter type to check template arguments
    var paramTypeModel = TypeParser.ParseType(thisParam.TypeReference?.TypeString ?? "");
    if (paramTypeModel.IsGeneric)
    {
      Assert.NotEmpty(paramTypeModel.TemplateArguments);
      var templateArg = paramTypeModel.TemplateArguments[0];
      Assert.Equal(3, templateArg.PointerDepth);
    }
  }

  [Fact]
  public void ParseTemplate_UserTestCase_CompleteExample()
  {
    // Test case from user's requirement
    string source = @"
/* 1451 */
struct TemplateArgsPointerDepthTestStruct
{
  ListNode<SkillRecord *> *_tail;
  void (__thiscall *~List<SkillRecord **>)(List<SkillRecord ***> *this);
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var structModel = structs[0];
    Assert.Equal(2, structModel.Members.Count);

    // First member: ListNode<SkillRecord *> *_tail
    var member1 = structModel.Members[0];
    var member1TypeModel = TypeParser.ParseType(member1.TypeReference?.TypeString ?? "");
    if (member1TypeModel.IsGeneric && member1TypeModel.TemplateArguments.Count > 0)
    {
      Assert.Equal(1, member1TypeModel.TemplateArguments[0].PointerDepth);
    }

    // Second member: function pointer with template arguments
    var member2 = structModel.Members[1];
    Assert.True(member2.IsFunctionPointer);

    if (member2.FunctionSignature!.Parameters.Count > 0)
    {
      var firstParam = member2.FunctionSignature.Parameters[0];
      var paramTypeModel = TypeParser.ParseType(firstParam.TypeReference?.TypeString ?? "");
      if (paramTypeModel.IsGeneric && paramTypeModel.TemplateArguments.Count > 0)
      {
        Assert.Equal(3, paramTypeModel.TemplateArguments[0].PointerDepth);
      }
    }
  }
}
