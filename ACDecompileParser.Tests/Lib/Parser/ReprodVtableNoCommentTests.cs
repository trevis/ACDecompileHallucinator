using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Tests.Lib.Parser;

public class ReprodVtableNoCommentTests
{
    [Fact]
    public void ParseVtableStruct_WithoutVFTComment_IsIdentifiedAsVTable()
    {
        // Arrange
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 3015 */",
                "struct __declspec(align(4)) RenderVertexBuffer_vtbl",
                "{",
                "  void (__thiscall *RenderVertexBuffer_dtor_0)(struct RenderVertexBuffer *this);",
                "  bool (__thiscall *Startup)(struct RenderVertexBuffer *this, const unsigned int, const unsigned int, const bool, const bool, const unsigned int);",
                "  void (__thiscall *Shutdown)(struct RenderVertexBuffer *this);",
                "  void *(__thiscall *Lock)(struct RenderVertexBuffer *this, const unsigned int, const unsigned int);",
                "  void (__thiscall *Unlock)(struct RenderVertexBuffer *this, const bool, const bool);",
                "  void *RenderIndexedPrimitives;",
                "  void *RenderPrimitives;",
                "  void *LockVirtualArray;",
                "  void *UnlockVirtualArray;",
                "  void *AddDirtyRange;",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Act
        parser.Parse();
        parser.SaveToDatabase(repo);

        // Assert
        var allTypes = repo.GetAllTypes();
        var vtableType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "RenderVertexBuffer_vtbl");

        Assert.NotNull(vtableType);
        Assert.True(vtableType.IsVTable, "RenderVertexBuffer_vtbl should be identified as IsVTable=true");
        Assert.Equal("RenderVertexBuffer_vtbl", vtableType.BaseName);

        // Fetch members explicitly as GetAllTypes does not include them
        var members = repo.GetStructMembers(vtableType.Id);
        Assert.Equal(10, members.Count);

        // Check destructor (function pointer)
        var dtor = members.FirstOrDefault(m => m.Name == "RenderVertexBuffer_dtor_0");
        Assert.NotNull(dtor);
        Assert.True(dtor.IsFunctionPointer);

        // Check non-function pointer member (void *)
        var renderPrimitives = members.FirstOrDefault(m => m.Name == "RenderPrimitives");
        Assert.NotNull(renderPrimitives);
        Assert.False(renderPrimitives.IsFunctionPointer);
        Assert.Equal("void*", renderPrimitives.TypeString);
    }
}
