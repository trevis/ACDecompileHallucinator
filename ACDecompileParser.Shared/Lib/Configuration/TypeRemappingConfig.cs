namespace ACDecompileParser.Shared.Lib.Configuration;

public static class TypeRemappingConfig
{
    public static readonly Dictionary<string, string> Mappings = new()
    {
        // Basic integer types
        { "_BYTE", "byte" },
        { "_WORD", "uint16_t" },
        { "_DWORD", "uint32_t" },
        { "_QWORD", "uint64_t" },
        { "unsigned int", "uint32_t" },
        { "unsigned long", "uint32_t" },
        { "unsigned __int64", "uint64_t" },
        { "unsigned short", "uint16_t" },
        { "unsigned char", "uint8_t" },
        { "unsigned __int32", "uint32_t" },
        { "unsigned __int16", "uint16_t" },
        { "unsigned __int8", "uint8_t" },
        { "char",           "byte"     },
        
        // Signed types
        { "int", "int32_t" },
        { "__int64", "int64_t" },
        { "signed int", "int32_t" },
        { "signed long", "int32_t" },
        { "signed short", "int16_t" },
        { "signed char", "int8_t" },
        { "signed __int32", "int32_t" },
        { "signed __int16", "int16_t" },
        { "signed __int8", "int8_t" },
        { "long double", "float64_t"},
        { "double", "float64_t"},
        { "float", "float32_t"}
    };
}
