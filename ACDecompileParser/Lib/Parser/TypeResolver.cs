using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Parser;

public class TypeResolver
{
    public static TypeModel CreateTypeModel(StructTypeModel structModel)
    {
        var model = new TypeModel
        {
            BaseName = structModel.Name,  // Now structModel.Name is already clean (no namespace or templates)
            Namespace = structModel.Namespace,
            Type = structModel.Type,
            Source = structModel.Source,
            IsVTable = structModel.Name.EndsWith("_vtbl") // Set the IsVTable property based on name
        };

        // Convert template arguments to TypeTemplateArgument
        model.TemplateArguments = structModel.TemplateArguments.Select((arg, index) => new TypeTemplateArgument
        {
            Position = index,
            TypeString = arg.TypeString,
            TypeReference = arg // Store the reference directly for now, ID will be set during resolution
        }).ToList();

        // Convert base types to TypeInheritance
        model.BaseTypes = structModel.BaseTypes.Select((bt, index) =>
        {
            return new TypeInheritance
            {
                Order = index,
                RelatedTypeString = bt,
                RelatedType = null // We need to create a TypeModel from the ParsedTypeInfo
            };
        }).ToList();

        return model;
    }

    /// <summary>
    /// Creates a TypeReference from a type string and parsed type information.
    /// Delegates to TypeReferenceUtilities for the actual implementation.
    /// </summary>
    public static TypeReference CreateTypeReference(string typeString) => TypeReferenceUtilities.CreateTypeReference(typeString);
}
