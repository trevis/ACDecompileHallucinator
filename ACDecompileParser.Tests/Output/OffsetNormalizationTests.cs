using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using Xunit;

namespace ACDecompileParser.Tests.Output;

public class OffsetNormalizationTests
{
    [Fact]
    public void Generate_StaticMemberAddress_NormalizesTo32BitHex()
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
                    Name = "s_rDegradeDistance",
                    TypeString = "float",
                    Value = "50.0",
                    Address = "000000000081FC68"
                }
            }
        };

        var generator = new ClassOutputGenerator(null);
        var tokens = generator.Generate(type).ToList();
        var code = string.Join("", tokens.Select(t => t.Text));

        Assert.Contains("// 0x0081FC68", code);
        Assert.DoesNotContain("000000000081FC68", code);
    }

    [Fact]
    public void Generate_MethodSignatureOffset_PadsTo32BitHex()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "Render",
            Namespace = "",
            Source = "class Render",
            FunctionBodies = new List<FunctionBodyModel>
            {
                new FunctionBodyModel
                {
                    FullyQualifiedName = "Render::add_active_light",
                    Offset = 0x54CBC0,
                    FunctionSignature = new FunctionSignatureModel
                    {
                        Name = "add_active_light",
                        ReturnType = "void",
                        CallingConvention = "__cdecl",
                        Parameters = new List<FunctionParamModel>
                        {
                            new FunctionParamModel { Position = 0, Name = "index", ParameterType = "int32_t" },
                            new FunctionParamModel { Position = 1, Name = "lightClass", ParameterType = "int32_t" }
                        }
                    }
                }
            }
        };

        var generator = new ClassOutputGenerator(null);
        var tokens = generator.Generate(type).ToList();
        var code = string.Join("", tokens.Select(t => t.Text));

        Assert.Contains("// 0x0054CBC0", code);
        Assert.DoesNotContain("// 0x54CBC0", code);
    }

    [Fact]
    public void Generate_StructStaticMemberAddress_NormalizesTo32BitHex()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "TestStruct",
            Namespace = "Tests",
            Source = "struct TestStruct",
            StaticVariables = new List<StaticVariableModel>
            {
                new StaticVariableModel
                {
                    Name = "kValue",
                    TypeString = "int",
                    Address = "12345"
                }
            }
        };

        var generator = new StructOutputGenerator(null);
        var tokens = generator.Generate(type).ToList();
        var code = string.Join("", tokens.Select(t => t.Text));

        Assert.Contains("// 0x00012345", code);
    }
}
