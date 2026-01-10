using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class CSharpBindingsGeneratorTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly ITestOutputHelper _testOutput;

    public CSharpBindingsGeneratorTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_Generate_MethodWithFullSignatureInFQN_ExtractsNameCorrectly()
    {
        // Setup a type with a method having the problematic FQN
        // FQN: unsigned int __stdcall APIManager::IAsheronsCallImpl::AddRef(APIManager::IAsheronsCallImpl*)

        var type = new TypeModel
        {
            Id = 1,
            BaseName = "IAsheronsCallImpl",
            Namespace = "APIManager",
            Type = TypeType.Struct,
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName =
                        "unsigned int __stdcall APIManager::IAsheronsCallImpl::AddRef(APIManager::IAsheronsCallImpl*)",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "uint",
                        CallingConvention = "Stdcall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "APIManager::IAsheronsCallImpl*", Position = 0 }
                        }
                    },
                    Offset = 0x0055A7B0
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Verify that the method AddRef is generated correctly
        Assert.Contains("public static uint AddRef(", output);

        // Ensure it doesn't contain the garbage extraction
        Assert.DoesNotContain("IAsheronsCallImpl*)", output);
    }

    [Fact]
    public void Test_ExtractMethodName_WithOperator_HandlesCorrectly()
    {
        // Test implicit operator/global function names if relevant, though mostly checked via Generate
        var type = new TypeModel
        {
            Id = 2,
            BaseName = "TestStruct",
            Type = TypeType.Struct,
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 201,
                    FullyQualifiedName = "void operator delete(void*)",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "void",
                        CallingConvention = "Cdecl",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "ptr", ParameterType = "void*", Position = 0 }
                        }
                    },
                    Offset = 0x1000
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Should contain "operator delete" but commented out because it starts with "operator"
        Assert.Contains("public static void operator delete(", output);
    }
}
