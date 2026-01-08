namespace ACDecompileParser.Shared.Lib.Models;

public abstract class BaseTypeModel
{
    public abstract TypeType Type { get; }
    public abstract string Name { get; set; }
    public abstract string Namespace { get; set; }
    public abstract string FullyQualifiedName { get; }
    public abstract string Source { get; set; }
    public string? File { get; set; }
    public int? LineNumber { get; set; }

    public abstract TypeModel MakeTypeModel();
}
