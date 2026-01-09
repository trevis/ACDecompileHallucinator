using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class UserPurgeTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public UserPurgeTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_Method_With_UserPurge_ShouldBe_CommentedOut()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "TestStruct",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>(),
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName = "TestStruct::StartTooltip",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        // Simulate __userpurge in return type which causes it to appear in output
                        ReturnType = "void __userpurge",
                        CallingConvention = "Cdecl",
                        Parameters = new List<FunctionParamModel>
                        {
                             new() { Name = "this", ParameterType = "TestStruct*", Position = 0 }
                        }
                    },
                    Offset = 0x0045DF70
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Current behavior (broken/undesired): The method might be generated normally or with valid C# syntax that includes "void __userpurge".
        // Desired behavior: The line should be commented out.
        
        // Assert that the line is commented out
        Assert.Matches(@"(?m)^\s*//.*StartTooltip.*", output);
    }
}
