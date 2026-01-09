using ACDecompileParser.Shared.Lib.Constants;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants;

public class STLVectorTests
{
    [Theory]
    [InlineData("_STL::vector<ContractInfo,_STL::allocator<ContractInfo> >", "long")]
    [InlineData("_STL::vector<int>", "long")]
    [InlineData("struct _STL::vector<ContractInfo,_STL::allocator<ContractInfo> >", "long")]
    public void MapType_STLVector_ReturnsLong(string input, string expected)
    {
        var result = PrimitiveTypeMappings.MapType(input);
        Assert.Equal(expected, result);
    }
}
