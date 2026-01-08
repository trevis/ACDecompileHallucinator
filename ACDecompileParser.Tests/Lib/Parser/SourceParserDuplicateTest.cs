using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Parser;

public class SourceParserDuplicateTest
{
    [Fact]
    public void Parse_MultipleSourceFilesWithDifferentTypes_CountsAreCorrect()
    {
        // Create source content with different types in multiple files
        var sourceFile1 = @"
/* 4172 */
struct __cppobj AC1Legacy::flex_unit
{
  unsigned int *a;
 unsigned int z;
 unsigned int n;
};

/* 4173 */
struct __cppobj AC1Legacy::vlong_value : AC1Legacy::flex_unit
{
  unsigned int share;
};
";

        var sourceFile2 = @"
/* 4174 */
struct __cppobj AC1Legacy::another_struct
{
  int x;
};
";

        var sourceFileContents = new List<string> { sourceFile1, sourceFile2 };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // Check that we have the expected number of models
        // Before the fix: Each source file would add ALL previously parsed types to TypeModels again
        // After the fix: Each source file only adds newly parsed types to TypeModels
        Assert.Equal(3, parser.StructModels.Count); // Should have 3 unique structs total
        Assert.Equal(3, parser.TypeModels.Count);  // Should have 3 unique type models, not more
    }

    [Fact]
    public void Parse_MultipleSourceFilesWithSameTypes_BeforeAndAfterFixComparison()
    {
        // This test shows what the behavior would have been before vs after the fix
        // Before fix: same content in both files would lead to exponential growth in TypeModels
        // After fix: each file only adds its newly parsed types
        var sourceFile1 = @"
/* 4172 */
struct __cppobj AC1Legacy::flex_unit
{
  unsigned int *a;
  unsigned int z;
  unsigned int n;
};
";

        var sourceFile2 = @"
/* 4172 */
struct __cppobj AC1Legacy::flex_unit
{
  unsigned int *a;
  unsigned int z;
  unsigned int n;
};
";

        var sourceFileContents = new List<string> { sourceFile1, sourceFile2 };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        Assert.Single(parser.StructModels);
        Assert.Single(parser.TypeModels);
    }

    [Fact]
    public void Parse_SingleSourceFile_DoesNotCreateDuplicates()
    {
        var sourceFile = @"
/* 4172 */
struct __cppobj AC1Legacy::flex_unit
{
  unsigned int *a;
  unsigned int z;
  unsigned int n;
};

/* 4173 */
struct __cppobj AC1Legacy::vlong_value : AC1Legacy::flex_unit
{
  unsigned int share;
};
";

        var sourceFileContents = new List<string> { sourceFile };
        var parser = new SourceParser(sourceFileContents);
        parser.Parse();

        // With a single file, should have the expected count
        Assert.Equal(2, parser.StructModels.Count);
        Assert.Equal(2, parser.TypeModels.Count);

        // Verify that the types are unique
        var uniqueTypeNames = parser.TypeModels.Select(t => t.FullyQualifiedName).Distinct().ToList();
        Assert.Equal(2, uniqueTypeNames.Count);
    }
}
