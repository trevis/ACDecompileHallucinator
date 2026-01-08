using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using System.Collections.Generic;
using Xunit;
using System.Linq;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class NamespaceGenerationTests
{
    private readonly CSharpBindingsGenerator _generator;

    public NamespaceGenerationTests()
    {
        _generator = new CSharpBindingsGenerator();
    }

    [Fact]
    public void GenerateWithNamespace_SingleNamespace_UsesFileScopedNamespace()
    {
        var types = new List<TypeModel>
        {
            new TypeModel
            {
                BaseName = "Vector3",
                Namespace = "AC1Legacy",
                Type = TypeType.Struct
            }
        };

        var output = _generator.GenerateWithNamespace(types, "ACBindings");

        Assert.Contains("namespace ACBindings.AC1Legacy;", output);
        Assert.Contains("public unsafe struct Vector3", output);
    }

    [Fact]
    public void GenerateWithNamespace_NoNamespace_UsesRootNamespace()
    {
        var types = new List<TypeModel>
        {
            new TypeModel
            {
                BaseName = "GlobalStruct",
                Namespace = "",
                Type = TypeType.Struct
            }
        };

        var output = _generator.GenerateWithNamespace(types, "ACBindings");

        Assert.Contains("namespace ACBindings;", output);
        Assert.Contains("public unsafe struct GlobalStruct", output);
    }

    [Fact]
    public void GenerateWithNamespace_NestedNamespace_ReplacesSeparators()
    {
        var types = new List<TypeModel>
        {
            new TypeModel
            {
                BaseName = "InnerStruct",
                Namespace = "Outer::Inner",
                Type = TypeType.Struct
            }
        };

        var output = _generator.GenerateWithNamespace(types, "ACBindings");

        Assert.Contains("namespace ACBindings.Outer.Inner;", output);
        Assert.Contains("public unsafe struct InnerStruct", output);
    }

    [Fact]
    public void GenerateWithNamespace_MultipleTypes_SameNamespace_UsesSingleFileScoped()
    {
        var types = new List<TypeModel>
        {
            new TypeModel { BaseName = "TypeA", Namespace = "Common", Type = TypeType.Struct },
            new TypeModel { BaseName = "TypeB", Namespace = "Common", Type = TypeType.Struct }
        };

        var output = _generator.GenerateWithNamespace(types, "ACBindings");

        Assert.Contains("namespace ACBindings.Common;", output);
        Assert.Contains("public unsafe struct TypeA", output);
        Assert.Contains("public unsafe struct TypeB", output);

        // Count occurrences of "namespace"
        int count = output.Split("namespace").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public void GenerateWithNamespace_MixedNamespaces_UsesBlockScoped()
    {
        var types = new List<TypeModel>
        {
            new TypeModel { BaseName = "TypeA", Namespace = "NamespaceA", Type = TypeType.Struct },
            new TypeModel { BaseName = "TypeB", Namespace = "NamespaceB", Type = TypeType.Struct }
        };

        var output = _generator.GenerateWithNamespace(types, "ACBindings");

        // Expectations:
        Assert.Contains("namespace ACBindings.NamespaceA", output);
        Assert.Contains("namespace ACBindings.NamespaceB", output);
        Assert.Contains("public unsafe struct TypeA", output);
        Assert.Contains("public unsafe struct TypeB", output);
    }
}
