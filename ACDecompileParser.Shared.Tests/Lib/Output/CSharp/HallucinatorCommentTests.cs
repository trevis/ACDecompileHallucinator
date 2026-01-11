using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Output;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class HallucinatorCommentTests
{
    private readonly Mock<ITypeRepository> _mockRepo;
    private readonly Mock<ITypeHierarchyService> _mockHierarchy;
    private readonly Mock<ICommentProvider> _mockCommentProvider;
    private readonly CSharpGroupProcessor _processor;

    public HallucinatorCommentTests()
    {
        _mockRepo = new Mock<ITypeRepository>();
        _mockHierarchy = new Mock<ITypeHierarchyService>();
        _mockCommentProvider = new Mock<ICommentProvider>();
        _processor = new CSharpGroupProcessor(_mockRepo.Object, null, _mockHierarchy.Object);
    }

    [Fact]
    public async Task PopulateCommentsAsync_AddsCommentsToEnum()
    {
        var type = new TypeModel { Id = 1, BaseName = "MyEnum", Type = TypeType.Enum, Namespace = "NS" };
        var types = new List<TypeModel> { type };
        _mockCommentProvider.Setup(p => p.GetEnumCommentAsync("NS::MyEnum"))
            .ReturnsAsync("/// <summary>Enum comment</summary>");

        await _processor.PopulateCommentsAsync(types, _mockCommentProvider.Object);

        Assert.Equal("/// <summary>Enum comment</summary>", type.XmlDocComment);
    }

    [Fact]
    public async Task PopulateCommentsAsync_AddsCommentsToStruct()
    {
        var type = new TypeModel { Id = 2, BaseName = "MyStruct", Type = TypeType.Struct, Namespace = "NS" };
        var types = new List<TypeModel> { type };
        _mockCommentProvider.Setup(p => p.GetStructCommentAsync("NS::MyStruct"))
            .ReturnsAsync("/// <summary>Struct comment</summary>");

        await _processor.PopulateCommentsAsync(types, _mockCommentProvider.Object);

        Assert.Equal("/// <summary>Struct comment</summary>", type.XmlDocComment);
    }

    [Fact]
    public async Task PopulateCommentsAsync_AddsCommentsToMethods()
    {
        var method = new FunctionBodyModel { Id = 3, FullyQualifiedName = "NS::MyStruct::MyMethod" };
        var type = new TypeModel
        {
            Id = 2,
            BaseName = "MyStruct",
            Namespace = "NS",
            Type = TypeType.Struct,
            FunctionBodies = new List<FunctionBodyModel> { method }
        };
        var types = new List<TypeModel> { type };
        _mockCommentProvider.Setup(p => p.GetMethodCommentAsync("NS::MyStruct::MyMethod"))
            .ReturnsAsync("/// <summary>Method comment</summary>");

        await _processor.PopulateCommentsAsync(types, _mockCommentProvider.Object);

        Assert.Equal("/// <summary>Method comment</summary>", method.XmlDocComment);
    }
}
