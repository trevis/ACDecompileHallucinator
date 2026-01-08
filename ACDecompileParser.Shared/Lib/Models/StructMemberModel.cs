using System;
using System.Collections.Generic;

namespace ACDecompileParser.Shared.Lib.Models;

public class StructMemberModel : BaseMemberModel
{
    public string TypeString { get; set; } = string.Empty;
    public int? Offset { get; set; }
    public int StructTypeId { get; set; }
    public int? TypeReferenceId { get; set; } // Points to the TypeReferences table, null if not using type reference
    public int DeclarationOrder { get; set; } = 0; // Preserves the original declaration order in the source code

    // Bit field properties (e.g., "unsigned int x : 4;")
    public int? BitFieldWidth { get; set; } // Width of the bit field, null if not a bit field

    // Alignment property (e.g., "__declspec(align(8))")
    public int? Alignment { get; set; } // Alignment in bytes, null if not specified

    // Overload support for vtables with multiple functions of the same name
    // When multiple functions share the same name (e.g., InitForPacking overloads),
    // this index differentiates them (0 for first, 1 for second, etc.)
    public int OverloadIndex { get; set; } = 0;

    // Function pointer properties
    public bool IsFunctionPointer { get; set; }

    // For function pointer members, this references the FunctionSignatureModel that describes the function type
    public int? FunctionSignatureId { get; set; }

    // Navigation property for the function signature (not stored in DB)
    public FunctionSignatureModel? FunctionSignature { get; set; }

    // Navigation property for the type reference (not stored in DB)
    public TypeReference? TypeReference { get; set; }

}
