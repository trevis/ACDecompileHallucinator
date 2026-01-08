using System;
using System.Collections.Generic;

namespace ACDecompileParser.Shared.Lib.Models;

/// <summary>
/// Represents a function signature that can be used for:
/// - Function pointer return types (methods that return function pointers)
/// - Function pointer parameters (parameters that are themselves function pointers)
/// This model is reusable and can be referenced by StructMemberModel or FunctionParamModel.
/// </summary>
public class FunctionSignatureModel : BaseMemberModel
{
    /// <summary>
    /// The return type of this function signature (e.g., "HRESULT", "void", "int")
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// The calling convention (e.g., "__cdecl", "__thiscall", "__stdcall")
    /// </summary>
    public string CallingConvention { get; set; } = string.Empty;

    /// <summary>
    /// The fully qualified name including return type, calling convention, name, and parameters.
    /// Normalized for consistent lookup.
    /// </summary>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the TypeReferences table for the return type
    /// </summary>
    public int? ReturnTypeReferenceId { get; set; }

    /// <summary>
    /// Navigation property for the return type reference (not stored in DB)
    /// </summary>
    public TypeReference? ReturnTypeReference { get; set; }

    /// <summary>
    /// For functions that return a function pointer, this references the FunctionSignatureModel of the returned function type
    /// </summary>
    public int? ReturnFunctionSignatureId { get; set; }

    /// <summary>
    /// Navigation property for the return function signature (not stored in DB)
    /// </summary>
    public FunctionSignatureModel? ReturnFunctionSignature { get; set; }

    /// <summary>
    /// The parameters of this function signature
    /// </summary>
    public List<FunctionParamModel> Parameters { get; set; } = new();
}
