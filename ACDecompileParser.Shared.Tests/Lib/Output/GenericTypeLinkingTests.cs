using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class GenericTypeLinkingTests
{
    [Fact]
    public void Generate_LinksIntrusiveHashTable_WhenTypeReferenceIsMissing()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "HashSet",
            Type = TypeType.Struct,
            Source = "//..."
        };

        var mockRepo = new Mock<ITypeRepository>();

        // Mock IntrusiveHashTable type in DB as a Generic Type
        var intrusiveTableType = new TypeModel
        {
            Id = 999,
            BaseName = "IntrusiveHashTable",
            TemplateArguments = new List<TypeTemplateArgument> { new TypeTemplateArgument { TypeString = "T" } }
        };
        // The repository would return this if searching by FQN "IntrusiveHashTable<T>"
        mockRepo.Setup(r => r.GetTypeByFullyQualifiedName("IntrusiveHashTable<T>")).Returns(intrusiveTableType);

        // But the tokenizer searches for "IntrusiveHashTable" (no brackets)
        mockRepo.Setup(r => r.GetTypeByFullyQualifiedName("IntrusiveHashTable")).Returns((TypeModel)null!);

        // We expect the generator to fallback to searching by BaseName if FQN fails
        mockRepo.Setup(r => r.GetTypesForGroup("IntrusiveHashTable", It.IsAny<string>()))
            .Returns(new List<TypeModel> { intrusiveTableType });

        // Mock members
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "m_intrusiveTable",
                TypeString =
                    "IntrusiveHashTable<bool (__cdecl*)(HResultDebugData&),HashSetData<bool (__cdecl*)(HResultDebugData&)>*,1>",
                DeclarationOrder = 1,
                TypeReference = null // Simulate missing reference
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(1)).Returns(members);

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();

        // Assert
        var token = tokens.FirstOrDefault(t => t.Text == "IntrusiveHashTable");
        Assert.NotNull(token);
        Assert.Equal(TokenType.TypeName, token.Type);
        Assert.Equal("999", token.ReferenceId); // Should link to ID 999
    }
}
