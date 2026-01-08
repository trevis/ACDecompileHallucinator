using ACDecompileParser.Shared.Lib.Constants;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

public class PrimitiveTypeMappingTests
{
    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        "PrimitiveInplaceArray<ArchiveVersionRow.VersionEntry>")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", "PrimitiveInplaceArray<int>")]
    [InlineData("PrimitiveInplaceArray<SomeType*,20,8>", "PrimitiveInplaceArray<void*>")]
    [InlineData("int", "int")]
    [InlineData("ArchiveVersionRow::VersionEntry", "ArchiveVersionRow.VersionEntry")]
    public void MapType_PrimitiveInplaceArray_RemovesLiteralArgs(string input, string expected)
    {
        var result = PrimitiveTypeMappings.MapType(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        "PrimitiveInplaceArray<ArchiveVersionRow.VersionEntry>*")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", "PrimitiveInplaceArray<int>*")]
    public void MapTypeForStaticPointer_PrimitiveInplaceArray_RemovesLiteralArgs(string input, string expected)
    {
        var result = PrimitiveTypeMappings.MapTypeForStaticPointer(input);
        Assert.Equal(expected, result);
    }
}
