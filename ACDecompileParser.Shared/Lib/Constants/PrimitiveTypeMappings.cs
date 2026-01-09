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
        { "bool", "byte" },

// Floating point
        { "float", "float" },
        { "double", "double" },
        { "long double", "double" },

// Other
        { "void", "void" },
        { "wchar_t", "System.Char" }, // C# char is 16-bit Unicode
        { "WCHAR", "char" },
        { "char16_t", "char" },
        { "char32_t", "uint" },

// Size/ptr types (32-bit)
        { "size_t", "nuint" },
        { "ptrdiff_t", "nint" },
        { "intptr_t", "nint" },
        { "uintptr_t", "nuint" },
        { "DWORD_PTR", "nuint" },

        { "IDClass<_tagDataID,32,0>", "uint" },
        { "IDClass<_tagVersionHandle,32,0>", "uint" },
        { "void*", "System.IntPtr" },
        { "_DWORD", "int" },
        { "HRESULT", "int" },
        {  "sockaddr_in", "int" },
        { "LPBYTE", "byte*" },
        { "HKEY", "int" },
        { "LPCSTR", "sbyte*"},
        { "_iobuf", "byte"}
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
    /// Comprehensive list of C# reserved keywords.
    /// </summary>
    public static readonly HashSet<string> CSharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
        "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
        "virtual", "void", "volatile", "while"
    };

    /// <summary>
    /// Handles pointers, arrays, and complex types.
    /// </summary>
    /// <param name="cppType">The C++ type string (may include pointers, const, etc.)</param>
    /// <param name="typeRef">Optional TypeReference to check if the type was successfully parsed</param>
    /// <returns>The equivalent C# type string</returns>
    public static string MapType(string cppType, TypeReference? typeRef = null)
    {
        if (string.IsNullOrWhiteSpace(cppType))
            return "void";

        // Check for function pointer syntax directly
        if (ParsingUtilities.IsFunctionPointerParameter(cppType))
        {
            return "System.IntPtr";
        }

        // Normalize the type string first
        string normalized = ParsingUtilities.NormalizeTypeString(cppType);

        // Check if it's a pointer
        bool isPointer = normalized.EndsWith("*");
        if (isPointer)
        {
            // Special case for void* to map to System.IntPtr
            if (normalized == "void*")
            {
                if (CppToCSharp.TryGetValue("void*", out string? mapped))
                    return mapped;
                return "System.IntPtr";
            }

            // Check if this is an unknown/unparsed pointer type
            if (typeRef != null && typeRef.IsPointer)
            {
                bool isUnknownType = typeRef.ReferencedTypeId == null;
                bool isIgnoredType = typeRef.ReferencedType?.IsIgnored == true;

                if (isUnknownType || isIgnoredType)
                {
                    return "System.IntPtr";
                }
            }

            // Known/parsed pointers: Map the base type recursively
            // e.g. "MyType*" -> "ACBindings.MyType*"
            string baseTypePtr = normalized.Substring(0, normalized.Length - 1);
            return MapType(baseTypePtr, typeRef: null) + "*";
        }

        // Check if it's a reference (treat as pointer)
        if (normalized.EndsWith("&"))
        {
            return "void*";
        }

        // Strip const and other qualifiers used for mapping lookup
        string baseType = normalized
            .Replace("const ", "")
            .Replace(" const", "")
            .Replace("volatile ", "")
            .Replace(" volatile", "")
            .Replace("struct ", "")
            .Replace("enum ", "")
            .Replace("union ", "")
            .Trim();

        // Look up in mapping first (prioritize exact collection matches like IDClass)
        if (CppToCSharp.TryGetValue(baseType, out string? csType))
        {
            return csType;
        }

        // Special handling for _STL::vector -> long
        if (baseType.StartsWith("_STL::vector<") || baseType.StartsWith("struct _STL::vector<") ||
            baseType.Equals("_STL::vector", StringComparison.OrdinalIgnoreCase))
        {
            return "long";
        }

        // Check for generics
        int openBracket = baseType.IndexOf('<');
        if (openBracket != -1)
        {
            string genericBase = baseType.Substring(0, openBracket);
            // Ensure we handle the closing bracket correctly
            int closeBracket = baseType.LastIndexOf('>');
            if (closeBracket > openBracket)
            {
                string templateArgsText = baseType.Substring(openBracket + 1, closeBracket - openBracket - 1);
                return ProcessGenericType(genericBase, templateArgsText);
            }
        }

        // If not found, return the original (might be a custom type)
        // For custom types in C# bindings, we'll assume they're defined elsewhere
        return "ACBindings." + CleanTypeName(baseType.Replace("::", "."));
    }

    /// <summary>
    /// Maps a C++ type to C# for static variable pointers (preserves pointer type).
    /// </summary>
    /// <param name="cppType">The C++ type string</param>
    /// <param name="typeRef">Optional TypeReference to check if the type was successfully parsed</param>
    public static string MapTypeForStaticPointer(string cppType, TypeReference? typeRef = null)
    {
        if (string.IsNullOrWhiteSpace(cppType))
            return "void*";

        string normalized = ParsingUtilities.NormalizeTypeString(cppType);

        // Check if this is an unknown/unparsed pointer type first
        if (typeRef != null && typeRef.IsPointer)
        {
            bool isUnknownType = typeRef.ReferencedTypeId == null;
            bool isIgnoredType = typeRef.ReferencedType?.IsIgnored == true;

            if (isUnknownType || isIgnoredType)
            {
                return "System.IntPtr";
            }
        }

        // Strip pointer suffix if present to map the underlying type
        string baseType = normalized.TrimEnd('*', ' ');

        // We do NOT return void* here; we want the actual typed pointer.
        // So we map the underlying type using MapType (which now handles generics/literals), then append *

        string mappedBase = MapType(baseType, typeRef: null);

        // If MapType returns void* (because it thought baseType was a pointer?), we just return it as is?
        // But baseType shouldn't be a pointer if we stripped it.
        // Unless it was a double pointer? ** -> *
        if (mappedBase == "void*")
            return "void*"; // Fallback if internal map failed or it was void**

        return mappedBase + "*";
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

    private static string ProcessGenericType(string baseType, string templateArgs)
    {
        // 1. Map the base type (e.g. SmartArray)
        string mappedBase = MapType(baseType);

        // 2. Parse and map arguments
        var args = SplitTemplateArgs(templateArgs);
        var mappedArgs = new List<string>();

        foreach (var arg in args)
        {
            if (IsNumericLiteral(arg))
            {
                // Skip literals in flattened names
                continue;
            }

            // Map and then clean for identifier
            string mapped = MapType(arg);
            mappedArgs.Add(ToIdentifier(mapped));
        }

        if (mappedArgs.Count == 0)
            return mappedBase;

        // Flatten: Base__Arg1__Arg2
        return $"{mappedBase}__{string.Join("__", mappedArgs)}";
    }

    /// <summary>
    /// Converts a type string into a valid C# identifier part.
    /// ACBindings.Foo -> Foo
    /// System.IntPtr -> System_IntPtr (or void_ptr if preferred, but let's stick to safe mapping)
    /// Foo* -> Foo_ptr
    /// </summary>
    private static string ToIdentifier(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return "void";

        // Handle Ptr<T> wrapper if present (generated by WrapPointerForGeneric logic in tests, though we might not use it now)
        if (typeName.StartsWith("Ptr<") && typeName.EndsWith(">"))
        {
            string inner = typeName.Substring(4, typeName.Length - 5);
            return ToIdentifier(inner) + "_ptr";
        }

        string clean = typeName;

        // Strip ACBindings. prefix for cleaner names
        if (clean.StartsWith("ACBindings."))
            clean = clean.Substring("ACBindings.".Length);

        // Handle pointers
        while (clean.EndsWith("*"))
        {
            clean = clean.Substring(0, clean.Length - 1) + "_ptr";
        }

        // Handle arrays [N] -> _ArrayN_ ?? No, usually used in fixed buffers, not template args.
        // But if it happens:
        clean = clean.Replace("[", "_Array").Replace("]", "_");

        // Handle standard replacements
        // :: -> _
        // . -> _
        // < -> __
        // > -> 
        // , -> __
        // space -> _

        clean = clean
            .Replace("::", "_")
            .Replace(".", "_")
            .Replace("<", "__")
            .Replace(">", "")
            .Replace(",", "__")
            .Replace(" ", "_");

        // Specific cleanups
        if (clean == "System_IntPtr") return "void_ptr"; // Nice alias
        if (clean == "int") return "int";

        return clean;
    }

    private static List<string> SplitTemplateArgs(string args)
    {
        var list = new List<string>();
        int depth = 0;
        int parenDepth = 0;
        int start = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == '<') depth++;
            else if (args[i] == '>') depth--;
            else if (args[i] == '(') parenDepth++;
            else if (args[i] == ')') parenDepth--;
            else if (args[i] == ',' && depth == 0 && parenDepth == 0)
            {
                list.Add(args.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        if (start < args.Length)
            list.Add(args.Substring(start).Trim());

        return list;
    }

    public static bool IsNumericLiteral(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return false;
        // Check for digit start or negative sign followed by digit
        char c = arg[0];
        if (char.IsDigit(c)) return true;
        if (c == '-' && arg.Length > 1 && char.IsDigit(arg[1])) return true;
        return false;
    }

    /// <summary>
    /// Removes $ and other unwanted characters from a type name.
    /// </summary>
    public static string CleanTypeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // Manually handle the specific decompiler artifact BEFORE stripping comments
        string artifact = "// local variable allocation has failed, the output may be wrong!";
        if (name.Contains(artifact))
        {
            name = name.Replace(artifact, "");
        }

        // Strip comments first to handle other comments robustly
        var cleaned = ParsingUtilities.StripComments(name).Trim();

        return cleaned.Replace("$", "_");
    }

    /// <summary>
    /// Wraps pointer types with Ptr&lt;&gt; for use in generic type arguments.
    /// C# does not allow pointer types as generic arguments directly.
    /// </summary>
    public static string WrapPointerForGeneric(string mappedType)
    {
        if (string.IsNullOrEmpty(mappedType))
            return mappedType;

        if (mappedType.EndsWith("*"))
        {
            string baseType = mappedType.TrimEnd('*');
            return $"Ptr<{baseType}>";
        }

        return mappedType;
    }

    /// <summary>
    /// Sanitizes an identifier (member or parameter name) by appending an underscore if it is a C# keyword.
    /// </summary>
    public static string SanitizeIdentifier(string? name)
    {
        name = name?.Replace("~", "Destructor");
        if (string.IsNullOrEmpty(name))
            return "_param";

        if (CSharpKeywords.Contains(name))
            return name + "_";

        // Ensure valid identifier start (though C++ usually already does)
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return "_" + name;

        return name;
    }
}
