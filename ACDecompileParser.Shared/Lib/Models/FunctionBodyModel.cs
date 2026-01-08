using System.ComponentModel.DataAnnotations.Schema;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Lib.Models;

/// <summary>
/// Represents the parsed body of a function from the decompiled source.
/// </summary>
public class FunctionBodyModel : BaseMemberModel
{
    /// <summary>
    /// The comprehensive name of the function, including class scope if applicable.
    /// </summary>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// The raw text of the function body.
    /// </summary>
    public string BodyText { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the FunctionSignatureModel.
    /// </summary>
    public int? FunctionSignatureId { get; set; }

    /// <summary>
    /// Navigation property for the function signature.
    /// </summary>
    public FunctionSignatureModel? FunctionSignature { get; set; }

    /// <summary>
    /// Foreign key to the parent TypeModel. Null for global functions.
    /// </summary>
    public int? ParentId { get; set; }
    /// <summary>
    /// The memory address/offset of the function from the source file header.
    /// </summary>
    public long? Offset { get; set; }
    // Note: ParentType navigation property is inherited from BaseMemberModel
}
