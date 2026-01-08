using ACDecompileParser.Shared.Lib.Utilities;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Utilities;

public class ParsingUtilitiesTests
{
    [Fact]
    public void ExtractBaseTypeName_ExtractsTemplateArguments()
    {
        // Test extracting base name from template arguments
        string result = ParsingUtilities.ExtractBaseTypeName("AutoGrowHashTable<AsyncContext,AsyncCache::CCallbackHandler *>");
        Assert.Equal("AutoGrowHashTable", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_ExtractsNamespace()
    {
        // Test extracting base name from namespace-qualified type
        string result = ParsingUtilities.ExtractBaseTypeName("NS::SomeTemplate");
        Assert.Equal("SomeTemplate", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_ExtractsNamespaceFromTemplate()
    {
        // Test extracting base name from namespace-qualified template
        string result = ParsingUtilities.ExtractBaseTypeName("NS::SomeTemplate<int, float>");
        Assert.Equal("SomeTemplate", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_HandlesSimpleName()
    {
        // Test with simple name (no changes expected)
        string result = ParsingUtilities.ExtractBaseTypeName("SimpleClass");
        Assert.Equal("SimpleClass", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesConstModifier()
    {
        // Test removing const modifier
        string result = ParsingUtilities.ExtractBaseTypeName("const SomeType");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesPointerModifier()
    {
        // Test removing pointer modifier
        string result = ParsingUtilities.ExtractBaseTypeName("SomeType*");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesReferenceModifier()
    {
        // Test removing reference modifier
        string result = ParsingUtilities.ExtractBaseTypeName("SomeType&");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesMultiplePointers()
    {
        // Test removing multiple pointer modifiers
        string result = ParsingUtilities.ExtractBaseTypeName("SomeType**");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesPointerAndTemplate()
    {
        // Test removing pointer and extracting template base name
        string result = ParsingUtilities.ExtractBaseTypeName("SomeType<int>*");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesConstAndTemplate()
    {
        // Test removing const and extracting template base name
        string result = ParsingUtilities.ExtractBaseTypeName("const SomeType<int>");
        Assert.Equal("SomeType", result);
    }
    
    [Fact]
    public void ExtractBaseTypeName_RemovesConstNamespaceAndTemplate()
    {
        // Test removing const, namespace and extracting template base name
        string result = ParsingUtilities.ExtractBaseTypeName("const NS::SomeType<int>");
        Assert.Equal("SomeType", result);
    }

    #region ExtractArrayInfo Tests

    [Fact]
    public void ExtractArrayInfo_ReturnsFalse_ForNonArrayDeclaration()
    {
        // Test with non-array declaration
        var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo("int x");
        Assert.False(isArray);
        Assert.Null(arraySize);
    }

    [Fact]
    public void ExtractArrayInfo_ReturnsTrueWithSize_ForSizedArray()
    {
        // Test with sized array like "char name[32]"
        var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo("char name[32]");
        Assert.True(isArray);
        Assert.Equal(32, arraySize);
    }

    [Fact]
    public void ExtractArrayInfo_ReturnsTrueWithNull_ForUnsizedArray()
    {
        // Test with unsized/flexible array like "char Format[]"
        var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo("char Format[]");
        Assert.True(isArray);
        Assert.Null(arraySize);
    }

    [Fact]
    public void ExtractArrayInfo_HandlesVoidPointerArray()
    {
        // Test with void pointer array "void *pad[2]"
        var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo("void *pad[2]");
        Assert.True(isArray);
        Assert.Equal(2, arraySize);
    }

    [Fact]
    public void ExtractArrayInfo_HandlesLargeArraySize()
    {
        // Test with large array size "char __ss_pad2[112]"
        var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo("char __ss_pad2[112]");
        Assert.True(isArray);
        Assert.Equal(112, arraySize);
    }

    [Fact]
    public void ExtractArrayInfo_HandlesByteArray()
    {
        // Test with _BYTE array "_BYTE gap4[4]"
        var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo("_BYTE gap4[4]");
        Assert.True(isArray);
        Assert.Equal(4, arraySize);
    }

    [Fact]
    public void ExtractArrayInfo_HandlesUnsignedInt16Array()
    {
        // Test with unsigned __int16 array "unsigned __int16 lfFaceName[32]"
        var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo("unsigned __int16 lfFaceName[32]");
        Assert.True(isArray);
        Assert.Equal(32, arraySize);
    }

    [Fact]
    public void ExtractArrayInfo_ReturnsFalse_ForNullOrEmpty()
    {
        // Test with null - using nullable reference type syntax to avoid warning
        var (isArrayNull, arraySizeNull) = ParsingUtilities.ExtractArrayInfo(null!);
        Assert.False(isArrayNull);
        Assert.Null(arraySizeNull);

        // Test with empty string
        var (isArrayEmpty, arraySizeEmpty) = ParsingUtilities.ExtractArrayInfo("");
        Assert.False(isArrayEmpty);
        Assert.Null(arraySizeEmpty);

        // Test with whitespace
        var (isArrayWhitespace, arraySizeWhitespace) = ParsingUtilities.ExtractArrayInfo("   ");
        Assert.False(isArrayWhitespace);
        Assert.Null(arraySizeWhitespace);
    }

    [Fact]
    public void ExtractArrayInfo_StripsComments()
    {
        // Test with offset comment "/* 0x08 */ char name[16]"
        var (isArray, arraySize) = ParsingUtilities.ExtractArrayInfo("/* 0x08 */ char name[16]");
        Assert.True(isArray);
        Assert.Equal(16, arraySize);
    }

    #endregion

    #region ExtractBitFieldInfo Tests

    [Fact]
    public void ExtractBitFieldInfo_ReturnsFalse_ForNonBitFieldDeclaration()
    {
        // Test with regular member declaration
        var (isBitField, width) = ParsingUtilities.ExtractBitFieldInfo("int x;");
        Assert.False(isBitField);
        Assert.Null(width);
    }

    [Fact]
    public void ExtractBitFieldInfo_ReturnsTrueWithWidth_ForSingleBitField()
    {
        // Test with single bit field "unsigned int flag : 1;"
        var (isBitField, width) = ParsingUtilities.ExtractBitFieldInfo("unsigned int flag : 1;");
        Assert.True(isBitField);
        Assert.Equal(1, width);
    }

    [Fact]
    public void ExtractBitFieldInfo_ReturnsTrueWithWidth_ForMultiBitField()
    {
        // Test with multi-bit field "unsigned int Reserved : 30;"
        var (isBitField, width) = ParsingUtilities.ExtractBitFieldInfo("unsigned int Reserved : 30;");
        Assert.True(isBitField);
        Assert.Equal(30, width);
    }

    [Fact]
    public void ExtractBitFieldInfo_HandlesUnsignedInt32()
    {
        // Test with __int32 type "unsigned __int32 AllowDemotion : 1;"
        var (isBitField, width) = ParsingUtilities.ExtractBitFieldInfo("unsigned __int32 AllowDemotion : 1;");
        Assert.True(isBitField);
        Assert.Equal(1, width);
    }

    [Fact]
    public void ExtractBitFieldInfo_HandlesNoSpaceAroundColon()
    {
        // Test with no spaces around colon "int x:4;"
        var (isBitField, width) = ParsingUtilities.ExtractBitFieldInfo("int x:4;");
        Assert.True(isBitField);
        Assert.Equal(4, width);
    }

    [Fact]
    public void ExtractBitFieldInfo_HandlesExtraSpaces()
    {
        // Test with extra spaces "int x  :  8  ;"
        var (isBitField, width) = ParsingUtilities.ExtractBitFieldInfo("int x  :  8  ;");
        Assert.True(isBitField);
        Assert.Equal(8, width);
    }

    [Fact]
    public void ExtractBitFieldInfo_ReturnsFalse_ForNullOrEmpty()
    {
        // Test with null
        var (isNull, widthNull) = ParsingUtilities.ExtractBitFieldInfo(null!);
        Assert.False(isNull);
        Assert.Null(widthNull);

        // Test with empty string
        var (isEmpty, widthEmpty) = ParsingUtilities.ExtractBitFieldInfo("");
        Assert.False(isEmpty);
        Assert.Null(widthEmpty);

        // Test with whitespace
        var (isWhitespace, widthWhitespace) = ParsingUtilities.ExtractBitFieldInfo("   ");
        Assert.False(isWhitespace);
        Assert.Null(widthWhitespace);
    }

    [Fact]
    public void ExtractBitFieldInfo_IgnoresNamespaceSeparator()
    {
        // Test that namespace separator is NOT treated as bit field syntax
        var (isBitField, width) = ParsingUtilities.ExtractBitFieldInfo("NS::TypeName name;");
        Assert.False(isBitField);
        Assert.Null(width);
    }

    #endregion

    #region RemoveBitFieldSyntax Tests

    [Fact]
    public void RemoveBitFieldSyntax_RemovesBitFieldWidth()
    {
        // Test removing bit field width
        string result = ParsingUtilities.RemoveBitFieldSyntax("unsigned int flag : 1;");
        Assert.Equal("unsigned int flag;", result);
    }

    [Fact]
    public void RemoveBitFieldSyntax_HandlesMultiBitField()
    {
        // Test removing multi-bit field width
        string result = ParsingUtilities.RemoveBitFieldSyntax("unsigned __int32 Reserved : 30;");
        Assert.Equal("unsigned __int32 Reserved;", result);
    }

    [Fact]
    public void RemoveBitFieldSyntax_LeavesNonBitFieldUnchanged()
    {
        // Test with non-bit field declaration
        string result = ParsingUtilities.RemoveBitFieldSyntax("int x;");
        Assert.Equal("int x;", result);
    }

    [Fact]
    public void RemoveBitFieldSyntax_HandlesNullOrEmpty()
    {
        // Test with null
        string resultNull = ParsingUtilities.RemoveBitFieldSyntax(null!);
        Assert.Null(resultNull);

        // Test with empty string
        string resultEmpty = ParsingUtilities.RemoveBitFieldSyntax("");
        Assert.Equal("", resultEmpty);
    }

    #endregion
}
