using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using Xunit;

namespace ACDecompileParser.Tests.Output;

public class StaticMemberOutputTests
{
    [Fact]
    public void Generate_StaticMemberWithValue_AddsConst()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "TestClass",
            Namespace = "Tests",
            Source = "class TestClass",
            StaticVariables = new List<StaticVariableModel>
            {
                new StaticVariableModel
                {
                    Name = "kValue",
                    TypeString = "float",
                    Value = "50.0f",
                    Address = "0x123456"
                }
            }
        };

        var generator = new ClassOutputGenerator(null);
        var tokens = generator.Generate(type).ToList();
        var code = string.Join("", tokens.Select(t => t.Text));

        // Normalize whitespace for easier assertion
        var normalizedCode = System.Text.RegularExpressions.Regex.Replace(code, @"\s+", " ");

        if (normalizedCode.Contains("static const float32_t kValue") ||
            !normalizedCode.Contains("static float32_t kValue = 50.0f;"))
        {
            Assert.Fail($"Expected 'static float32_t kValue = 50.0f;' without const. Actual: '{normalizedCode}'");
        }
    }

    [Fact]
    public void Generate_StaticMemberWithoutValue_NoConst()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "TestClass",
            Namespace = "Tests",
            Source = "class TestClass",
            StaticVariables = new List<StaticVariableModel>
            {
                new StaticVariableModel
                {
                    Name = "kValue",
                    TypeString = "float",
                    Address = "0x123456"
                }
            }
        };

        var generator = new ClassOutputGenerator(null);
        var tokens = generator.Generate(type).ToList();
        var code = string.Join("", tokens.Select(t => t.Text));

        // Normalize whitespace
        var normalizedCode = System.Text.RegularExpressions.Regex.Replace(code, @"\s+", " ");

        Assert.Contains("static float32_t kValue;", normalizedCode);
        Assert.DoesNotContain("const float32_t kValue", normalizedCode);
    }
}
