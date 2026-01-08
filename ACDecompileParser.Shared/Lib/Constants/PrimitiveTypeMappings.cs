using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Shared.Lib.Constants;

/// <summary>
/// Provides C++ to C# type mappings for code generation.
/// </summary>
public static class PrimitiveTypeMappings
{
    /// <summary>
    /// Maps normalized C++ types to C# types.
    /// </summary>
    public static readonly Dictionary<string, string> CppToCSharp = new(StringComparer.OrdinalIgnoreCase)
    {
// Signed integers
        { "int", "int" },
        { "int32_t", "int" },
        { "__int32", "int" },
        { "long", "int" },
        { "signed", "int" },
        { "signed int", "int" },
        { "signed long", "int" },
        { "long int", "int" },
// INT and LONG are covered by case-insensitive "int" and "long"

// Unsigned integers  
        { "unsigned int", "uint" },
        { "uint32_t", "uint" },
        { "unsigned __int32", "uint" },
        { "unsigned long", "uint" },
        { "DWORD", "uint" },
        { "UINT", "uint" },
        { "ULONG", "uint" },
        { "unsigned", "uint" },

// 64-bit
        { "__int64", "long" },
        { "int64_t", "long" },
        { "long long", "long" },
        { "signed long long", "long" },
        { "LONGLONG", "long" },
        { "unsigned __int64", "ulong" },
        { "uint64_t", "ulong" },
        { "unsigned long long", "ulong" },
        { "DWORD64", "ulong" },
        { "QWORD", "ulong" },
        { "ULONGLONG", "ulong" },

// 16-bit
        { "short", "short" },
        { "int16_t", "short" },
        { "__int16", "short" },
        { "short int", "short" },
        { "signed short", "short" },
        { "signed short int", "short" },
        { "unsigned short", "ushort" },
        { "uint16_t", "ushort" },
        { "unsigned __int16", "ushort" },
        { "unsigned short int", "ushort" },
        { "WORD", "ushort" },

// 8-bit
        { "char", "sbyte" },
        { "int8_t", "sbyte" },
        { "__int8", "sbyte" },
        { "signed char", "sbyte" },
        { "unsigned char", "byte" },
        { "uint8_t", "byte" },
        { "unsigned __int8", "byte" },
        { "BYTE", "byte" },
        { "_BYTE", "byte" },

// Boolean (use Byte for struct layout compatibility)
        { "bool", "Byte" },

// Floating point
        { "float", "float" },
        { "double", "double" },
        { "long double", "double" },

// Other
        { "void", "void" },
        { "wchar_t", "char" }, // C# char is 16-bit Unicode
        { "WCHAR", "char" },
        { "char16_t", "char" },
        { "char32_t", "uint" },

// Size/ptr types (32-bit)
        { "size_t", "nuint" },
        { "ptrdiff_t", "nint" },
        { "intptr_t", "nint" },
        { "uintptr_t", "nuint" },
        { "DWORD_PTR", "nuint" },
    };

