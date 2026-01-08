using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Tests.Parsers;

public class StaticValueParserReproductionTests
{
    [Fact]
    public void ParseValues_AvoidsOverparsing_WithVftableQuote()
    {
        var statics = new List<StaticVariableModel>
        {
            new StaticVariableModel { Name = "player_pos", TypeString = "Position" }
        };

        var sourceLines = new List<string>
        {
            "Position Render::player_pos =",
            "{",
            "  { &Position::`vftable' },",
            "  0u,",
            "  {",
            "    1.0,",
            "    0.0,",
            "    0.0,",
            "    0.0,",
            "    { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },",
            "    { { 0.0, 0.0, 0.0 } }",
            "  }",
            "};",
            "RenderPrefs Render::m_RenderPrefs = { 1u, false, true, false, 2u, 1u, 1u, 8u, 1u, 0u, 0u, 0.0, 90.0, 2u };"
        };

        // We run the parser
        StaticValueParser.ParseValues(statics, new List<List<string>> { sourceLines }, new List<string> { "source.c" });

        // If overparsing occurs, the value might contain the next line (RenderPrefs...) or be malformed.
        // The correct value should end at the semicolon of the first struct.
        // It definitely should NOT contain "Render::m_RenderPrefs".

        Assert.NotNull(statics[0].Value);
        Assert.DoesNotContain("m_RenderPrefs", statics[0].Value);
        Assert.EndsWith("}", statics[0].Value.Trim());
    }

    [Fact]
    public void ParseValues_HandlesComments_WithoutOverparsing()
    {
        // This tests if comments cause issues if they contain braces
        // Although the user case was about overparsing capturing subsequent values,
        // comments with braces are a common cause of parser confusion.
        var statics = new List<StaticVariableModel>
        {
            new StaticVariableModel { Name = "diffuse", TypeString = "RGBColor" }
        };

        var sourceLines = new List<string>
        {
            "RGBColor Render::diffuse = { 1.0, 1.0, 1.0 }; // { comment causing brace count to go up?",
            "int dword_NEXT = 1;",
        };

        StaticValueParser.ParseValues(statics, new List<List<string>> { sourceLines }, new List<string> { "source.c" });

        Assert.Equal("{ 1.0, 1.0, 1.0 }", statics[0].Value);
        Assert.DoesNotContain("dword_NEXT", statics[0].Value);
    }
}
