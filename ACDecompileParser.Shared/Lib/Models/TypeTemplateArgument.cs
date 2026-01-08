namespace ACDecompileParser.Shared.Lib.Models;

public class TypeTemplateArgument
{
    public int Id { get; set; }
    public int ParentTypeId { get; set; }
    public int Position { get; set; }
    public int? TypeReferenceId { get; set; } // Points to the TypeReferences table, null if not using type reference

    /// <summary>
    /// Raw type string before resolution
    /// </summary>
    public string TypeString { get; set; } = string.Empty;

    /// <summary>
    /// For template arguments that are function pointer types, this references the FunctionSignatureModel
    /// </summary>
    public int? FunctionSignatureId { get; set; }

    // Navigation property for the type reference (not stored in DB)
    public TypeReference? TypeReference { get; set; }

    // Navigation property for the function signature (not stored in DB)
    public FunctionSignatureModel? FunctionSignature { get; set; }
}
