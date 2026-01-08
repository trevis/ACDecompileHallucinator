using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class VTableDelegateTests
{
    [Fact]
    public void Generate_ReplacesVoidPointerWithUnmanagedDelegate()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 10,
            BaseName = "Render_vtbl",
            Type = TypeType.Struct,
            IsVTable = true,
            Namespace = "AC1Modern"
        };

        var mockRepo = new Mock<ITypeRepository>();

        // Mock Parent Class
        var parentType = new TypeModel
        {
            Id = 5,
            BaseName = "Render",
            Namespace = "AC1Modern"
        };
        mockRepo.Setup(r => r.GetTypesForGroup("Render", "AC1Modern")).Returns(new List<TypeModel> { parentType });

        // Mock Function Body for CreateRenderDevice
        var functionBodies = new List<FunctionBodyModel>
        {
            new FunctionBodyModel
            {
                Id = 101,
                FullyQualifiedName = "AC1Modern::Render::CreateRenderDevice",
                ParentId = 5,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "RenderDevice*",
                    Name = "CreateRenderDevice",
                    CallingConvention = "__thiscall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel { ParameterType = "Render*", Name = "this", Position = 0 }
                    }
                }
            }
        };
        mockRepo.Setup(r => r.GetFunctionBodiesForType(5)).Returns(functionBodies);

        // Mock VTable Member
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "CreateRenderDevice",
                TypeString = "void* (*)(Render* this)", // Current void* representation
                IsFunctionPointer = true,
                DeclarationOrder = 1
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(10)).Returns(members);

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();
        var code = string.Join("", tokens.Select(t => t.Text));

        // Assert
        Assert.Contains("delegate* unmanaged[Thiscall]<Render*, RenderDevice*>", code);
        Assert.DoesNotContain("void* (*)(Render* this)", code);
    }

    [Fact]
    public void Generate_RenamesDestructor()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 10,
            BaseName = "Render_vtbl",
            Type = TypeType.Struct,
            IsVTable = true,
            Namespace = "AC1Modern"
        };

        var mockRepo = new Mock<ITypeRepository>();

        // Mock Parent Class
        var parentType = new TypeModel { Id = 5, BaseName = "Render", Namespace = "AC1Modern" };
        mockRepo.Setup(r => r.GetTypesForGroup("Render", "AC1Modern")).Returns(new List<TypeModel> { parentType });

        // Mock Destructor Function Body
        var functionBodies = new List<FunctionBodyModel>
        {
            new FunctionBodyModel
            {
                Id = 100,
                FullyQualifiedName = "AC1Modern::Render::~Render",
                ParentId = 5,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "void",
                    Name = "~Render",
                    CallingConvention = "__thiscall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel { ParameterType = "Render*", Name = "this", Position = 0 },
                        new FunctionParamModel { ParameterType = "UInt32", Name = "flags", Position = 1 }
                    }
                }
            }
        };
        mockRepo.Setup(r => r.GetFunctionBodiesForType(5)).Returns(functionBodies);

        // Mock VTable Member
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "~Render",
                TypeString = "void* (*)(Render* this, UInt32 flags)",
                IsFunctionPointer = true,
                DeclarationOrder = 0
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(10)).Returns(members);

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();
        var code = string.Join("", tokens.Select(t => t.Text));

        // Assert
        // Should be renamed to _DestructorInternal
        Assert.Contains("_DestructorInternal", code);
        // Should use delegate* unmanaged
        Assert.Contains("delegate* unmanaged[Thiscall]<Render*, UInt32, void>", code);
    }

    [Fact]
    public void Generate_RenamesDtorString()
    {
        // Arrange
        var type = new TypeModel
        {
            Id = 11,
            BaseName = "NetError_vtbl",
            Type = TypeType.Struct,
            IsVTable = true,
            Namespace = "AC1Modern"
        };

        var mockRepo = new Mock<ITypeRepository>();

        // Mock Parent Class
        var parentType = new TypeModel { Id = 6, BaseName = "NetError", Namespace = "AC1Modern" };
        mockRepo.Setup(r => r.GetTypesForGroup("NetError", "AC1Modern")).Returns(new List<TypeModel> { parentType });

        // Mock Function Bodies for Parent Class (Matched via name convention ideally, but here we test the renaming regardless of match if possible, 
        // OR we match it to a destructor body if one exists.
        // The user requirement implies that IF the vtable member has _dtor_, it should be treated as a destructor.
        // Let's assume there is a matching body or at least we want the output name to change.

        // Actually, renaming happens in ReconstructMemberTokens which is called inside the loop.
        // If we match a function, we use the matched function's signature. 
        // If we DON'T match a function, we might still want to rename it if it looks like a function pointer?
        // But the current implementation only generates 'delegate* unmanaged' IF there is a match.
        // If there is NO match, it falls back to the old logic (which might just output the function pointer as is?).

        // Let's provide a match to ensure we hit the delegate path, and see if the name gets renamed.
        var functionBodies = new List<FunctionBodyModel>
        {
            new FunctionBodyModel
            {
                Id = 102,
                FullyQualifiedName = "AC1Modern::NetError::NetError_dtor_0",
                ParentId = 6,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "void",
                    Name = "NetError_dtor_0",
                    CallingConvention = "__thiscall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel { ParameterType = "NetError*", Name = "this", Position = 0 }
                    }
                }
            }
        };
        mockRepo.Setup(r => r.GetFunctionBodiesForType(6)).Returns(functionBodies);

        // Mock VTable Member
        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "NetError_dtor_0",
                TypeString = "void (__thiscall *NetError_dtor_0)(struct NetError *this)",
                IsFunctionPointer = true,
                DeclarationOrder = 0
            }
        };
        mockRepo.Setup(r => r.GetStructMembersWithRelatedTypes(11)).Returns(members);

        var generator = new StructOutputGenerator(mockRepo.Object);

        // Act
        var tokens = generator.Generate(type).ToList();
        var code = string.Join("", tokens.Select(t => t.Text));

        // Assert
        // Should be renamed to _DestructorInternal
        Assert.Contains("public static delegate* unmanaged[Thiscall]<NetError*, void> _DestructorInternal;", code);
    }
}
