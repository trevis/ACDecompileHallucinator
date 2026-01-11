using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class StructCommentTests
{
    private readonly CSharpBindingsGenerator _generator;

    public StructCommentTests()
    {
        _generator = new CSharpBindingsGenerator();
    }


    [Fact]
    public void Test_Enum_Includes_Original_Type_Comment()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "OBJECT_TYPE",
            Namespace = CSharpBindingsGenerator.NAMESPACE,
            Type = TypeType.Enum
        };

        var output = _generator.Generate(type);

        // Should contain the comment with original C++ name
        Assert.Contains($"// {CSharpBindingsGenerator.NAMESPACE}::OBJECT_TYPE", output);
        // Comment should be exactly above the enum
        Assert.Contains($"// {CSharpBindingsGenerator.NAMESPACE}::OBJECT_TYPE\npublic enum OBJECT_TYPE", output.Replace("\r\n", "\n"));
    }
}
