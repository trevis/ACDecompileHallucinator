namespace ACDecompileParser.Shared.Lib.Models;

public enum TypeType
{
    Unknown = 0,
    Enum = 1,
    Struct = 2,
    Class = 3,
    Union = 4,
    Typedef = 5,
    Primitive = 6 // For int, float, etc.
}
