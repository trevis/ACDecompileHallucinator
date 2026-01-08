using System;

namespace ACDecompileParser.Shared.Lib.Models;

public class TypeReference
{
    public int Id { get; set; }

    /// <summary>
    /// The raw type string before resolution (e.g., "const SmartArray&lt;T&gt;*")
    /// </summary>
    public string TypeString { get; set; } = string.Empty;

    /// <summary>
    /// The fully qualified type name without modifiers (const, *, &, etc.) used for matching against Types table
    /// </summary>
    public string FullyQualifiedType { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the resolved type in the Types table (null if not resolved)
    /// </summary>
    public int? ReferencedTypeId { get; set; }

    /// <summary>
    /// Indicates if the type is const
    /// </summary>
    public bool IsConst { get; set; }

    /// <summary>
    /// Indicates if the type is a pointer
    /// </summary>
    public bool IsPointer { get; set; }

    /// <summary>
    /// Indicates if the type is a reference
    /// </summary>
    public bool IsReference { get; set; }

    /// <summary>
    /// The depth of pointer indirection (e.g., 1 for *, 2 for **)
    /// </summary>
    public int PointerDepth { get; set; }

    /// <summary>
    /// Indicates if the type is an array
    /// </summary>
    public bool IsArray { get; set; }

    /// <summary>
    /// The size of the array. Null indicates an unsized/flexible array (e.g., char Format[])
    /// </summary>
    public int? ArraySize { get; set; }

    /// <summary>
    /// Navigation property for the referenced type (not stored in DB)
    /// </summary>
    public TypeModel? ReferencedType { get; set; }

    /// <summary>
    /// The source file of the reference
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// The line number of the reference in the source file
    /// </summary>
    public int? LineNumber { get; set; }
}
