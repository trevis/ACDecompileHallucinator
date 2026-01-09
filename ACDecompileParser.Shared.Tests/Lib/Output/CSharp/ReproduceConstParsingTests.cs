using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class ReproduceConstParsingTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public ReproduceConstParsingTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_Const_In_TemplateArgument_Parsing()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "CObjCell",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                // Reproduce: DArray<LIGHTOBJ const *> light_list;
                new()
                {
                    Name = "light_list",
                    TypeString = "DArray<LIGHTOBJ const *>",
                    DeclarationOrder = 1
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Verify the fix
        // We expect ACBindings.DArray<ACBindings.LIGHTOBJ*> or similar valid C#
        // Definitely NOT const inside generics
        Assert.DoesNotContain("const", output);
        Assert.Contains("public ACBindings.DArray<Ptr<ACBindings.LIGHTOBJ>> light_list;", output);
    }
}
