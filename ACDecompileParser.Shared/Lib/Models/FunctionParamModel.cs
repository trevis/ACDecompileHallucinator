using System;

namespace ACDecompileParser.Shared.Lib.Models;

public class FunctionParamModel : BaseMemberModel
{
    public string ParameterType { get; set; } = string.Empty;
    public int Position { get; set; }

    // Parent relationship
    public int? ParentFunctionSignatureId { get; set; } // For params belonging to function signatures

    public int? TypeReferenceId { get; set; } // Points to the TypeReferences table, null if not using type reference

    // If this parameter is itself a function pointer type (e.g., HRESULT (__cdecl *)(const unsigned __int16 *))
    // This flag indicates the parameter type is a function pointer
    public bool IsFunctionPointerType { get; set; } = false;

    // For function pointer parameters, this references the FunctionSignatureModel that describes the function type
    public int? NestedFunctionSignatureId { get; set; }

    // Navigation property for the nested function signature (not stored in DB)
    public FunctionSignatureModel? NestedFunctionSignature { get; set; }

    // Navigation property for the type reference (not stored in DB)
    public TypeReference? TypeReference { get; set; }

    /// <summary>
    /// Pointer depth for function pointer types (e.g. 1 for *, 2 for **)
    /// </summary>
    public int PointerDepth { get; set; } = 1;
}
