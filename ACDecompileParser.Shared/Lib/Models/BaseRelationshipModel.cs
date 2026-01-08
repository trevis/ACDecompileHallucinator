using System;

namespace ACDecompileParser.Shared.Lib.Models;

public abstract class BaseRelationshipModel
{
    public int Id { get; set; }
    public int ParentTypeId { get; set; }
    public int? RelatedTypeId { get; set; }
    
    /// <summary>
    /// Raw type string before resolution
    /// </summary>
    public string RelatedTypeString { get; set; } = string.Empty;
    
    /// <summary>
    /// Resolved type reference (null if not yet resolved)
    /// </summary>
    public TypeModel? RelatedType { get; set; }
}
