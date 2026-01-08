using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACDecompileParser.Shared.Lib.Models;

public class StaticVariableModel
{
    [Key] public int Id { get; set; }

    [Required] [MaxLength(50)] public string Address { get; set; } = string.Empty;

    [Required] [MaxLength(500)] public string Name { get; set; } = string.Empty;

    [Required] [MaxLength(500)] public string TypeString { get; set; } = string.Empty;

    [Required] [Column(TypeName = "TEXT")] public string GlobalType { get; set; } = string.Empty;

    [Column(TypeName = "TEXT")] public string? Value { get; set; }

    public int? ParentTypeId { get; set; }

    public int? TypeReferenceId { get; set; }

    [ForeignKey("ParentTypeId")] public virtual TypeModel? ParentType { get; set; }

    [ForeignKey("TypeReferenceId")] public virtual TypeReference? TypeReference { get; set; }
}
