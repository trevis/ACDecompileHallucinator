namespace ACDecompileParser.Shared.Lib.Models;

public class StructTypeModel : BaseTypeModel
{
    public override TypeType Type => TypeType.Struct;
    public override string Name { get; set; } = string.Empty;
    public override string Namespace { get; set; } = string.Empty;

    public override string FullyQualifiedName => string.IsNullOrEmpty(Namespace)
        ? Name
        : $"{Namespace}::{Name}";

    /// <summary>
    /// Gets the fully qualified name including template arguments (e.g., "Namespace::TypeName&lt;int,long&gt;")
    /// This matches how TypeModel.FullyQualifiedName computes the FQN and should be used for database lookups.
    /// </summary>
    public string FullyQualifiedNameWithTemplates
    {
        get
        {
            var nameWithTemplates = IsGeneric
                ? $"{Name}<{string.Join(",", TemplateArguments.Select(t => t.TypeString))}>"
                : Name;
            return string.IsNullOrEmpty(Namespace) ? nameWithTemplates : $"{Namespace}::{nameWithTemplates}";
        }
    }

    public override string Source { get; set; } = string.Empty;

    public List<string> BaseTypes { get; set; } = new();
    public List<TypeReference> TemplateArguments { get; set; } = new();
    public List<StructMemberModel> Members { get; set; } = new();
    public bool IsGeneric => TemplateArguments?.Count > 0;
    public bool IsVolatile { get; set; } = false;
    public int? Alignment { get; set; }

    public override TypeModel MakeTypeModel()
    {
        var typeModel = new TypeModel
        {
            BaseName = this.Name,
            Namespace = this.Namespace,
            Type = this.Type,
            Source = this.Source,
            IsVTable = this.Name.EndsWith("_vtbl"), // Set IsVTable based on name suffix
            IsVolatile = this.IsVolatile, // Set IsVolatile property
            File = this.File,
            LineNumber = this.LineNumber,
            Alignment = this.Alignment // Set Alignment property
        };

        // Set the stored fully qualified name to ensure uniqueness in the database
        typeModel.StoredFullyQualifiedName = this.FullyQualifiedName;

        // Convert template arguments to TypeTemplateArgument
        typeModel.TemplateArguments = TemplateArguments.Select((arg, index) => new TypeTemplateArgument
        {
            Position = index,
            TypeString = arg.TypeString,
            TypeReferenceId =
                arg.Id > 0
                    ? arg.Id
                    : (int?)null // Only set TypeReferenceId if the TypeReference has been saved to database
        }).ToList();

        // Convert base types to TypeInheritance
        typeModel.BaseTypes = BaseTypes.Select((bt, index) => new TypeInheritance
        {
            Order = index,
            RelatedTypeString = bt,
            RelatedType = null // Will be resolved later by TypeResolutionService
        }).ToList();

        return typeModel;
    }
}
