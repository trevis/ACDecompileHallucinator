using System.Collections.Generic;

namespace ACDecompileParser.Shared.Lib.Constants;

public static class PrimitiveTypes
{
    public static readonly HashSet<string> TypeNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        // Basic types
        "void", "bool", "char", "wchar_t", "char16_t", "char32_t",
        "int", "short", "long", "signed", "unsigned", "float", "double",
        
        // Sized integer types
        "int8_t", "int16_t", "int32_t", "int64_t",
        "uint8_t", "uint16_t", "uint32_t", "uint64_t",
        "size_t", "ptrdiff_t", "intptr_t", "uintptr_t",
        
        // Windows types
        "BYTE", "WORD", "DWORD", "DWORD64", "QWORD", "DWORD_PTR",
        "INT", "UINT", "LONG", "ULONG", "LONGLONG", "ULONGLONG",
        "HRESULT", "BOOL", "WCHAR", "LPSTR", "LPWSTR", "LPCSTR", "LPCWSTR",
        
        // Common typedefs
        "time_t", "clock_t", "FILE", "DIR", "jmp_buf", "fpos_t",
        "mbstate_t", "va_list", "ptrdiff_t", "sig_atomic_t",
        
        // Additional C++ types
        "nullptr_t", "max_align_t", "decimal32", "decimal64", "decimal128",
        
        "_BYTE", "unsigned __int8", "unsigned __int16", "unsigned __int32", "unsigned __int64",
        "__int8", "__int16", "__int32", "__int64"
    };

    public static readonly HashSet<string> TypeCombinations = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "unsigned int", "unsigned long", "unsigned short", "unsigned char",
        "signed int", "signed long", "signed short", "signed char",
        "long int", "long long", "long double", "short int",
        "unsigned long long", "signed long long"
    };
}
