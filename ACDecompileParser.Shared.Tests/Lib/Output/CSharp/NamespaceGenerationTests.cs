using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using Xunit;

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

    [Fact]
    public void GenerateWithNamespace_StructWithNestedType_UsesSingleFileScoped()
    {
        var parent = new TypeModel
        {
            Id = 1,
            BaseName = "AFrame",
            Namespace = "ACBindings",
            Type = TypeType.Struct
        };

        var nested = new TypeModel
        {
            Id = 2,
            BaseName = "FrameInitializationEnum",
            Namespace = "ACBindings::AFrame",
            Type = TypeType.Enum,
            ParentType = parent
        };

        parent.NestedTypes = new List<TypeModel> { nested };

        // Even if both are passed (common if flat list loaded), we should not see a namespace block for the nested type's namespace
        var types = new List<TypeModel> { parent, nested };

        var output = _generator.GenerateWithNamespace(types, "ACBindings");

        // Should use file-scoped namespace for the parent's namespace
        // Note: parent namespace "ACBindings" with prefix "ACBindings" -> "ACBindings.ACBindings" if regex replace works that way?
        // Wait, logic is: string finalNs = string.IsNullOrEmpty(subNs) ? namespaceName : $"{namespaceName}.{subNs}";
        // If Namespace="ACBindings" and we pass namespaceName="ACBindings", it prefixes it.
        // User example shows `namespace ACBindings`.
        // If Type.Namespace is "ACBindings", and we pass "ACBindings" as root, we get "ACBindings.ACBindings".
        // In the user example, maybe Type.Namespace is empty? Or root is empty?

        // Let's assume the user's types have Type.Namespace as "ACBindings" and they want "namespace ACBindings".
        // Or Type.Namespace is empty, and they want "namespace ACBindings".
        // In the user example: `namespace ACBindings { ... }`.

        // If I construct parent with Namespace="", and nested with Namespace="AFrame".

        // Let's stick to the parameters used in the test.
        // If I pass parent.Namespace="ACBindings", and subNs="ACBindings". final="ACBindings.ACBindings".

        // The user example shows:
        // namespace ACBindings { ... }
        // namespace ACBindings.AFrame { }

        // This implies the Nested type has namespace "ACBindings.AFrame".

        Assert.Contains("namespace ACBindings.ACBindings;", output); // Based on how code works now for single group
        Assert.DoesNotContain("namespace ACBindings.ACBindings.AFrame", output);
        Assert.Contains("public unsafe struct AFrame", output);
        // Nested type should be inside AFrame
        Assert.Contains("public enum FrameInitializationEnum", output);
    }
}
