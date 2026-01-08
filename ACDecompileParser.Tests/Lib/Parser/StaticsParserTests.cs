using Xunit;
using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Parser;

public class StaticsParserTests
{
    [Fact]
    public void ParseLine_ValidLine_ReturnsModel()
    {
        string line = "0000000140BA96E0 public: static int CBaseObject::m_cObjects 4 bytes [statics]";
        var result = StaticsParser.ParseLine(line);

        Assert.NotNull(result);
        Assert.Equal("0000000140BA96E0", result.Address);
        Assert.Equal("CBaseObject::m_cObjects", result.Name);
        Assert.Equal("int", result.TypeString);
    }

    [Fact]
    public void ParseLine_VftableLine_ReturnsNull()
    {
        string line = "0000000140BA96E0 public: static const ACScene::`vftable' ACScene::vftable 8 bytes [statics]";
        var result = StaticsParser.ParseLine(line);

        Assert.Null(result);
    }

    [Fact]
    public void ParseLine_ComplexType_ParsesCorrectly()
    {
        string line = "0000000140BA96E0 public: static std::vector<int> ACRender::m_vec 24 bytes [statics]";
        var result = StaticsParser.ParseLine(line);

        Assert.NotNull(result);
        Assert.Equal("std::vector<int>", result.TypeString);
        Assert.Equal("ACRender::m_vec", result.Name);
    }

    [Fact]
    public void ParseLine_PointerType_ParsesCorrectly()
    {
        string line = "0000000140BA96E0 public: static char * GlobalName 8 bytes [statics]";
        var result = StaticsParser.ParseLine(line);

        Assert.NotNull(result);
        Assert.Equal("char*", result.TypeString);
        Assert.Equal("GlobalName", result.Name);
    }
}