    /// <summary>
    /// Maps C++ calling conventions to C# unmanaged calling conventions.
    /// </summary>
    public static readonly Dictionary<string, string> CallingConventions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "__cdecl", "Cdecl" },
        { "__thiscall", "Thiscall" },
        { "__stdcall", "Stdcall" },
        { "__fastcall", "Fastcall" },
        // Support normalized inputs (Case-insensitive)
        { "cdecl", "Cdecl" },
        { "thiscall", "Thiscall" },
        { "stdcall", "Stdcall" },
        { "fastcall", "Fastcall" },
    };

    /// <summary>
    /// Maps a C++ type string to its C# equivalent.
    /// Handles pointers, arrays, and complex types.
    /// </summary>
    /// <param name="cppType">The C++ type string (may include pointers, const, etc.)</param>
    /// <returns>The equivalent C# type string</returns>
    public static string MapType(string cppType)
    {
        if (string.IsNullOrWhiteSpace(cppType))
            return "void";

        // Normalize the type string first
        string normalized = ParsingUtilities.NormalizeTypeString(cppType);

        // Check if it's a pointer
        bool isPointer = normalized.EndsWith("*");
        if (isPointer)
        {
            // All pointers become void* in C# unsafe structs for simplicity
            return "void*";
        }

        // Check if it's a reference (treat as pointer)
        if (normalized.EndsWith("&"))
        {
            return "void*";
        }

        // Strip const and other qualifiers
        string baseType = normalized
            .Replace("const ", "")
            .Replace("volatile ", "")
            .Replace("struct ", "")
            .Replace("enum ", "")
            .Replace("union ", "")
            .Trim();

        // Look up in mapping
        if (CppToCSharp.TryGetValue(baseType, out string? csType))
        {
            return csType;
        }

        // If not found, return the original (might be a custom type)
        // For custom types in C# bindings, we'll assume they're defined elsewhere
        return baseType;
    }

    /// <summary>
    /// Maps a C++ type to C# for static variable pointers (preserves pointer type).
    /// </summary>
    public static string MapTypeForStaticPointer(string cppType)
    {
        if (string.IsNullOrWhiteSpace(cppType))
            return "void*";

        string normalized = ParsingUtilities.NormalizeTypeString(cppType);

        // Strip pointer suffix if present
        string baseType = normalized.TrimEnd('*', ' ');
        baseType = baseType
            .Replace("const ", "")
            .Replace("volatile ", "")
            .Replace("struct ", "")
            .Replace("enum ", "")
            .Replace("union ", "")
            .Trim();

        // Look up in mapping
        if (CppToCSharp.TryGetValue(baseType, out string? csType))
        {
            return csType + "*";
        }

        // Custom type pointer
        return baseType + "*";
    }

    /// <summary>
    /// Maps a C++ calling convention to the C# unmanaged calling convention name.
    /// </summary>
    public static string MapCallingConvention(string? convention)
    {
        if (string.IsNullOrWhiteSpace(convention))
            return "Cdecl"; // Default to Cdecl

        if (CallingConventions.TryGetValue(convention, out string? csConvention))
        {
            return csConvention;
        }

        return "Cdecl";
    }

    /// <summary>
    /// Detects the appropriate C# enum underlying type based on enum member values.
    /// </summary>
    public static string GetEnumUnderlyingType(IEnumerable<EnumMemberModel>? members)
    {
        if (members == null || !members.Any())
            return "uint"; // Default to uint

        long maxValue = 0;
        long minValue = 0;
        bool hasNegative = false;

        foreach (var member in members)
        {
            if (string.IsNullOrEmpty(member.Value))
                continue;

            string valueStr = member.Value.Trim();

            // Try to parse the value (supports hex and decimal)
            if (TryParseEnumValue(valueStr, out long value))
            {
                if (value < 0)
                    hasNegative = true;
                if (value > maxValue)
                    maxValue = value;
                if (value < minValue)
                    minValue = value;
            }
        }

        // Determine type based on range
        if (hasNegative)
        {
            // Signed types
            if (minValue >= sbyte.MinValue && maxValue <= sbyte.MaxValue)
                return "sbyte";
            if (minValue >= short.MinValue && maxValue <= short.MaxValue)
                return "short";
            if (minValue >= int.MinValue && maxValue <= int.MaxValue)
                return "int";
            return "long";
        }
        else
        {
            // Unsigned types - check for 32-bit forcing pattern
            if (maxValue == 0x7FFFFFFF || maxValue > int.MaxValue)
                return "uint";
            if (maxValue <= byte.MaxValue)
                return "byte";
            if (maxValue <= ushort.MaxValue)
                return "ushort";
            return "uint";
        }
    }

    private static bool TryParseEnumValue(string valueStr, out long value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(valueStr))
            return false;

        // Handle hex values
        if (valueStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            valueStr.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
        {
            string hex = valueStr.Substring(2);
            // Remove any suffix like L, U, LL, etc.
            hex = hex.TrimEnd('L', 'l', 'U', 'u');
            if (long.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out value))
                return true;
            return false;
        }

        // Handle negative hex like -0x...
        if (valueStr.StartsWith("-0x", StringComparison.OrdinalIgnoreCase))
        {
            string hex = valueStr.Substring(3).TrimEnd('L', 'l', 'U', 'u');
            if (long.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out value))
            {
                value = -value;
                return true;
            }

            return false;
        }

        // Handle decimal
        string decStr = valueStr.TrimEnd('L', 'l', 'U', 'u');
        return long.TryParse(decStr, out value);
    }

    /// <summary>
    /// Checks if a member name indicates a vtable pointer.
    /// </summary>
    public static bool IsVTablePointer(string? memberName, string? typeString)
    {
        if (string.IsNullOrEmpty(memberName))
            return false;

        // Common vtable naming patterns
        if (memberName.Contains("vftable") ||
            memberName.Contains("vtbl") ||
            memberName.Contains("__vfptr") ||
            memberName.EndsWith("_vtbl"))
        {
            return true;
        }

        // Check type string for vtable pattern
        if (!string.IsNullOrEmpty(typeString) &&
            (typeString.Contains("_vtbl") || typeString.Contains("Vtbl")))
        {
            return true;
        }

        return false;
    }
}
