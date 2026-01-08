using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;
using ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;

namespace ACDecompileParser.Shared.Tests.Lib.Parsing;

public class ReproductionTest
{
    private readonly TestTypeRepository _repository;

    public ReproductionTest()
    {
        _repository = new TestTypeRepository();
    }

    [Fact]
    public void VerifyRenderMethodsParsed()
    {
        string input = @"
/* 1000 */
struct Render
{
  Render_vtbl *__vftable /*VFT*/;
  RenderConfig m_Config;
};

//----- (0050DF00) --------------------------------------------------------
void __cdecl Render::SetObjectScale(const Vector3 *scale)
{
  float y; // edx
  Render::object_scale = y;
}
";
        // SourceParser expects a list of file contents (strings), not lines.
        var parser = new SourceParser(new List<string> { input });

        parser.Parse();

        // Manually simulate SourceParser.SaveToDatabaseInternal behavior for Types and FunctionBodies

        // 1. Insert Types
        foreach (var type in parser.TypeModels)
        {
            _repository.InsertType(type);
        }

        // 2. Build Lookup
        var typeModelsByFqn = new Dictionary<string, TypeModel>();
        foreach (var type in parser.TypeModels)
        {
            var key = type.StoredFullyQualifiedName ?? type.FullyQualifiedName;
            if (!string.IsNullOrEmpty(key) && !typeModelsByFqn.ContainsKey(key))
                typeModelsByFqn[key] = type;
        }

        // 3. Link and Insert Function Bodies
        foreach (var func in parser.FunctionBodyModels)
        {
            var fqn = func.FullyQualifiedName;
            if (fqn.Contains("::"))
            {
                var lastScope = fqn.LastIndexOf("::");
                var parentName = fqn.Substring(0, lastScope);
                if (typeModelsByFqn.TryGetValue(parentName, out var parentType))
                {
                    func.ParentId = parentType.Id;
                }
            }

            _repository.InsertFunctionBody(func);
        }

        // 4. Verify Render type exists
        var renderType = _repository.GetTypeByFullyQualifiedName("Render");
        Assert.NotNull(renderType);

        // 5. Verify Function Body exists locally and in repo
        var methodBody = parser.FunctionBodyModels.FirstOrDefault(b => b.FullyQualifiedName.Contains("SetObjectScale"));
        Assert.NotNull(methodBody);
        Assert.Contains("Render::SetObjectScale", methodBody.FullyQualifiedName);

        // 6. Verify Function Body is linked to Render type
        Assert.True(methodBody.ParentId.HasValue,
            $"FunctionBody {methodBody.FullyQualifiedName} should have ParentId set. Parent lookup key was likely 'Render'.");
        Assert.Equal(renderType.Id, methodBody.ParentId);

        var linkedBodies = _repository.GetFunctionBodiesForType(renderType.Id);
        Assert.NotEmpty(linkedBodies);
        Assert.Contains(linkedBodies, b => b.Id == methodBody.Id);
    }
}
