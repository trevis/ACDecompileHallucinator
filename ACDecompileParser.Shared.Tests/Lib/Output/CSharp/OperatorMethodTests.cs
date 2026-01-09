using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class OperatorMethodTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly ITestOutputHelper _testOutput;

    public OperatorMethodTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_OperatorMethod_IsCommentedOut()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "TestStruct",
            Type = TypeType.Struct,
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName = "TestStruct::operator__",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "byte",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "TestStruct*", Position = 0 },
                            new() { Name = "rhs", ParameterType = "void*", Position = 1 }
                        }
                    },
                    Offset = 0x00424400
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Verify the line is commented out (allow for indentation)
        Assert.Matches(@"//\s+public byte operator__", output);
        // Verify it is not present as active code
        Assert.DoesNotContain("\n    public byte operator__", output);
    }
}
