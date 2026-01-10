using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class MethodCommentTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly ITestOutputHelper _testOutput;

    public MethodCommentTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_Method_Original_Signature_Comments()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "Render",
            Type = TypeType.Struct,
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName = "Render::Set3DView",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        FullyQualifiedName = "int __thiscall Render::Set3DView(int x, int y)",
                        ReturnType = "int",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "Render*", Position = 0 },
                            new() { Name = "x", ParameterType = "int", Position = 1 },
                            new() { Name = "y", ParameterType = "int", Position = 2 }
                        }
                    },
                    Offset = 0x0054FC80
                },
                new()
                {
                    Id = 102,
                    FullyQualifiedName = "Render::StaticMethod",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        FullyQualifiedName = "void __cdecl Render::StaticMethod(int a)",
                        ReturnType = "void",
                        CallingConvention = "Cdecl",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "a", ParameterType = "int", Position = 0 }
                        }
                    },
                    Offset = 0x00123456
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Verify comments are present above methods
        Assert.Contains("int __thiscall Render::Set3DView(int x, int y)", output);
        Assert.Contains("void __cdecl Render::StaticMethod(int a)", output);
        
    }
}
