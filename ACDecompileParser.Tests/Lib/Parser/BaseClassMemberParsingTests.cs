using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Tests.Lib.Parser;

public class BaseClassMemberParsingTests
{
    [Fact]
    public void ParseMembers_DetectsBaseclassMembers_AddsToBaseTypes()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "TestStruct",
            Namespace = ""
        };

        var source = @"
struct TestStruct
{
  CUnknown baseclass_0;
  IBaseFilter baseclass_c;
};";

        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd()).ToList();

        // Act
        StructParser.ParseMembers(structModel, lines, 1);

        // Assert
        Assert.Equal(2, structModel.BaseTypes.Count);
        Assert.Contains("CUnknown", structModel.BaseTypes);
        Assert.Contains("IBaseFilter", structModel.BaseTypes);
        Assert.Empty(structModel.Members);
    }

    [Fact]
    public void ParseMembers_MixedBaseclassAndRegularMembers()
    {
        // Arrange - Full CBaseFilter struct from user's example
        var structModel = new StructTypeModel
        {
            Name = "CBaseFilter",
            Namespace = ""
        };

        var source = @"
struct __declspec(align(8)) CBaseFilter
{
  CUnknown baseclass_0;
  IBaseFilter baseclass_c;
  IAMovieSetup baseclass_10;
  _FilterState m_State;
  IReferenceClock *m_pClock;
  CRefTime m_tStart;
  _GUID m_clsid;
  CCritSec *m_pLock;
  unsigned __int16 *m_pName;
  IFilterGraph *m_pGraph;
  IMediaEventSink *m_pSink;
  int m_PinVersion;
};";

        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd()).ToList();

        // Act
        StructParser.ParseMembers(structModel, lines, 1);

        // Assert - 3 base classes
        Assert.Equal(3, structModel.BaseTypes.Count);
        Assert.Contains("CUnknown", structModel.BaseTypes);
        Assert.Contains("IBaseFilter", structModel.BaseTypes);
        Assert.Contains("IAMovieSetup", structModel.BaseTypes);

        // Assert - 9 regular members (not baseclass_*)
        Assert.Equal(9, structModel.Members.Count);
        Assert.Contains(structModel.Members, m => m.Name == "m_State");
        Assert.Contains(structModel.Members, m => m.Name == "m_pClock");
        Assert.Contains(structModel.Members, m => m.Name == "m_tStart");
        Assert.Contains(structModel.Members, m => m.Name == "m_clsid");
        Assert.Contains(structModel.Members, m => m.Name == "m_pLock");
        Assert.Contains(structModel.Members, m => m.Name == "m_pName");
        Assert.Contains(structModel.Members, m => m.Name == "m_pGraph");
        Assert.Contains(structModel.Members, m => m.Name == "m_pSink");
        Assert.Contains(structModel.Members, m => m.Name == "m_PinVersion");
    }

    [Fact]
    public void ParseMembers_NoBaseclassMembers_WorksNormally()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "RegularStruct",
            Namespace = ""
        };

        var source = @"
struct RegularStruct
{
  int value1;
  char* name;
  double data;
};";

        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd()).ToList();

        // Act
        StructParser.ParseMembers(structModel, lines, 1);

        // Assert - No base types added from members
        Assert.Empty(structModel.BaseTypes);
        
        // Assert - All members parsed normally
        Assert.Equal(3, structModel.Members.Count);
        Assert.Contains(structModel.Members, m => m.Name == "value1");
        Assert.Contains(structModel.Members, m => m.Name == "name");
        Assert.Contains(structModel.Members, m => m.Name == "data");
    }

    [Fact]
    public void ParseMembers_SimilarButNonMatchingNames_NotTreatedAsBaseclass()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "TestStruct",
            Namespace = ""
        };

        var source = @"
struct TestStruct
{
  int baseclass;
  char baseclass_xyz;
  double mybaseclass_0;
  float baseclass_;
  int _baseclass_a;
};";

        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd()).ToList();

        // Act
        StructParser.ParseMembers(structModel, lines, 1);

        // Assert - None should be treated as base classes
        Assert.Empty(structModel.BaseTypes);
        
        // Assert - All should be treated as regular members
        Assert.Equal(5, structModel.Members.Count);
        Assert.Contains(structModel.Members, m => m.Name == "baseclass");
        Assert.Contains(structModel.Members, m => m.Name == "baseclass_xyz");
        Assert.Contains(structModel.Members, m => m.Name == "mybaseclass_0");
        Assert.Contains(structModel.Members, m => m.Name == "baseclass_");
        Assert.Contains(structModel.Members, m => m.Name == "_baseclass_a");
    }
}
