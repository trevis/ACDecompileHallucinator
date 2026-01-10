using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class VTableDelegateTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public VTableDelegateTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_VTable_Member_Generates_Delegate()
    {
        // Model a VTable struct with a function pointer member
        // void (__thiscall *OnStartup)(ICIDM *this, int);
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "ICIDM_vtbl",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new()
                {
                    Name = "OnStartup",
                    DeclarationOrder = 1,
                    IsFunctionPointer = true,
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "bool",
                        CallingConvention = "__thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "ICIDM*", Position = 0 },
                            new() { Name = "arg1", ParameterType = "int", Position = 1 }
                        }
                    }
                }
            }
        };

        // Test with pre-loaded signature
        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        Assert.DoesNotContain("System.IntPtr OnStartup;", output);
        Assert.Contains("delegate* unmanaged[Thiscall]<", output);
        Assert.Contains("OnStartup;", output);
    }

    [Fact]
    public void Test_VTable_Member_Defensive_Load()
    {
        // Setup mock for lazy loading
        var signatureId = 100;
        var signature = new FunctionSignatureModel
        {
            Id = 100,
            ReturnType = "void",
            CallingConvention = "__cdecl",
            Parameters = new List<FunctionParamModel>()
        };

        _mockRepository.Setup(r => r.GetFunctionSignatureById(signatureId))
            .Returns(signature);

        var type = new TypeModel
        {
            Id = 2,
            BaseName = "LazyStruct",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new()
                {
                    Name = "LazyFunc",
                    IsFunctionPointer = true,
                    FunctionSignature = null, // Intentional null
                    FunctionSignatureId = signatureId
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Verify repository was called
        _mockRepository.Verify(r => r.GetFunctionSignatureById(signatureId), Times.Once);

        // Verify output
        Assert.DoesNotContain("System.IntPtr LazyFunc;", output);
        Assert.Contains("delegate* unmanaged[Cdecl]<void> LazyFunc;", output);
    }
}
