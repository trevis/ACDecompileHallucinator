using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class StructOutputGeneratorTests
{
    [Fact]
    public void Generate_PopulatesReferenceIdForRelatedTypes()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "ComplexStruct",
            Type = TypeType.Struct,
            Source = "struct ComplexStruct : Base { Inner inner; };"
        };

        var mockRepo = new Mock<ITypeRepository>();

        // Mock Base Type
        var baseRelationship = new TypeInheritance
        {
            ParentTypeId = 1,
            RelatedTypeId = 100,
            RelatedType = new TypeModel { Id = 100, BaseName = "Base" }
        };
        mockRepo.Setup(r => r.GetBaseTypesWithRelatedTypes(1)).Returns(new List<TypeInheritance> { baseRelationship });

        // Mock members
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "inner",
                TypeString = "Inner",
                DeclarationOrder = 0,
                TypeReference = new TypeReference
                {
                    ReferencedTypeId = 200
                }
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(1)).Returns(members);

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();

        // Assert
        // Check base class reference
        var baseToken = tokens.FirstOrDefault(t => t.Text == "Base");
        Assert.NotNull(baseToken);
        Assert.Equal(TokenType.TypeName, baseToken.Type);
        Assert.Equal("100", baseToken.ReferenceId);

        // Check member type reference
        var memberTypeToken = tokens.FirstOrDefault(t => t.Text == "Inner");
        Assert.NotNull(memberTypeToken);
        Assert.Equal(TokenType.TypeName, memberTypeToken.Type);
        Assert.Equal("200", memberTypeToken.ReferenceId);
    }

    [Fact]
    public void Generate_MatchesFunctionBodiesToVTableMembers()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 10,
            BaseName = "UIFlow_vtbl",
            Type = TypeType.Struct,
            IsVTable = true,
            Namespace = "MyNamespace"
        };

        var mockRepo = new Mock<ITypeRepository>();

        // Mock Parent Class
        var parentType = new TypeModel
        {
            Id = 5,
            BaseName = "UIFlow",
            Namespace = "MyNamespace"
        };
        mockRepo.Setup(r => r.GetTypesForGroup("UIFlow", "MyNamespace")).Returns(new List<TypeModel> { parentType });

        // Mock Function Bodies for Parent Class
        var functionBodies = new List<FunctionBodyModel>
        {
            new FunctionBodyModel
            {
                Id = 101,
                FullyQualifiedName = "MyNamespace::UIFlow::Release",
                BodyText = "{ ... }",
                ParentId = 5,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "void",
                    Name = "Release",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel { ParameterType = "UIFlow*", Name = "this", Position = 0 }
                    }
                }
            },
            new FunctionBodyModel
            {
                Id = 102,
                FullyQualifiedName = "MyNamespace::UIFlow::UnmatchedMethod",
                BodyText = "{ ... }",
                ParentId = 5,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "void",
                    Name = "UnmatchedMethod",
                    Parameters = new List<FunctionParamModel>()
                }
            }
        };
        mockRepo.Setup(r => r.GetFunctionBodiesForType(5)).Returns(functionBodies);

        // Mock members for VTable
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "Release", // Should match
                TypeString = "void (*)(UIFlow* this)",
                IsFunctionPointer = true,
                DeclarationOrder = 0
            },
            new StructMemberModel
            {
                Name = "QueryInterface", // Should not match
                TypeString = "void* (*)(UIFlow* this, int id)",
                IsFunctionPointer = true,
                DeclarationOrder = 1
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(10)).Returns(members);

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();

        // Assert

        // Verify match comment for Release
        // Should contain Normalized Signature: void Release(UIFlow* this)
        var matchComment = tokens.FirstOrDefault(t => t.Text.Contains("// Matched: void Release(UIFlow* this)"));
        Assert.NotNull(matchComment);

        // Verify unmatched method comment for Release is NOT present
        var unmatchedRelComment = tokens.FirstOrDefault(t => t.Text.Contains("// Unmatched Method: Release"));
        Assert.Null(unmatchedRelComment);

        // Verify unmatched method comment for QueryInterface IS present
        var unmatchedQiComment = tokens.FirstOrDefault(t => t.Text.Contains("// Unmatched Method: QueryInterface"));
        Assert.NotNull(unmatchedQiComment);

        // Verify unmatched function body comment for UnmatchedMethod is NOT present (suppressed for VTable)
        var unmatchedBodyHeading = tokens.FirstOrDefault(t => t.Text.Contains("// Unmatched Function Bodies:"));
        Assert.Null(unmatchedBodyHeading);

        var unmatchedBodyComment =
            tokens.FirstOrDefault(t => t.Text.Contains("// MyNamespace::UIFlow::UnmatchedMethod"));
        Assert.Null(unmatchedBodyComment);

        // Verify Release body is NOT listed in unmatched bodies
        var matchedBodyInUnmatchedList = tokens.FirstOrDefault(t =>
            t.Text.Contains("// MyNamespace::UIFlow::Release") && !t.Text.Contains("Matched:"));
        Assert.Null(matchedBodyInUnmatchedList);
    }

    [Fact]
    public void Generate_ReportsUnmatchedFunctionBodiesForNonVTables()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 20,
            BaseName = "MyStruct",
            Namespace = "TestNamespace",
            Type = TypeType.Struct,
            IsVTable = false
        };

        var mockRepo = new Mock<ITypeRepository>();

        // Mock Function Bodies
        var functionBodies = new List<FunctionBodyModel>
        {
            new FunctionBodyModel
            {
                Id = 201,
                FullyQualifiedName = "TestNamespace::MyStruct::MatchedMethod",
                ParentId = 20,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "void",
                    Name = "MatchedMethod",
                    Parameters = new List<FunctionParamModel>()
                }
            },
            new FunctionBodyModel
            {
                Id = 202,
                FullyQualifiedName = "TestNamespace::MyStruct::UnmatchedExtra",
                ParentId = 20,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "int",
                    Name = "UnmatchedExtra",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel { ParameterType = "int", Name = "a", Position = 0 }
                    }
                }
            }
        };
        // Mock retrieving bodies for type itself
        type.FunctionBodies = functionBodies;
        mockRepo.Setup(r => r.GetFunctionBodiesForType(20)).Returns(functionBodies);

        // Mock members
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "MatchedMethod",
                IsFunctionPointer = true,
                DeclarationOrder = 0
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(20)).Returns(members);

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();

        // Assert
        // Verify matched comment
        // Normalized: void MatchedMethod()
        var matchComment =
            tokens.FirstOrDefault(t => t.Text.Contains("// Matched: void MatchedMethod()"));
        Assert.NotNull(matchComment);

        // Verify unmatched body heading IS present
        var unmatchedBodyHeading = tokens.FirstOrDefault(t => t.Text.Contains("// Unmatched Function Bodies:"));
        Assert.NotNull(unmatchedBodyHeading);

        // Verify UnmatchedExtra is listed
        // Normalized: int UnmatchedExtra(int a)
        var unmatchedBodyComment =
            tokens.FirstOrDefault(t => t.Text.Contains("// int UnmatchedExtra(int a)"));
        Assert.NotNull(unmatchedBodyComment);
    }
}
