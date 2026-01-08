using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Parser;

public class ForwardDeclarationTests
{
    [Fact]
    public void Parse_ForwardDeclarationThenFullDeclaration_MergesCorrectly()
    {
        // Test case: forward declaration followed by full declaration
        var sourceFile = @"
/* 11361 */
struct ClientMain;

/* 11362 */
struct ClientMain
{
  int adjectives;
}
";

        var sourceFileContents = new List<string> { sourceFile };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have only one struct (merged)
        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);

        // The struct should have the member from the full declaration
        var structModel = parser.StructModels.First();
        Assert.Equal("ClientMain", structModel.Name);
        Assert.Single(structModel.Members);
        Assert.Equal("adjectives", structModel.Members.First().Name);
    }

    [Fact]
    public void Parse_FullDeclarationThenForwardDeclaration_KeepsFullDeclaration()
    {
        // Test case: full declaration followed by forward declaration
        var sourceFile = @"
/* 11363 */
struct AMesh
{
  int dispCatchObj;
}

/* 11364 */
struct AMesh;
";

        var sourceFileContents = new List<string> { sourceFile };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have only one struct (kept the full one)
        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);

        // The struct should have the member from the full declaration
        var structModel = parser.StructModels.First();
        Assert.Equal("AMesh", structModel.Name);
        Assert.Single(structModel.Members);
        Assert.Equal("dispCatchObj", structModel.Members.First().Name);
    }

    [Fact]
    public void Parse_ForwardDeclarationThenFullDeclarationAcrossFiles_MergesCorrectly()
    {
        // Test case: forward declaration in first file, full declaration in second file
        var sourceFile1 = @"
/* 11361 */
struct ClientMain;
";

        var sourceFile2 = @"
/* 11362 */
struct ClientMain
{
  int adjectives;
}
";

        var sourceFileContents = new List<string> { sourceFile1, sourceFile2 };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have only one struct (merged)
        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);

        // The struct should have the member from the full declaration
        var structModel = parser.StructModels.First();
        Assert.Equal("ClientMain", structModel.Name);
        Assert.Single(structModel.Members);
        Assert.Equal("adjectives", structModel.Members.First().Name);
    }

    [Fact]
    public void Parse_FullDeclarationThenForwardDeclarationAcrossFiles_KeepsFullDeclaration()
    {
        // Test case: full declaration in first file, forward declaration in second file
        var sourceFile1 = @"
/* 11363 */
struct AMesh
{
  int dispCatchObj;
}
";

        var sourceFile2 = @"
/* 11364 */
struct AMesh;
";

        var sourceFileContents = new List<string> { sourceFile1, sourceFile2 };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have only one struct (kept the full one)
        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);

        // The struct should have the member from the full declaration
        var structModel = parser.StructModels.First();
        Assert.Equal("AMesh", structModel.Name);
        Assert.Single(structModel.Members);
        Assert.Equal("dispCatchObj", structModel.Members.First().Name);
    }

    [Fact]
    public void Parse_OnlyForwardDeclaration_CreatesEmptyStruct()
    {
        // Test case: only a forward declaration, no full declaration
        var sourceFile = @"
/* 11361 */
struct ClientMain;
";

        var sourceFileContents = new List<string> { sourceFile };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have one struct with no members
        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);

        var structModel = parser.StructModels.First();
        Assert.Equal("ClientMain", structModel.Name);
        Assert.Empty(structModel.Members);
    }

    [Fact]
    public void Parse_MultipleForwardDeclarations_CreatesOnlyOneStruct()
    {
        // Test case: multiple forward declarations of the same struct
        var sourceFile = @"
/* 11361 */
struct ClientMain;

/* 11362 */
struct ClientMain;
";

        var sourceFileContents = new List<string> { sourceFile };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have only one struct
        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);

        var structModel = parser.StructModels.First();
        Assert.Equal("ClientMain", structModel.Name);
        Assert.Empty(structModel.Members);
    }

    [Fact]
    public void Parse_ForwardDeclarationWithNamespace_MergesCorrectly()
    {
        // Test case: forward declaration and full declaration with namespace
        var sourceFile = @"
/* 11361 */
struct AC1Legacy::ClientMain;

/* 11362 */
struct AC1Legacy::ClientMain
{
  int adjectives;
}
";

        var sourceFileContents = new List<string> { sourceFile };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have only one struct (merged)
        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);

        var structModel = parser.StructModels.First();
        Assert.Equal("ClientMain", structModel.Name);
        Assert.Equal("AC1Legacy", structModel.Namespace);
        Assert.Single(structModel.Members);
        Assert.Equal("adjectives", structModel.Members.First().Name);
    }

    [Fact]
    public void Parse_TwoFullDeclarations_KeepsFirstAndWarns()
    {
        // Test case: two full declarations (true duplicate)
        var sourceFile = @"
/* 11361 */
struct ClientMain
{
  int adjectives;
}

/* 11362 */
struct ClientMain
{
  int other_field;
}
";

        var sourceFileContents = new List<string> { sourceFile };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have only one struct (the first one)
        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);

        var structModel = parser.StructModels.First();
        Assert.Equal("ClientMain", structModel.Name);
        // Should have the member from the first declaration, not the second
        Assert.Single(structModel.Members);
        Assert.Equal("adjectives", structModel.Members.First().Name);
    }

    [Fact]
    public void Parse_ForwardDeclarationFollowedByEnum_DoesNotMixMembers()
    {
        // Test case: forward declaration followed by enum (regression test)
        // The parser should not try to parse the enum members as struct members
        var sourceFile = @"
/* 11363 */
struct AMesh;

/* 11364 */
enum CLUSAGE
{
  D3DDECLUSAGE_POSITION = 0x0,
  D3DDECLUSAGE_BLENDWEIGHT = 0x1,
  D3DDECLUSAGE_BLENDINDICES = 0x2,
  D3DDECLUSAGE_NORMAL = 0x3,
  D3DDECLUSAGE_PSIZE = 0x4,
  D3DDECLUSAGE_TEXCOORD = 0x5,
  D3DDECLUSAGE_TANGENT = 0x6,
  D3DDECLUSAGE_BINORMAL = 0x7,
  D3DDECLUSAGE_TESSFACTOR = 0x8,
  D3DDECLUSAGE_POSITIONT = 0x9,
  D3DDECLUSAGE_COLOR = 0xA,
  D3DDECLUSAGE_FOG = 0xB,
  D3DDECLUSAGE_DEPTH = 0xC,
  D3DDECLUSAGE_SAMPLE = 0xD,
};
";

        var sourceFileContents = new List<string> { sourceFile };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Should have one struct and one enum
        Assert.Single(parser.StructModels);
        Assert.Single(parser.EnumModels);

        var structModel = parser.StructModels.First();
        Assert.Equal("AMesh", structModel.Name);
        // The struct should have NO members (it's a forward declaration)
        Assert.Empty(structModel.Members);

        var enumModel = parser.EnumModels.First();
        Assert.Equal("CLUSAGE", enumModel.Name);
        // The enum should have all its members
        Assert.Equal(14, enumModel.Members.Count);
    }
}
