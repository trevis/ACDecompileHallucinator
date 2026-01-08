using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Models;

public class MemberParsingTest
{
  [Fact]
  public void TestOperatorMemberParsing()
  {
    string source = @"
/* 763 */
struct TestStruct
{
  bool (__thiscall *operator==)(TestStruct *this, const TestStruct *);
};";

    var structs = TypeParser.ParseStructs(source);

    Assert.Single(structs);

    var structModel2 = structs[0];
    Assert.Single(structModel2.Members);

    var member = structModel2.Members[0];
    Assert.True(member.IsFunctionPointer);
    Assert.Equal("operator==", member.Name);
  }

  #region Array Member Parsing Tests

  [Fact]
  public void TestPaddingTestStruct_ParsesAllArrayMembers()
  {
    string source = @"
/* 1450 */
struct PaddingTest
{
  void *pad[2];
  char __ss_pad2[112];
  char Format[];
  _BYTE gap4[4];
  unsigned __int16 lfFaceName[32];
};";

    var structs = TypeParser.ParseStructs(source);

    Assert.Single(structs);
    var structModel = structs[0];
    Assert.Equal("PaddingTest", structModel.Name);
    Assert.Equal(5, structModel.Members.Count);
  }

  [Fact]
  public void TestVoidPointerArray_ParsesCorrectly()
  {
    string source = @"
/* 1 */
struct TestStruct
{
  void *pad[2];
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];
    Assert.Equal("pad", member.Name);
    Assert.NotNull(member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null"));
    Assert.True((member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null"))
      .IsArray);
    Assert.Equal(2,
      (member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null")).ArraySize);
  }

  [Fact]
  public void TestCharArrayWithSize_ParsesCorrectly()
  {
    string source = @"
/* 1 */
struct TestStruct
{
  char __ss_pad2[112];
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];
    Assert.Equal("__ss_pad2", member.Name);
    Assert.NotNull(member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null"));
    Assert.True((member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null"))
      .IsArray);
    Assert.Equal(112,
      (member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null")).ArraySize);
  }

  [Fact]
  public void TestFlexibleArrayMember_ParsesCorrectly()
  {
    string source = @"
/* 1 */
struct TestStruct
{
  char Format[];
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];
    Assert.Equal("Format", member.Name);
    Assert.NotNull(member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null"));
    Assert.True((member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null"))
      .IsArray);
    Assert.Null((member.TypeReference ?? throw new InvalidOperationException("TypeReference should not be null"))
      .ArraySize);
  }

  [Fact]
  public void TestByteArrayWithGap_ParsesCorrectly()
  {
    string source = @"
/* 1 */
struct TestStruct
{
  _BYTE gap4[4];
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];
    Assert.Equal("gap4", member.Name);
    Assert.NotNull(member.TypeReference);
    Assert.True(member.TypeReference.IsArray);
    Assert.Equal(4, member.TypeReference.ArraySize);
  }

  [Fact]
  public void TestUnsignedInt16Array_ParsesCorrectly()
  {
    string source = @"
/* 1 */
struct TestStruct
{
  unsigned __int16 lfFaceName[32];
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];
    Assert.Equal("lfFaceName", member.Name);
    Assert.NotNull(member.TypeReference);
    Assert.True(member.TypeReference.IsArray);
    Assert.Equal(32, member.TypeReference.ArraySize);
  }

  [Fact]
  public void TestNonArrayMember_HasNoArrayInfo()
  {
    string source = @"
/* 1 */
struct TestStruct
{
  int regularMember;
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);

    var member = structs[0].Members[0];
    Assert.Equal("regularMember", member.Name);
    Assert.NotNull(member.TypeReference);
    Assert.False(member.TypeReference.IsArray);
    Assert.Null(member.TypeReference.ArraySize);
  }

  [Fact]
  public void TestMixedArrayAndNonArrayMembers()
  {
    string source = @"
/* 1 */
struct TestStruct
{
  int x;
  char buffer[256];
  float y;
};";

    var structs = TypeParser.ParseStructs(source);
    Assert.Single(structs);
    Assert.Equal(3, structs[0].Members.Count);

    var member1 = structs[0].Members[0];
    Assert.Equal("x", member1.Name);
    Assert.NotNull(member1.TypeReference);
    Assert.False(member1.TypeReference.IsArray);

    var member2 = structs[0].Members[1];
    Assert.Equal("buffer", member2.Name);
    Assert.NotNull(member2.TypeReference);
    Assert.True(member2.TypeReference.IsArray);
    Assert.Equal(256, member2.TypeReference.ArraySize);

    var member3 = structs[0].Members[2];
    Assert.Equal("y", member3.Name);
    Assert.NotNull(member3.TypeReference);
    Assert.False(member3.TypeReference.IsArray);
  }

  #endregion
}
