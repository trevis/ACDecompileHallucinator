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
    public void Test_Struct_Includes_Original_Type_Comment()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "AFrame",
            Namespace = "AC1Modern",
            Type = TypeType.Struct
        };

        var output = _generator.Generate(type);

        // Should contain the comment with original C++ name
        Assert.Contains("// AC1Modern::AFrame", output);
        // Comment should be exactly above the struct
        Assert.Contains("// AC1Modern::AFrame\npublic unsafe struct AFrame", output.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Test_Generic_Struct_Includes_Original_Type_Comment()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "DArray",
            Namespace = "ACBindings",
            Type = TypeType.Struct,
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new() { Position = 0, TypeString = "int" }
            }
        };

        var output = _generator.Generate(type);

        // Should contain the comment with original C++ name including templates
        Assert.Contains("// ACBindings::DArray<int>", output);
        // Comment should be exactly above the struct
        Assert.Contains("// ACBindings::DArray<int>\npublic unsafe struct DArray__int", output.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Test_Enum_Includes_Original_Type_Comment()
    {
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "OBJECT_TYPE",
            Namespace = "ACBindings",
            Type = TypeType.Enum
        };

        var output = _generator.Generate(type);

        // Should contain the comment with original C++ name
        Assert.Contains("// ACBindings::OBJECT_TYPE", output);
        // Comment should be exactly above the enum
        Assert.Contains("// ACBindings::OBJECT_TYPE\npublic enum OBJECT_TYPE", output.Replace("\r\n", "\n"));
    }
}
