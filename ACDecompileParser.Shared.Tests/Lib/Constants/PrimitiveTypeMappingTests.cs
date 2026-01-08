using ACDecompileParser.Shared.Lib.Constants;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

public class PrimitiveTypeMappingTests
{
    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        "ACBindings.PrimitiveInplaceArray<ACBindings.ArchiveVersionRow.VersionEntry>")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", "ACBindings.PrimitiveInplaceArray<int>")]
    [InlineData("PrimitiveInplaceArray<SomeType*,20,8>", "ACBindings.PrimitiveInplaceArray<Ptr<ACBindings.SomeType>>")]
    [InlineData("int", "int")]
    [InlineData("ArchiveVersionRow::VersionEntry", "ACBindings.ArchiveVersionRow.VersionEntry")]
    [InlineData("Vector3", "ACBindings.Vector3")]
    [InlineData("SmartArray<ACCharGenStartArea,1>", "ACBindings.SmartArray<ACBindings.ACCharGenStartArea>")]
    [InlineData("HashTable<unsigned long,HeritageGroup_CG,0>",
        "ACBindings.HashTable<uint,ACBindings.HeritageGroup_CG>")]
    [InlineData("Foo<Bar<int, 5>, 10>", "ACBindings.Foo<ACBindings.Bar<int>>")]
    [InlineData("void*", "System.IntPtr")]
    [InlineData("SmartArray<void*, 1>", "ACBindings.SmartArray<System.IntPtr>")]
    [InlineData("PrimitiveInplaceArray<void*, 8, 1>", "ACBindings.PrimitiveInplaceArray<System.IntPtr>")]
    [InlineData("HashTable<void*, int, 0>", "ACBindings.HashTable<System.IntPtr,int>")]
    [InlineData("DArray<UnknownType*>", "ACBindings.DArray<Ptr<ACBindings.UnknownType>>")]
    [InlineData("Type$With$Dollars", "ACBindings.Type_With_Dollars")]
    [InlineData("SmartArray<UIChildFramework*>", "ACBindings.SmartArray<Ptr<ACBindings.UIChildFramework>>")]
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

    [Theory]
    [InlineData("ACBindings.SomeType*", "Ptr<ACBindings.SomeType>")]
    [InlineData("ACBindings.UIChildFramework*", "Ptr<ACBindings.UIChildFramework>")]
    [InlineData("System.IntPtr", "System.IntPtr")]
    [InlineData("int", "int")]
    [InlineData("ACBindings.Vector3", "ACBindings.Vector3")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void WrapPointerForGeneric_WrapsPointerTypes(string input, string expected)
    {
        var result = PrimitiveTypeMappings.WrapPointerForGeneric(input);
        Assert.Equal(expected, result);
    }
}
