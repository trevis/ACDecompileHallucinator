using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Tests.Parsers;

public class StaticValueParserMethodScopeTests
{
    [Fact]
    public void ParseValues_IgnoresAssignmentsInsideMethods()
    {
        var statics = new List<StaticVariableModel>
        {
            new StaticVariableModel { Name = "deg_mul", TypeString = "float" },
            new StaticVariableModel { Name = "max_static_lights", TypeString = "int" }
        };

        var sourceLines = new List<string>
        {
            "// Global initialization - this SHOULD be captured",
            "float Render::deg_mul = 1.0;",
            "",
            "int __cdecl Render::SetDegradeLevelInternal(float new_deg_mul)",
            "{",
            "  Render::deg_mul = new_deg_mul;", // Should NOT fail or overwrite with "new_deg_mul"
            "  if ( v2 | v3 )",
            "  {",
            "    // ...",
            "  }",
            "  Render::max_static_lights = v6;", // Should IGNORE this
            "  return 1;",
            "}"
        };

        // Initialize values to something known to ensure they aren't overwritten by "new_deg_mul" or "v6"
        // Actually the parser usually looking for initial values.
        // If the parser finds "Render::deg_mul = new_deg_mul;", it might think the value is "new_deg_mul".
        // The first line "float Render::deg_mul = 1.0;" should set it to "1.0".
        // If we process the file sequentially, later assignments might overwrite it if we don't check for existence (which we do, line 92 in original file `if (candidate.Value != null) continue;`).
        // BUT: What if the function comes BEFORE the definition? Or if the definition is missing (e.g. extern)?
        // The user request says "the static value parser is overparsing", implying it's finding these assignments and treating them as the static value.
        // If I put the function first, it will definitely pick it up if not scoped.

        var sourceLinesReordered = new List<string>
        {
            "int __cdecl Render::SetDegradeLevelInternal(float new_deg_mul)",
            "{",
            "  Render::deg_mul = new_deg_mul;",
            "  Render::max_static_lights = v6;",
            "}",
            "float Render::deg_mul = 1.0;",
            "int Render::max_static_lights = 100;"
        };

        // Reset values
        foreach (var s in statics) s.Value = null;

        StaticValueParser.ParseValues(statics, new List<List<string>> { sourceLinesReordered },
            new List<string> { "source.c" });

        Assert.Equal("1.0", statics[0].Value);
        Assert.Equal("100", statics[1].Value);
        Assert.DoesNotContain("new_deg_mul", statics[0].Value);
        Assert.DoesNotContain("v6", statics[1].Value);
    }
}
