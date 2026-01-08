namespace ACDecompileParser.Shared.Lib.Models;

/// <summary>
/// Represents a fully parsed type with all its components
/// </summary>
public class ParsedTypeInfo
{
    public string FullTypeString { get; set; } = string.Empty;
    public string BaseName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public bool IsConst { get; set; }
    public bool IsVolatile { get; set; }
    public bool IsPointer { get; set; }
    public bool IsReference { get; set; }
    public int PointerDepth { get; set; }
    public bool IsArray { get; set; }
    public int? ArraySize { get; set; }
    public bool IsGeneric => TemplateArguments.Count > 0;
    public List<ParsedTypeInfo> TemplateArguments { get; set; } = new();
    
    /// <summary>
    /// Gets the simple name with template arguments (e.g., "SmartArray<ContextMenuData,1>")
    /// </summary>
    public string NameWithTemplates
    {
        get
        {
            if (!IsGeneric)
                return BaseName;
            
            var args = string.Join(",", TemplateArguments.Select(t => t.FullTypeString));
            return $"{BaseName}<{args}>";
        }
    }
    
    /// <summary>
    /// Gets the fully qualified name with namespace and templates
    /// </summary>
    public string FullyQualifiedName
    {
        get
        {
            var name = NameWithTemplates;
            return string.IsNullOrEmpty(Namespace) ? name : $"{Namespace}::{name}";
        }
    }
}
