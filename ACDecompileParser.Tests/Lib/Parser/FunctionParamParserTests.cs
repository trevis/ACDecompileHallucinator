using ACDecompileParser.Lib.Parser;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class FunctionParamParserTests
{
    #region Duplicate Parameter Name Handling Tests

    [Fact]
    public void ParseFunctionParameters_RenamesDuplicateNames()
    {
        // Arrange - simulating the IStorage_vtbl SetElementTimes example
        // "const _FILETIME *, const _FILETIME *, const _FILETIME *"
        var paramString = "const _FILETIME *, const _FILETIME *, const _FILETIME *";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);
        // The parser correctly identifies _FILETIME as part of the type, so these are unnamed parameters.
        // They get auto-named with __paramN prefixes.
        Assert.Equal("__param1", result[0].Name);
        Assert.Equal("__param2", result[1].Name);
        Assert.Equal("__param3", result[2].Name);
    }

    [Fact]
    public void ParseFunctionParameters_RenamesDuplicateNamedParameters()
    {
        // Arrange - parameters with same name
        var paramString = "int x, float x, double x";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("x_1", result[0].Name);
        Assert.Equal("x_2", result[1].Name);
        Assert.Equal("x_3", result[2].Name);
    }

    [Fact]
    public void ParseFunctionParameters_LeavesUniqueParametersUnchanged()
    {
        // Arrange
        var paramString = "int x, float y, double z";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("x", result[0].Name);
        Assert.Equal("y", result[1].Name);
        Assert.Equal("z", result[2].Name);
    }

    [Fact]
    public void ParseFunctionParameters_HandlesMixedDuplicateAndUniqueNames()
    {
        // Arrange - mix of duplicate and unique names
        var paramString = "int a, float b, double a";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("a_1", result[0].Name); // Renamed due to duplicate
        Assert.Equal("b", result[1].Name); // Unique, unchanged
        Assert.Equal("a_2", result[2].Name); // Renamed due to duplicate
    }

    [Fact]
    public void ParseFunctionParameters_HandlesThisPointerWithFileTimes()
    {
        // Arrange - the exact example from the user's input
        // int (__stdcall *SetElementTimes)(IStorage *this, const unsigned __int16 *, const _FILETIME *, const _FILETIME *, const _FILETIME *)
        var paramString =
            "IStorage *this, const unsigned __int16 *, const _FILETIME *, const _FILETIME *, const _FILETIME *";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal("this", result[0].Name);
        // "const unsigned __int16 *" is treated as compound type with no param name, gets auto-named
        Assert.Equal("__param2", result[1].Name);
        // The three _FILETIME params are also unnamed and get auto-named
        Assert.Equal("__param3", result[2].Name);
        Assert.Equal("__param4", result[3].Name);
        Assert.Equal("__param5", result[4].Name);
    }

    [Fact]
    public void ParseFunctionParameters_HandlesEmptyString()
    {
        // Arrange
        var paramString = "";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseFunctionParameters_HandlesSingleParameter()
    {
        // Arrange
        var paramString = "int x";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Single(result);
        Assert.Equal("x", result[0].Name);
    }

    [Fact]
    public void ParseFunctionParameters_TwoDuplicatesGetRenamedCorrectly()
    {
        // Arrange
        var paramString = "int value, float value";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("value_1", result[0].Name);
        Assert.Equal("value_2", result[1].Name);
    }

    [Fact]
    public void ParseFunctionParameters_HandlesUnnamedLowercaseTypes()
    {
        // Arrange - testing the specific cases reported by the user
        var paramString = "tagDVTARGETDEVICE *, tagLOGPALETTE **, tagSIZE *";

        // Act
        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        // Assert
        Assert.Equal(3, result.Count);

        // They should all be treated as unnamed parameters
        Assert.Equal("__param1", result[0].Name);
        Assert.Equal("__param2", result[1].Name);
        Assert.Equal("__param3", result[2].Name);

        // The types should be preserved correctly
        Assert.Equal("tagDVTARGETDEVICE*", result[0].ParameterType);
        Assert.Equal("tagLOGPALETTE**", result[1].ParameterType);
        Assert.Equal("tagSIZE*", result[2].ParameterType);

        // Verify pointer status in type reference
        Assert.True(result[0].TypeReference?.IsPointer);
        Assert.Equal(1, result[0].TypeReference?.PointerDepth);

        Assert.True(result[1].TypeReference?.IsPointer);
        Assert.Equal(2, result[1].TypeReference?.PointerDepth);
    }
    [Fact]
    public void ParseFunctionParameters_HandlesComplexFunctionPointerInTemplate()
    {
        // failing case from user
        var paramString = "IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIElement * (__cdecl *)(LayoutDesc const &,ElementDesc const &)> *,0> *this, unsigned int _numBuckets";

        var result = FunctionParamParser.ParseFunctionParameters(paramString);

        Assert.Equal(2, result.Count);
        Assert.Equal("this", result[0].Name);
        Assert.Equal("_numBuckets", result[1].Name);
        // Verify the type string is preserved correctly (FunctionParamParser normalizes spaces)
        Assert.Contains("UIElement*", result[0].ParameterType);
        Assert.Contains("LayoutDesc const&", result[0].ParameterType);
    }

    [Fact]
    public void ParseFunctionParameters_HandlesAnotherComplexCase()
    {
        var paramString = "IntrusiveHashTable<unsigned long,HashTableData<unsigned long,void (__cdecl *)(PropertyCollection const &)> *,0> *this, unsigned int _numBuckets";
         var result = FunctionParamParser.ParseFunctionParameters(paramString);
        
        Assert.Equal(2, result.Count);
        Assert.Equal("this", result[0].Name);
        Assert.Equal("_numBuckets", result[1].Name);
    }
    #endregion
}
