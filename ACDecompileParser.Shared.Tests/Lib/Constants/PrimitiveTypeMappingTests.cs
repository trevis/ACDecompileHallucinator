using ACDecompileParser.Shared.Lib.Constants;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

public class PrimitiveTypeMappingTests
{
    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        "ACBindings.PrimitiveInplaceArray__ArchiveVersionRow_VersionEntry")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", "ACBindings.PrimitiveInplaceArray__int")]
    [InlineData("PrimitiveInplaceArray<SomeType*,20,8>", "ACBindings.PrimitiveInplaceArray__SomeType_ptr")]
    [InlineData("int", "int")]
    [InlineData("ArchiveVersionRow::VersionEntry", "ACBindings.ArchiveVersionRow.VersionEntry")]
    [InlineData("Vector3", "ACBindings.Vector3")]
    [InlineData("SmartArray<ACCharGenStartArea,1>", "ACBindings.SmartArray__ACCharGenStartArea")]
    [InlineData("HashTable<unsigned long,HeritageGroup_CG,0>",
        "ACBindings.HashTable__uint__HeritageGroup_CG")]
    [InlineData("Foo<Bar<int, 5>, 10>", "ACBindings.Foo__Bar__int")]
    [InlineData("void*", "System.IntPtr")]
    [InlineData("SmartArray<void*, 1>", "ACBindings.SmartArray__void_ptr")]
    [InlineData("PrimitiveInplaceArray<void*, 8, 1>", "ACBindings.PrimitiveInplaceArray__void_ptr")]
    [InlineData("HashTable<void*, int, 0>", "ACBindings.HashTable__void_ptr__int")]
    [InlineData("DArray<UnknownType*>", "ACBindings.DArray__UnknownType_ptr")]
    [InlineData("Type$With$Dollars", "ACBindings.Type_With_Dollars")]
    [InlineData("SmartArray<UIChildFramework*>", "ACBindings.SmartArray__UIChildFramework_ptr")]
    public void MapType_PrimitiveInplaceArray_RemovesLiteralArgs(string input, string expected)
    {
        var result = PrimitiveTypeMappings.MapType(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("PrimitiveInplaceArray<ArchiveVersionRow::VersionEntry,8,1>",
        "ACBindings.PrimitiveInplaceArray__ArchiveVersionRow_VersionEntry*")]
    [InlineData("PrimitiveInplaceArray<int,10,4>", "ACBindings.PrimitiveInplaceArray__int*")]
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
        // This method usage is removed from ProcessGenericType but still exists as a helper
        // We'll keep the test or remove it. Let's keep it to ensure logic didn't break even if unused.
        var result = PrimitiveTypeMappings.WrapPointerForGeneric(input);
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
