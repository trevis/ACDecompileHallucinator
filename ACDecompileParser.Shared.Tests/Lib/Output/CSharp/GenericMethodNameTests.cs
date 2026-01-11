using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Lib.Parser;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class GenericMethodNameTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public GenericMethodNameTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_GenericMethodName_Flattening()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "UIElement",
            Type = TypeType.Struct,
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName = "UIElement::GetChildRecursiveTemplate<UIElement_Text>",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "int",
                        CallingConvention = "Stdcall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "UIElement*", Position = 0 },
                            new() { Name = "ID", ParameterType = "unsigned int", Position = 1 }
                        }
                    },
                    Offset = 0x00476820
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Expectation: The generic part <UIElement_Text> should be flattened to __UIElement_Text
        // Note: Stdcall is treated as static, so 'this' is explicit
        Assert.Contains(
            $"public static int GetChildRecursiveTemplate__UIElement_Text({CSharpBindingsGenerator.NAMESPACE}.UIElement* this_, uint ID)",
            output);
    }
}
