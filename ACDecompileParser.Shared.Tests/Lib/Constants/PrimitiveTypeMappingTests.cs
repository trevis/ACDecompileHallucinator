using ACDecompileParser.Shared.Lib.Constants;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

public class PrimitiveTypeMappingTests
{
    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        $"{CSharpBindingsGenerator.NAMESPACE}.PrimitiveInplaceArray___ArchiveVersionRow_VersionEntry")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", $"{CSharpBindingsGenerator.NAMESPACE}.PrimitiveInplaceArray__int")]
    [InlineData("PrimitiveInplaceArray<SomeType*,20,8>",
        $"{CSharpBindingsGenerator.NAMESPACE}.PrimitiveInplaceArray___SomeType_ptr")]
    [InlineData("int", "int")]
    [InlineData("ArchiveVersionRow::VersionEntry",
        $"{CSharpBindingsGenerator.NAMESPACE}.ArchiveVersionRow.VersionEntry")]
    [InlineData("Vector3", $"{CSharpBindingsGenerator.NAMESPACE}.Vector3")]
    [InlineData("SmartArray<ACCharGenStartArea,1>",
        $"{CSharpBindingsGenerator.NAMESPACE}.SmartArray___ACCharGenStartArea")]
    [InlineData("HashTable<unsigned long,HeritageGroup_CG,0>",
        $"{CSharpBindingsGenerator.NAMESPACE}.HashTable__uint___HeritageGroup_CG")]
    [InlineData("Foo<Bar<int, 5>, 10>", $"{CSharpBindingsGenerator.NAMESPACE}.Foo___Bar__int")]
    [InlineData("void*", "System.IntPtr")]
    [InlineData("SmartArray<void*, 1>", $"{CSharpBindingsGenerator.NAMESPACE}.SmartArray__void_ptr")]
    [InlineData("PrimitiveInplaceArray<void*, 8, 1>",
        $"{CSharpBindingsGenerator.NAMESPACE}.PrimitiveInplaceArray__void_ptr")]
    [InlineData("HashTable<void*, int, 0>", $"{CSharpBindingsGenerator.NAMESPACE}.HashTable__void_ptr__int")]
    [InlineData("DArray<UnknownType*>", $"{CSharpBindingsGenerator.NAMESPACE}.DArray___UnknownType_ptr")]
    [InlineData("Type$With$Dollars", $"{CSharpBindingsGenerator.NAMESPACE}.Type_With_Dollars")]
    [InlineData("SmartArray<UIChildFramework*>",
        $"{CSharpBindingsGenerator.NAMESPACE}.SmartArray___UIChildFramework_ptr")]
    public void MapType_PrimitiveInplaceArray_RemovesLiteralArgs(string input, string expected)
    {
        var result = PrimitiveTypeMappings.MapType(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        $"{CSharpBindingsGenerator.NAMESPACE}.PrimitiveInplaceArray___ArchiveVersionRow_VersionEntry*")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", $"{CSharpBindingsGenerator.NAMESPACE}.PrimitiveInplaceArray__int*")]
    public void MapTypeForStaticPointer_PrimitiveInplaceArray_RemovesLiteralArgs(string input, string expected)
    {
        var result = PrimitiveTypeMappings.MapTypeForStaticPointer(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Type$With$Dollars", "Type_With_Dollars")]
    [InlineData("// local variable allocation has failed, the output may be wrong! bool", "bool")]
    [InlineData("SomeType // local variable allocation has failed, the output may be wrong!", "SomeType")]
    public void CleanTypeName_CleansSpecialCharactersAndArtifacts(string input, string expected)
    {
        var result = PrimitiveTypeMappings.CleanTypeName(input);
        Assert.Equal(expected, result);
    }
}
