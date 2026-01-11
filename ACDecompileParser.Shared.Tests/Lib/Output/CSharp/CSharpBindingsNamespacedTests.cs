using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class CSharpBindingsNamespacedTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public CSharpBindingsNamespacedTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_Namespaced_Struct_Delegate()
    {
        var vectorType = new TypeModel
        {
            Id = 1,
            BaseName = "Vector3",
            Namespace = "AC1Legacy",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new() { Name = "baseclass_0", TypeString = "Vector3", DeclarationOrder = 1 }
            },
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName = "AC1Legacy::Vector3::normalize_check_small",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "int",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "AC1Legacy::Vector3*", Position = 0 }
                        }
                    },
                    Offset = 0x004524A0
                }
            }
        };

        // Use GenerateWithNamespace to simulate full flow
        var output = _generator.GenerateWithNamespace(new List<TypeModel> { vectorType });
        _testOutput.WriteLine(output);

        Assert.Contains($"namespace {CSharpBindingsGenerator.NAMESPACE}.AC1Legacy;", output);
        Assert.Contains("public unsafe struct Vector3", output);
        
        // This is the check failing for the user allegedly
        // Expecting: delegate* unmanaged[Thiscall]<ref ACBindings.AC1Legacy.Vector3, int>
        Assert.Contains($"delegate* unmanaged[Thiscall]<ref {CSharpBindingsGenerator.NAMESPACE}.AC1Legacy.Vector3, int>", output);
    }
}
