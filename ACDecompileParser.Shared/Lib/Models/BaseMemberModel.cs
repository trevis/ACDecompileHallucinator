using System;

namespace ACDecompileParser.Shared.Lib.Models;

public abstract class BaseMemberModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? File { get; set; }
    public int? LineNumber { get; set; }

    // Navigation property (not stored in DB)
    public TypeModel? ParentType { get; set; }
}
