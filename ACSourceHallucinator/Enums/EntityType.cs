namespace ACSourceHallucinator.Enums;

public enum EntityType
{
    Struct,
    Enum,
    StructMember,
    EnumMember,
    StructMethod,       // FunctionBodyModel with ParentId != null
    FreeFunction        // FunctionBodyModel with ParentId == null
}
