using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class StructOutputGeneratorArrayTests
{
    [Fact]
    public void Generate_OutputsArraySyntaxCorrectly()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "IntrusiveHashTable",
            Type = TypeType.Struct,
            Source = "struct IntrusiveHashTable { HashSetData<UIElement *> *m_aInplaceBuckets[23]; };"
        };
        
        var mockRepo = new Mock<ITypeRepository>();
        // Mock members with array type reference
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "m_aInplaceBuckets",
                TypeString = "HashSetData<UIElement*>*",
                DeclarationOrder = 0,
                TypeReference = new TypeReference
                {
                    TypeString = "HashSetData<UIElement*>*",
                    IsArray = true,
                    ArraySize = 23,
                    IsPointer = true,
                    PointerDepth = 1
                }
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(1)).Returns(members);
        
        var generator = new StructOutputGenerator(mockRepo.Object);
        
        // Act
        var tokens = generator.Generate(type).ToList();
        
        // Assert - verify array bracket tokens are generated
        Assert.Contains(tokens, t => t.Text == "[" && t.Type == TokenType.Punctuation);
        Assert.Contains(tokens, t => t.Text == "23" && t.Type == TokenType.NumberLiteral);
        Assert.Contains(tokens, t => t.Text == "]" && t.Type == TokenType.Punctuation);
        Assert.Contains(tokens, t => t.Text == "m_aInplaceBuckets" && t.Type == TokenType.Identifier);
    }
}
