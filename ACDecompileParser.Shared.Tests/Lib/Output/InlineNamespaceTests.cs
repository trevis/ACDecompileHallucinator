using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class InlineNamespaceTests
{
    [Fact]
    public void StructOutputGenerator_InlinesNamespaceInStructDeclaration()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "AFrame",
            Namespace = "AC1Modern",
            Type = TypeType.Struct,
            Source = "struct __cppobj AC1Modern::AFrame { Vector3 m_vOrigin; Quaternion m_qOrientation; };"
        };

        var mockRepo = new Mock<ITypeRepository>();
        var members = new List<StructMemberModel>
        {
            new()
            {
                Name = "m_vOrigin",
                TypeString = "Vector3",
                DeclarationOrder = 0,
                Offset = 0x00
            },
            new()
            {
                Name = "m_qOrientation",
                TypeString = "Quaternion",
                DeclarationOrder = 1,
                Offset = 0x0C
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(1)).Returns(members);
        mockRepo.Setup(r => r.GetBaseTypesWithRelatedTypes(1)).Returns(new List<TypeInheritance>());

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();

        // Assert - verify namespace is inlined
        var structKeywordIndex = tokens.FindIndex(t => t.Text == "struct" && t.Type == TokenType.Keyword);
        Assert.True(structKeywordIndex >= 0, "Should contain 'struct' keyword");

        // The next non-whitespace token should be the namespace
        var namespaceTokenIndex = structKeywordIndex + 1;
        while (namespaceTokenIndex < tokens.Count && tokens[namespaceTokenIndex].Type == TokenType.Whitespace)
        {
            namespaceTokenIndex++;
        }

        Assert.True(namespaceTokenIndex < tokens.Count);
        Assert.Equal("AC1Modern::", tokens[namespaceTokenIndex].Text);
        Assert.Equal(TokenType.Identifier, tokens[namespaceTokenIndex].Type);

        // Next should be the type name
        var typeNameTokenIndex = namespaceTokenIndex + 1;
        Assert.Equal("AFrame", tokens[typeNameTokenIndex].Text);
        Assert.Equal(TokenType.TypeName, tokens[typeNameTokenIndex].Type);
        Assert.Equal("1", tokens[typeNameTokenIndex].ReferenceId);
    }

    [Fact]
    public void StructOutputGenerator_WorksWithoutNamespace()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "SimpleStruct",
            Namespace = string.Empty,
            Type = TypeType.Struct,
            Source = "struct SimpleStruct { int x; };"
        };

        var mockRepo = new Mock<ITypeRepository>();
        var members = new List<StructMemberModel>
        {
            new()
            {
                Name = "x",
                TypeString = "int",
                DeclarationOrder = 0
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(1)).Returns(members);
        mockRepo.Setup(r => r.GetBaseTypesWithRelatedTypes(1)).Returns(new List<TypeInheritance>());

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();

        // Assert - verify no namespace prefix
        var structKeywordIndex = tokens.FindIndex(t => t.Text == "struct" && t.Type == TokenType.Keyword);
        Assert.True(structKeywordIndex >= 0);

        // Find next non-whitespace token
        var nextTokenIndex = structKeywordIndex + 1;
        while (nextTokenIndex < tokens.Count && tokens[nextTokenIndex].Type == TokenType.Whitespace)
        {
            nextTokenIndex++;
        }

        // Should directly be the type name, not namespace
        Assert.Equal("SimpleStruct", tokens[nextTokenIndex].Text);
        Assert.Equal(TokenType.TypeName, tokens[nextTokenIndex].Type);
    }

    [Fact]
    public void TypeGroupProcessor_DoesNotWrapInNamespaceBlock()
    {
        // Arrange
        var types = new List<TypeModel>
        {
            new()
            {
                Id = 1,
                BaseName = "TestStruct",
                Namespace = "TestNamespace",
                Type = TypeType.Struct,
                Source = "struct TestStruct { int x; };"
            }
        };

        var mockRepo = new Mock<ITypeRepository>();
        mockRepo.Setup(r => r.GetStructMembersForMultipleTypes(It.IsAny<List<int>>()))
            .Returns(new Dictionary<int, List<StructMemberModel>>
            {
                {
                    1, new List<StructMemberModel>
                    {
                        new()
                        {
                            Name = "x",
                            TypeString = "int",
                            DeclarationOrder = 0
                        }
                    }
                }
            });
        mockRepo.Setup(r => r.GetBaseTypesForMultipleTypes(It.IsAny<List<int>>()))
            .Returns(new Dictionary<int, List<TypeInheritance>>());
        mockRepo.Setup(r => r.GetFunctionBodiesForMultipleTypes(It.IsAny<List<int>>()))
            .Returns(new Dictionary<int, List<FunctionBodyModel>>());
        mockRepo.Setup(r => r.GetBaseTypesWithRelatedTypes(1))
            .Returns(new List<TypeInheritance>());
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(1))
            .Returns(new List<StructMemberModel>
            {
                new()
                {
                    Name = "x",
                    TypeString = "int",
                    DeclarationOrder = 0
                }
            });
        mockRepo.Setup(r => r.GetStaticVariablesForType(It.IsAny<int>()))
            .Returns(new List<StaticVariableModel>());
        mockRepo.Setup(r => r.GetStaticVariablesForMultipleTypes(It.IsAny<List<int>>()))
            .Returns(new Dictionary<int, List<StaticVariableModel>>());

        var processor = new TypeGroupProcessor(mockRepo.Object);

        // Act
        var content = processor.GenerateGroupContent(types, includeHeaderAndNamespace: true);

        // Assert - should NOT contain namespace block
        Assert.DoesNotContain("namespace TestNamespace", content);
        Assert.DoesNotContain("// namespace TestNamespace", content);

        // Should contain inline namespace
        Assert.Contains("struct TestNamespace::TestStruct", content);
    }
}
