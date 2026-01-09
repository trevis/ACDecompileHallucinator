using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Parser;

public class MemberParserTests
{
    #region Bit Field Parsing Tests

    [Fact]
    public void ParseMemberDeclaration_ParsesSingleBitField()
    {
        // Arrange
        var line = "unsigned __int32 AllowDemotion : 1;";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("AllowDemotion", result.Name);
        Assert.Equal(1, result.BitFieldWidth);
        Assert.Equal("unsigned __int32", result.TypeString);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesMultiBitField()
    {
        // Arrange
        var line = "unsigned __int32 Reserved : 30;";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Reserved", result.Name);
        Assert.Equal(30, result.BitFieldWidth);
        Assert.Equal("unsigned __int32", result.TypeString);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesBitFieldWithUnsignedInt()
    {
        // Arrange
        var line = "unsigned int AllowPromotion : 1;";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("AllowPromotion", result.Name);
        Assert.Equal(1, result.BitFieldWidth);
        Assert.Equal("unsigned int", result.TypeString);
    }

    [Fact]
    public void ParseMemberDeclaration_RegularMemberHasNullBitFieldWidth()
    {
        // Arrange
        var line = "unsigned int TimeCheck;";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TimeCheck", result.Name);
        Assert.Null(result.BitFieldWidth);
        Assert.Equal("unsigned int", result.TypeString);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesProcessorPowerPolicyInfoBitFields()
    {
        // Test the exact example from the user's input
        var line1 = "unsigned __int32 AllowDemotion : 1;";
        var line2 = "unsigned __int32 AllowPromotion : 1;";
        var line3 = "unsigned __int32 Reserved : 30;";

        var result1 = MemberParser.ParseMemberDeclaration(line1);
        var result2 = MemberParser.ParseMemberDeclaration(line2);
        var result3 = MemberParser.ParseMemberDeclaration(line3);

        // Assert
        Assert.NotNull(result1);
        Assert.Equal("AllowDemotion", result1.Name);
        Assert.Equal(1, result1.BitFieldWidth);

        Assert.NotNull(result2);
        Assert.Equal("AllowPromotion", result2.Name);
        Assert.Equal(1, result2.BitFieldWidth);

        Assert.NotNull(result3);
        Assert.Equal("Reserved", result3.Name);
        Assert.Equal(30, result3.BitFieldWidth);
    }

    [Fact]
    public void ParseMemberDeclaration_MixedBitFieldAndRegularMembers()
    {
        // Test a struct with mixed bit field and regular members
        var regularMember = "char DemotePercent;";
        var bitFieldMember = "unsigned int flag : 1;";

        var regularResult = MemberParser.ParseMemberDeclaration(regularMember);
        var bitFieldResult = MemberParser.ParseMemberDeclaration(bitFieldMember);

        Assert.NotNull(regularResult);
        Assert.Null(regularResult.BitFieldWidth);
        Assert.Equal("DemotePercent", regularResult.Name);

        Assert.NotNull(bitFieldResult);
        Assert.Equal(1, bitFieldResult.BitFieldWidth);
        Assert.Equal("flag", bitFieldResult.Name);
    }

    #endregion

    #region Alignment Parsing Tests

    [Fact]
    public void ParseMemberDeclaration_ParsesFunctionPointerWithAlignment()
    {
        // Arrange - the exact example from the user's input
        var line = "MD_Data_Anim *(__thiscall *DynamicCast_Anim)(MediaDesc *this) __declspec(align(8));";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DynamicCast_Anim", result.Name);
        Assert.True(result.IsFunctionPointer);
        Assert.Equal(8, result.Alignment);
        Assert.Equal("__thiscall", result.FunctionSignature!.CallingConvention);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesMultipleFunctionPointersWithAlignment()
    {
        // Test all three function pointers from the vtable example
        var line1 = "MD_Data_Anim *(__thiscall *DynamicCast_Anim)(MediaDesc *this) __declspec(align(8));";
        var line2 = "MD_Data_Image *(__thiscall *DynamicCast_Image)(MediaDesc *this) __declspec(align(8));";
        var line3 = "MD_Data_Alpha *(__thiscall *DynamicCast_Alpha)(MediaDesc *this) __declspec(align(8));";

        var result1 = MemberParser.ParseMemberDeclaration(line1);
        var result2 = MemberParser.ParseMemberDeclaration(line2);
        var result3 = MemberParser.ParseMemberDeclaration(line3);

        // Assert
        Assert.NotNull(result1);
        Assert.Equal("DynamicCast_Anim", result1.Name);
        Assert.Equal(8, result1.Alignment);
        Assert.True(result1.IsFunctionPointer);

        Assert.NotNull(result2);
        Assert.Equal("DynamicCast_Image", result2.Name);
        Assert.Equal(8, result2.Alignment);
        Assert.True(result2.IsFunctionPointer);

        Assert.NotNull(result3);
        Assert.Equal("DynamicCast_Alpha", result3.Name);
        Assert.Equal(8, result3.Alignment);
        Assert.True(result3.IsFunctionPointer);
    }

    [Fact]
    public void ParseMemberDeclaration_ParsesAlignment16()
    {
        // Arrange
        var line = "void *(__stdcall *SomeFunc)(int param) __declspec(align(16));";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SomeFunc", result.Name);
        Assert.True(result.IsFunctionPointer);
        Assert.Equal(16, result.Alignment);
    }

    [Fact]
    public void ParseMemberDeclaration_RegularMemberHasNullAlignment()
    {
        // Arrange
        var line = "int someValue;";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("someValue", result.Name);
        Assert.Null(result.Alignment);
    }

    [Fact]
    public void ParseMemberDeclaration_RegularMemberWithAlignment()
    {
        // Arrange - regular member with alignment (less common but possible)
        var line = "double value __declspec(align(16));";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("value", result.Name);
        Assert.Equal(16, result.Alignment);
        Assert.False(result.IsFunctionPointer);
    }

    [Fact]
    public void ParseMemberDeclaration_FunctionPointerWithoutAlignment()
    {
        // Arrange
        var line = "void *(__thiscall *SomeFunc)(int param);";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SomeFunc", result.Name);
        Assert.True(result.IsFunctionPointer);
        Assert.Null(result.Alignment);
    }
    [Fact]
    public void ParseMemberDeclaration_HandlesDecompilerComments()
    {
        // Arrange
        var line = "/* 0x012 */ // local variable allocation has failed\nbool MyMember;";

        // Act
        var result = MemberParser.ParseMemberDeclaration(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MyMember", result.Name);
        Assert.Equal("bool", result.TypeString);
    }

    #endregion
}
