using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class StructParserTests
{
    [Fact]
    public void ParseMembers_ShouldIgnoreBracesInComments()
    {
        // Arrange
        var structModel = new StructTypeModel { Name = "TestStruct" };
        var lines = new List<string>
        {
            "struct TestStruct",
            "{",
            "    int member1;",
            "    // This is a comment with an opening brace {",
            "    int member2;",
            "};",
            "struct NextStruct",
            "{",
            "    int member3;",
            "};"
        };
        
        // Act
        // Struct start index is 1 (the line with '{')
        StructParser.ParseMembers(structModel, lines, 1);
        
        // Assert
        // Should find member1 and member2.
        // If it counts the brace in the comment, it won't find the closing brace at line 5,
        // and might continue parsing until end of file or find '}' in NextStruct?
        // Actually, if it counts '{', braceCount goes up.
        // line 1: { -> count 1
        // line 3: // { -> count 2
        // line 5: }; -> count 1
        // line 7: { -> count 2
        // line 9: }; -> count 1
        // Loop finishes at end of lines. braceEnd is -1 or incorrect.
        
        // If ParseMembers logic is "find matching brace", and braceEnd isn't found correctly, 
        // it might return early or parse too much.
        
        Assert.Contains(structModel.Members, m => m.Name == "member1");
        Assert.Contains(structModel.Members, m => m.Name == "member2");
        
        // If it consumed too much, we might see member3 if we weren't careful?
        // But ParseMembers iterates from braceStart+1 to braceEnd.
        // If braceEnd is not found (because count never drops to 0), it returns early.
        // So members would be empty!
        
        // Wait, if braceEnd == -1, ParseMembers returns. 
        // So validation is: if the brace in comment breaks it, members will be missing.
        Assert.Equal(2, structModel.Members.Count);
    }
}
