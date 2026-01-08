using System;

namespace ACDecompileParser.Shared.Lib.Models;

/// <summary>
/// Represents a detailed unresolved type reference with information about where it occurs
/// </summary>
public class DetailedUnresolvedReference
{
    /// <summary>
    /// The unresolved type string
    /// </summary>
    public string TypeString { get; set; } = string.Empty;

    /// <summary>
    /// The type of entity that contains this reference (StructMember, TemplateArgument, Inheritance, etc.)
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The name of the parent entity (struct name, type name, etc.)
    /// </summary>
    public string ParentEntityName { get; set; } = string.Empty;

    /// <summary>
    /// The specific member or parameter name where the reference occurs (optional)
    /// </summary>
    public string? MemberName { get; set; }

    /// <summary>
    /// Additional context about the location (e.g., parameter position in function, template argument position)
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// The source file of the reference
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// The line number of the reference in the source file
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Creates a string representation of this detailed unresolved reference
    /// </summary>
    public override string ToString()
    {
        var result = $"TypeReference: {TypeString}";

        if (!string.IsNullOrEmpty(ParentEntityName))
        {
            result += $" in {EntityType} '{ParentEntityName}'";

            if (!string.IsNullOrEmpty(MemberName))
            {
                result += $".{MemberName}";
            }

            if (!string.IsNullOrEmpty(Context))
            {
                result += $" ({Context})";
            }
        }

        if (!string.IsNullOrEmpty(File))
        {
            result += $" at {File}";
            if (LineNumber.HasValue)
            {
                result += $":{LineNumber}";
            }
        }
        else if (LineNumber.HasValue)
        {
            result += $" at line {LineNumber}";
        }

        return result;
    }
}
