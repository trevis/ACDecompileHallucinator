using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Models;

public class MemberTypePointerDepthTests
{
    [Fact]
    public void ParseMemberPointerTypes_SinglePointer_HasPointerDepthOne()
    {
        // Test parsing a member with single pointer (char *)
        string source = @"
/* 1450 */
struct MemberTypePointerDepthTestStruct
{
  char *lpDecimalSep;
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Single(structs);

        var member = structs[0].Members[0];
        Assert.Equal("lpDecimalSep", member.Name);
        Assert.NotNull(member.TypeReference);
        Assert.True(member.TypeReference.IsPointer);
        Assert.Equal(1, member.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseMemberPointerTypes_DoublePointer_HasPointerDepthTwo()
    {
        // Test parsing a member with double pointer (char **)
        string source = @"
/* 1450 */
struct MemberTypePointerDepthTestStruct
{
  char **lpThousandSep;
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Single(structs);

        var member = structs[0].Members[0];
        Assert.Equal("lpThousandSep", member.Name);
        Assert.NotNull(member.TypeReference);
        Assert.True(member.TypeReference.IsPointer);
        Assert.Equal(2, member.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseMemberPointerTypes_TriplePointer_HasPointerDepthThree()
    {
        // Test parsing a member with triple pointer (char ***)
        string source = @"
/* 1450 */
struct MemberTypePointerDepthTestStruct
{
  char ***lpMilliSep;
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Single(structs);

        var member = structs[0].Members[0];
        Assert.Equal("lpMilliSep", member.Name);
        Assert.NotNull(member.TypeReference);
        Assert.True(member.TypeReference.IsPointer);
        Assert.Equal(3, member.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseMemberPointerTypes_AllVariations_InSingleStruct()
    {
        // Test parsing a struct with multiple pointer depth variations
        string source = @"
/* 1450 */
struct MemberTypePointerDepthTestStruct
{
  char *lpDecimalSep;
  char **lpThousandSep;
  char ***lpMilliSep;
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Single(structs);

        var structModel = structs[0];
        Assert.Equal(3, structModel.Members.Count);

        // Check first member (char *)
        var member1 = structModel.Members[0];
        Assert.Equal("lpDecimalSep", member1.Name);
        Assert.NotNull(member1.TypeReference);
        Assert.True(member1.TypeReference.IsPointer);
        Assert.Equal(1, member1.TypeReference.PointerDepth);

        // Check second member (char **)
        var member2 = structModel.Members[1];
        Assert.Equal("lpThousandSep", member2.Name);
        Assert.NotNull(member2.TypeReference);
        Assert.True(member2.TypeReference.IsPointer);
        Assert.Equal(2, member2.TypeReference.PointerDepth);

        // Check third member (char ***)
        var member3 = structModel.Members[2];
        Assert.Equal("lpMilliSep", member3.Name);
        Assert.NotNull(member3.TypeReference);
        Assert.True(member3.TypeReference.IsPointer);
        Assert.Equal(3, member3.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseMemberPointerTypes_NonPointerMember_HasPointerDepthZero()
    {
        // Test parsing a member without pointer
        string source = @"
/* 1450 */
struct MemberTypePointerDepthTestStruct
{
  char value;
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Single(structs);

        var member = structs[0].Members[0];
        Assert.Equal("value", member.Name);
        Assert.NotNull(member.TypeReference);
        Assert.False(member.TypeReference.IsPointer);
        Assert.Equal(0, member.TypeReference.PointerDepth);
    }

    [Fact]
    public void ParseMemberPointerTypes_PointerWithStruct_HasCorrectPointerDepth()
    {
        // Test parsing a pointer to struct
        string source = @"
/* 1450 */
struct MemberTypePointerDepthTestStruct
{
  struct MyStruct *ptr;
  struct MyStruct **ptrPtr;
};";

        var structs = TypeParser.ParseStructs(source);
        Assert.Single(structs);

        var structModel = structs[0];
        Assert.Equal(2, structModel.Members.Count);

        var member1 = structModel.Members[0];
        Assert.Equal("ptr", member1.Name);
        Assert.NotNull(member1.TypeReference);
        Assert.Equal(1, member1.TypeReference.PointerDepth);

        var member2 = structModel.Members[1];
        Assert.Equal("ptrPtr", member2.Name);
        Assert.NotNull(member2.TypeReference);
        Assert.Equal(2, member2.TypeReference.PointerDepth);
    }
}
