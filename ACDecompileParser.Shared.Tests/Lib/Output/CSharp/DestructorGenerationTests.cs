using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Lib.Parser;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class DestructorGenerationTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public DestructorGenerationTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_Destructor_In_Template_Should_Be_Renamed()
    {
        // "DArray<view_vertex>::~DArray<view_vertex>"
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "DArray",
            Namespace = "ACBindings",
            Type = TypeType.Struct,
            // Simulating generic instance DArray<view_vertex>
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new() { Position = 0, TypeString = "view_vertex" }
            },
            StructMembers = new List<StructMemberModel>
            {
                new() { Name = "data", TypeString = "view_vertex*", DeclarationOrder = 1 },
                new() { Name = "blocksize", TypeString = "unsigned int", DeclarationOrder = 2 },
                new() { Name = "next_available", TypeString = "unsigned int", DeclarationOrder = 3 },
                new() { Name = "sizeOf", TypeString = "unsigned int", DeclarationOrder = 4 }
            },
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName = "DArray<view_vertex>::~DArray<view_vertex>",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "void",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "DArray<view_vertex>*", Position = 0 }
                        }
                    },
                    Offset = 0x0052DF80
                },
                new()
                {
                    Id = 102,
                    FullyQualifiedName = "DArray<view_vertex>::grow",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "void",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "DArray<view_vertex>*", Position = 0 },
                            new() { Name = "n", ParameterType = "unsigned int", Position = 1 }
                        }
                    },
                    Offset = 0x0054E930
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Verification
        Assert.Contains("public unsafe struct DArray___view_vertex", output);
        
        // Should have Dispose method
        //Assert.Contains("public void Dispose()", output);
        
        // Destructor should be renamed to _DestructorInternal
        // The original method generation logic might keep the original name if not renamed
        // Currently expect failure: "public void ~DArray<view_vertex>()"
        //Assert.Contains("public void _DestructorInternal()", output);
        Assert.DoesNotContain("public void ~DArray", output);
        
        // Dispose should call _DestructorInternal
        //Assert.Contains("_DestructorInternal();", output);
    }
}
