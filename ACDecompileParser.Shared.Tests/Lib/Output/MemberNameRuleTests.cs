using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Rules;
using ACDecompileParser.Shared.Lib.Services;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class MemberNameRuleTests
{
    [Fact]
    public void MemberNameRule_Matches_FieldName()
    {
        // Arrange
        var rule = new MemberNameRule("myField", "Test/");
        var type = new TypeModel
        {
            Type = TypeType.Struct,
            Source = "struct TestStruct {\n    int myField;\n    float otherField;\n};"
        };

        var mockGraph = new Mock<IInheritanceGraph>();

        // Act
        var matches = rule.Matches(type, mockGraph.Object, out var result);

        // Assert
        Assert.True(matches);
        Assert.NotNull(result);
        Assert.Equal("Test/", result.Prefix);
    }

    [Fact]
    public void MemberNameRule_Matches_MethodName()
    {
        // Arrange
        var rule = new MemberNameRule("myMethod", "Test/");
        var type = new TypeModel
        {
            Type = TypeType.Struct,
            Source = "struct TestStruct {\n    int myField;\n    void myMethod();\n};"
        };

        var mockGraph = new Mock<IInheritanceGraph>();

        // Act
        var matches = rule.Matches(type, mockGraph.Object, out var result);

        // Assert
        Assert.True(matches);
        Assert.NotNull(result);
        Assert.Equal("Test/", result.Prefix);
    }

    [Fact]
    public void MemberNameRule_DoesNotMatch_NonExistentMember()
    {
        // Arrange
        var rule = new MemberNameRule("nonExistent", "Test/");
        var type = new TypeModel
        {
            Type = TypeType.Struct,
            Source = "struct TestStruct {\n    int myField;\n    void myMethod();\n};"
        };

        var mockGraph = new Mock<IInheritanceGraph>();

        // Act
        var matches = rule.Matches(type, mockGraph.Object, out var result);

        // Assert
        Assert.False(matches);
        Assert.Null(result);
    }

    [Fact]
    public void MemberNameRule_DoesNotMatch_NonStructType()
    {
        // Arrange
        var rule = new MemberNameRule("myField", "Test/");
        var type = new TypeModel
        {
            Type = TypeType.Enum, // Not a struct
            Source = "enum TestEnum {\n    myField,\n    otherField\n};"
        };

        var mockGraph = new Mock<IInheritanceGraph>();

        // Act
        var matches = rule.Matches(type, mockGraph.Object, out var result);

        // Assert
        Assert.False(matches);
        Assert.Null(result);
    }

    [Fact]
    public void MemberNameRule_Matches_WithRegexPattern()
    {
        // Arrange
        var rule = new MemberNameRule(@"^my.*", "Test/"); // Matches any member starting with "my"
        var type = new TypeModel
        {
            Type = TypeType.Struct,
            Source = "struct TestStruct {\n    int myField;\n    float myOtherField;\n    void someMethod();\n};"
        };

        var mockGraph = new Mock<IInheritanceGraph>();

        // Act
        var matches = rule.Matches(type, mockGraph.Object, out var result);

        // Assert
        Assert.True(matches);
        Assert.NotNull(result);
        Assert.Equal("Test/", result.Prefix);
    }

    [Fact]
    public void MemberNameRule_Matches_Destructor()
    {
        // Arrange
        var rule = new MemberNameRule("~TestStruct", "Test/");
        var type = new TypeModel
        {
            Type = TypeType.Struct,
            Source = "struct TestStruct {\n    void ~TestStruct();\n};"
        };

        var mockGraph = new Mock<IInheritanceGraph>();

        // Act
        var matches = rule.Matches(type, mockGraph.Object, out var result);

        // Assert
        Assert.True(matches);
        Assert.NotNull(result);
        Assert.Equal("Test/", result.Prefix);
    }

    [Fact]
    public void MemberNameRule_Matches_VTableDestructor()
    {
        // Arrange
        var rule = new MemberNameRule("~CAnimHook", "Test/");
        var type = new TypeModel
        {
            Type = TypeType.Struct,
            Source = "struct CAnimHook_vtbl {\n    void (__thiscall *~CAnimHook)(CAnimHook* this);\n};"
        };

        var mockGraph = new Mock<IInheritanceGraph>();

        // Act
        var matches = rule.Matches(type, mockGraph.Object, out var result);

        // Assert
        Assert.True(matches);
        Assert.NotNull(result);
    }
}

