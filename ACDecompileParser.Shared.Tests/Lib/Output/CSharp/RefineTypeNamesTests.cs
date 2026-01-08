using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class RefineTypeNamesTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public RefineTypeNamesTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_DollarSign_Stripping()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "$A123",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new() { Name = "__s0", TypeString = "$B456", DeclarationOrder = 1 }
            },
            StaticVariables = new List<StaticVariableModel>
            {
                new() { Name = "staticPtr", TypeString = "$C789*", Address = "0x123" }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Verify struct name is stripped
        Assert.Contains("public unsafe struct _A123", output);
        Assert.DoesNotContain("$A123", output);

        // Verify member type is stripped (using MapType)
        Assert.Contains("public ACBindings._B456 __s0;", output);
        
        // Verify static member type is stripped (using MapTypeForStaticPointer)
        Assert.Contains("public static ACBindings._C789* staticPtr", output);
    }
}
