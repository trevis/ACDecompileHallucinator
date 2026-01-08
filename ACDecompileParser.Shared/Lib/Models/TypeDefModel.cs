namespace ACDecompileParser.Shared.Lib.Models;

/// <summary>
/// Represents a typedef declaration, linking a typedef name to its underlying type
/// </summary>
public class TypeDefModel
{
    public int Id { get; set; }

    /// <summary>
    /// The name of the typedef (e.g., "FARPROC", "flowqueueInterval_t")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The namespace of the typedef (e.g., "MyNamespace" or empty for global)
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// The fully qualified name of the typedef (Namespace::Name or just Name if no namespace)
    /// </summary>
    public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}::{Name}";

    /// <summary>
    /// Foreign key to the TypeReference that this typedef resolves to
    /// </summary>
    public int TypeReferenceId { get; set; }

    /// <summary>
    /// Navigation property to the underlying type reference
    /// </summary>
    public TypeReference? TypeReference { get; set; }

    /// <summary>
    /// For function pointer typedefs, reference to the function signature
    /// (e.g., "typedef int (*FARPROC)()")
    /// </summary>
    public int? FunctionSignatureId { get; set; }

    /// <summary>
    /// Navigation property to the function signature (if this is a function pointer typedef)
    /// </summary>
    public FunctionSignatureModel? FunctionSignature { get; set; }

    /// <summary>
    /// The raw source code of the typedef declaration
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// The source file of the typedef definition
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// The line number of the typedef definition in the source file
    /// </summary>
    public int? LineNumber { get; set; }
}
