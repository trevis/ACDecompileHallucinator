using System.ComponentModel.DataAnnotations.Schema;

namespace ACDecompileParser.Shared.Lib.Models;

public class TypeModel
{
    public int Id { get; set; }

    /// <summary>
    /// The simple base name without namespace or templates (e.g., "SmartArray")
    /// </summary>
    public string BaseName { get; set; } = string.Empty;

    /// <summary>
    /// The namespace of the type
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// The type classification (enum, struct, class, etc.)
    /// </summary>
    public TypeType Type { get; set; }

    /// <summary>
    /// The full source code of the type definition
    /// </summary>
    public string Source { get; set; } = string.Empty;

    // Relationships
    public virtual List<TypeTemplateArgument> TemplateArguments { get; set; } = new();
    public virtual List<TypeInheritance> BaseTypes { get; set; } = new();
    public List<StructMemberModel> StructMembers { get; set; } = new();
    public virtual List<FunctionBodyModel> FunctionBodies { get; set; } = new();
    public virtual List<StaticVariableModel> StaticVariables { get; set; } = new();

    /// <summary>
    /// Gets whether this type has template arguments
    /// </summary>
    public bool IsGeneric => TemplateArguments.Count > 0;

    /// <summary>
    /// Gets the simple name with template arguments (e.g., "SmartArray&lt;ContextMenuData,1&gt;")
    /// </summary>
    public string NameWithTemplates
    {
        get
        {
            if (!IsGeneric)
                return BaseName;

            var args = string.Join(",", TemplateArguments
                .OrderBy(t => t.Position)
                .Select(t =>
                {
                    // Prioritize the original type string to preserve pointer information in template arguments
                    // Only use FullyQualifiedName if RelatedTypeString is empty
                    if (!string.IsNullOrEmpty(t.TypeString))
                        return t.TypeString;
                    else if (t.TypeReference != null)
                        return t.TypeReference.TypeString;
                    else
                        return string.Empty;
                }));
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

    /// <summary>
    /// The stored fully qualified name in the database
    /// </summary>
    private string _storedFullyQualifiedName = string.Empty;

    /// <summary>
    /// Gets the stored fully qualified name from the database
    /// </summary>
    public string StoredFullyQualifiedName
    {
        get { return string.IsNullOrEmpty(_storedFullyQualifiedName) ? FullyQualifiedName : _storedFullyQualifiedName; }
        set { _storedFullyQualifiedName = value; }
    }

    /// <summary>
    /// Gets whether this type is a vtable (structs ending with "_vtbl")
    /// </summary>
    public bool IsVTable { get; set; }

    /// <summary>
    /// Gets whether this type is volatile (structs declared with 'volatile' keyword)
    /// </summary>
    public bool IsVolatile { get; set; }

    /// <summary>
    /// Gets whether this type is a bitmask enum (enums declared with '__bitmask' keyword)
    /// </summary>
    public bool IsBitmask { get; set; }

    /// <summary>
    /// The base type path used for grouping related types in output
    /// </summary>
    public string BaseTypePath { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this type should be ignored during output generation
    /// </summary>
    public bool IsIgnored { get; set; } = false;

    /// <summary>
    /// The source file of the type definition
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// The line number of the type definition in the source file
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// The explicit alignment of the type (e.g. from __declspec(align(N)))
    /// </summary>
    public int? Alignment { get; set; }

    /// <summary>
    /// Reference to the parent type if this is a nested type (not persisted to database)
    /// </summary>
    [NotMapped]
    public TypeModel? ParentType { get; set; }

    /// <summary>
    /// Collection of nested types that belong inside this class (not persisted to database)
    /// </summary>
    [NotMapped]
    public List<TypeModel>? NestedTypes { get; set; }
}
