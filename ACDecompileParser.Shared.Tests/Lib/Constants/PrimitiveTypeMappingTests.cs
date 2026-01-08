using ACDecompileParser.Shared.Lib.Constants;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

public class PrimitiveTypeMappingTests
{
    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        "ACBindings.PrimitiveInplaceArray<ACBindings.ArchiveVersionRow.VersionEntry>")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", "ACBindings.PrimitiveInplaceArray<int>")]
    [InlineData("PrimitiveInplaceArray<SomeType*,20,8>", "ACBindings.PrimitiveInplaceArray<void*>")]
    [InlineData("int", "int")]
    [InlineData("ArchiveVersionRow::VersionEntry", "ACBindings.ArchiveVersionRow.VersionEntry")]
    [InlineData("Vector3", "ACBindings.Vector3")]
    public void MapType_PrimitiveInplaceArray_RemovesLiteralArgs(string input, string expected)
    {
        var result = PrimitiveTypeMappings.MapType(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        "ACBindings.PrimitiveInplaceArray<ACBindings.ArchiveVersionRow.VersionEntry>*")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", "ACBindings.PrimitiveInplaceArray<int>*")]
    public void MapTypeForStaticPointer_PrimitiveInplaceArray_RemovesLiteralArgs(string input, string expected)
    {
        var result = PrimitiveTypeMappings.MapTypeForStaticPointer(input);
        Assert.Equal(expected, result);
    }
}
