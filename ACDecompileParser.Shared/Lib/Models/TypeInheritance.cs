namespace ACDecompileParser.Shared.Lib.Models;

public class TypeInheritance : BaseRelationshipModel
{
    /// <summary>
    /// The derived type ID (the type that inherits from another).
    /// This is an alias for ParentTypeId for semantic clarity - they always have the same value.
    /// </summary>
    public int DerivedTypeId
    {
        get => ParentTypeId;
        set => ParentTypeId = value;
    }

    /// <summary>
    /// The order position of this inheritance relationship (0-based)
    /// </summary>
    public int Order { get; set; }
}
