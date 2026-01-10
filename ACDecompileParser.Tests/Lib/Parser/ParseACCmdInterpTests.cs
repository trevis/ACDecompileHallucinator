using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class ParseACCmdInterpTests
{
  [Fact]
  public void ParseACCmdInterp_ShouldHaveCorrectNameAndNamespace()
  {
    string source = @"/* 1711 */
struct __declspec(align(8)) ACCmdInterp
{
  CommandInterpreter baseclass_0;
  gmNoticeHandler baseclass_c8;
  HashTable<unsigned long,unsigned long,0> m_hashEmoteInputActionsToCommands;
};";

    var models = TypeParser.ParseStructs(source);

    Assert.Single(models);
    var model = models[0];

    Assert.Equal("ACCmdInterp", model.Name);
    Assert.Equal("", model.Namespace);
    Assert.Equal(1, model.Members.Count); // 1 normal member
    Assert.Equal(2, model.BaseTypes.Count); // 2 base classes parsed from members
  }
}
