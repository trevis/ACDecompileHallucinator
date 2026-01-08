using ACDecompileParser.Lib.Parser;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class PaddingStructParsingTests
{
  [Fact]
  public void ParseStructWithPadding_ParsesBytePaddingAsArray()
  {
    // Reset the padding counter to ensure consistent test results
    MemberParser.ResetPaddingCounter();

    string source = @"
/* 100 */
struct TestStruct
{
  int value;
 _BYTE[4];
  char name[32];
};";

    var structs = TypeParser.ParseStructs(source);

    Assert.Single(structs);
    var structModel = structs[0];
    Assert.Equal("TestStruct", structModel.Name);
    Assert.Equal(3, structModel.Members.Count);

    // First member: int value
    var member1 = structModel.Members[0];
    Assert.Equal("value", member1.Name);
    Assert.Equal("int", member1.TypeString);

    // Second member: _BYTE[4] (should become __padding0)
    var member2 = structModel.Members[1];
    Assert.StartsWith("__padding", member2.Name);
    Assert.Equal("_BYTE", member2.TypeString);
    Assert.NotNull(member2.TypeReference);
    Assert.True(member2.TypeReference.IsArray);
    Assert.Equal(4, member2.TypeReference.ArraySize);

    // Third member: char name[32]
    var member3 = structModel.Members[2];
    Assert.Equal("name", member3.Name);
    Assert.Equal("char", member3.TypeString);
    Assert.NotNull(member3.TypeReference);
    Assert.True(member3.TypeReference.IsArray);
    Assert.Equal(32, member3.TypeReference.ArraySize);
  }

  [Fact]
  public void ParseStructWithMultiplePadding_ParsesEachWithUniqueNames()
  {
    // Reset the padding counter to ensure consistent test results
    MemberParser.ResetPaddingCounter();

    string source = @"
/* 101 */
struct TestStruct
{
  int first;
  _BYTE[2];
  int second;
  _BYTE[8];
  int third;
  _BYTE[1];
};";

    var structs = TypeParser.ParseStructs(source);

    Assert.Single(structs);
    var structModel = structs[0];
    Assert.Equal("TestStruct", structModel.Name);
    Assert.Equal(6, structModel.Members.Count);

    // Check that padding members get unique names
    Assert.Equal("first", structModel.Members[0].Name);
    Assert.StartsWith("__padding", structModel.Members[1].Name);
    Assert.Equal("second", structModel.Members[2].Name);
    Assert.StartsWith("__padding", structModel.Members[3].Name);
    Assert.Equal("third", structModel.Members[4].Name);
    Assert.StartsWith("__padding", structModel.Members[5].Name);

    // Check array sizes
    Assert.NotNull(structModel.Members[1].TypeReference);
    Assert.Equal(2, structModel.Members[1].TypeReference!.ArraySize);
    Assert.NotNull(structModel.Members[3].TypeReference);
    Assert.Equal(8, structModel.Members[3].TypeReference!.ArraySize);
    Assert.NotNull(structModel.Members[5].TypeReference);
    Assert.Equal(1, structModel.Members[5].TypeReference!.ArraySize);
  }

  [Fact]
  public void ParseStructWithPaddingAndOffsets_PreservesOffsetInformation()
  {
    // Reset the padding counter to ensure consistent test results
    MemberParser.ResetPaddingCounter();

    string source = @"
/* 102 */
struct TestStruct
{
  int value; /* 0x000 */
  _BYTE[16]; /* 0x004 */
  char name[32]; /* 0x014 */
  _BYTE[8]; /* 0x034 */
};";

    var structs = TypeParser.ParseStructs(source);

    Assert.Single(structs);
    var structModel = structs[0];
    Assert.Equal(4, structModel.Members.Count);

    // Check offsets are preserved
    Assert.Equal(0, structModel.Members[0].Offset); // value
    Assert.Equal(4, structModel.Members[1].Offset); // __padding0
    Assert.Equal(0x14, structModel.Members[2].Offset); // name (20 in decimal)
    Assert.Equal(0x34, structModel.Members[3].Offset); // __padding1 (52 in decimal)
  }

  [Fact]
  public void ParseStructWithMixedPaddingAndRegularArrays_WorksCorrectly()
  {
    // Reset the padding counter to ensure consistent test results
    MemberParser.ResetPaddingCounter();

    string source = @"
/* 103 */
struct TestStruct
{
  int regular_int;
  char regular_array[10];
  _BYTE[5];  // This should become __padding0
 short another_field;
  _BYTE[3];  // This should become __padding1
};";

    var structs = TypeParser.ParseStructs(source);

    Assert.Single(structs);
    var structModel = structs[0];
    Assert.Equal(5, structModel.Members.Count);

    // Check each member
    Assert.Equal("regular_int", structModel.Members[0].Name);
    Assert.NotNull(structModel.Members[0].TypeReference);
    Assert.False(structModel.Members[0].TypeReference!.IsArray);

    Assert.Equal("regular_array", structModel.Members[1].Name);
    Assert.NotNull(structModel.Members[1].TypeReference);
    Assert.True(structModel.Members[1].TypeReference!.IsArray);
    Assert.Equal(10, structModel.Members[1].TypeReference!.ArraySize);

    Assert.StartsWith("__padding", structModel.Members[2].Name);
    Assert.NotNull(structModel.Members[2].TypeReference);
    Assert.True(structModel.Members[2].TypeReference!.IsArray);
    Assert.Equal(5, structModel.Members[2].TypeReference!.ArraySize);

    Assert.Equal("another_field", structModel.Members[3].Name);
    Assert.NotNull(structModel.Members[3].TypeReference);
    Assert.False(structModel.Members[3].TypeReference!.IsArray);

    Assert.StartsWith("__padding", structModel.Members[4].Name);
    Assert.NotNull(structModel.Members[4].TypeReference);
    Assert.True(structModel.Members[4].TypeReference!.IsArray);
    Assert.Equal(3, structModel.Members[4].TypeReference!.ArraySize);
  }
}
