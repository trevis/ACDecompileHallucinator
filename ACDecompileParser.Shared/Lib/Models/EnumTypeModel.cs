namespace ACDecompileParser.Shared.Lib.Models;

public class EnumTypeModel : BaseTypeModel
{
    public override TypeType Type => TypeType.Enum;
    public override string Name { get; set; } = string.Empty;
    public override string Namespace { get; set; } = string.Empty;
    public bool IsBitmask { get; set; }

    public override string FullyQualifiedName => string.IsNullOrEmpty(Namespace)
        ? Name
        : $"{Namespace}::{Name}";

    public override string Source { get; set; } = string.Empty;
    public List<EnumMemberModel> Members { get; set; } = new List<EnumMemberModel>();

    public override TypeModel MakeTypeModel()
    {
        var typeModel = new TypeModel
        {
            BaseName = this.Name,
            Namespace = this.Namespace,
            Type = this.Type,
            Source = this.Source,
            File = this.File,
            LineNumber = this.LineNumber,
            IsBitmask = this.IsBitmask
        };

        // Set the stored fully qualified name to ensure uniqueness in the database
        typeModel.StoredFullyQualifiedName = this.FullyQualifiedName;

        return typeModel;
    }
}
