using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Utilities;
using System.Text;
using System.Text.RegularExpressions;

namespace ACDecompileParser.Lib.Parser;

public class TypedefParser
{
    /// <summary>
    /// Parses typedef declarations from source lines
    /// Handles: simple typedefs, pointer typedefs, function pointer typedefs
    /// </summary>
    public static List<TypeDefModel> ParseTypedefs(List<string> sourceLines)
    {
        var typedefs = new List<TypeDefModel>();

        for (int i = 0; i < sourceLines.Count; i++)
        {
            var line = sourceLines[i];

            // Look for offset comment followed by typedef
            if (Regex.IsMatch(line, @"\/\*\s*(\d+)\s*\*\/"))
            {
                if (i + 1 < sourceLines.Count)
                {
                    line = sourceLines[++i];
                    if (line.TrimStart().StartsWith("typedef "))
                    {
                        var source = CollectTypedefSource(sourceLines, i);
                        var typeDef = ParseTypedefInternal(source);
                        if (typeDef != null)
                        {
                            typeDef.LineNumber = i + 1;
                            typedefs.Add(typeDef);
                        }
                    }
                }
            }
        }

        return typedefs;
    }

    private static string CollectTypedefSource(List<string> lines, int startIndex)
    {
        var source = new StringBuilder();
        int i = startIndex;

        while (i < lines.Count)
        {
            var line = lines[i];
            source.AppendLine(line);

            if (line.Contains(';'))
            {
                break;
            }

            i++;
        }

        return source.ToString();
    }

    private static TypeDefModel? ParseTypedefInternal(string source)
    {
        source = ParsingUtilities.StripComments(source).Trim();

        if (!source.StartsWith("typedef "))
            return null;

        // Remove "typedef " prefix
        var declaration = source.Substring(8).Trim();

        // Remove trailing semicolon
        if (declaration.EndsWith(";"))
            declaration = declaration.Substring(0, declaration.Length - 1).Trim();

        // Detect typedef type
        if (IsFunctionPointerTypedef(declaration))
        {
            return ParseFunctionPointerTypedef(declaration, source);
        }
        else if (IsFunctionSignatureTypedef(declaration))
        {
            return ParseFunctionSignatureTypedef(declaration, source);
        }
        else
        {
            return ParseSimpleTypedef(declaration, source);
        }
    }

    /// <summary>
    /// Detects: typedef int (__stdcall *NAME)(...);
    /// </summary>
    private static bool IsFunctionPointerTypedef(string declaration)
    {
        // Match: (optional_calling_convention *Name)
        return Regex.IsMatch(declaration, @"\(\s*(__\w+\s+)?\*\s*\w+\s*\)");
    }

    /// <summary>
    /// Detects: typedef bool __cdecl NAME(...);
    /// </summary>
    private static bool IsFunctionSignatureTypedef(string declaration)
    {
        // Check for calling convention followed by name and parentheses
        return Regex.IsMatch(declaration, @"__(cdecl|stdcall|thiscall|fastcall)\s+\w+\s*\(");
    }

    /// <summary>
    /// Parses: typedef HKEY__ **PHKEY;
    /// typedef unsigned __int16 flowqueueInterval_t;
    /// typedef void *RPC_IF_HANDLE;
    /// </summary>
    private static TypeDefModel ParseSimpleTypedef(string declaration, string source)
    {
        // Extract typedef name (rightmost identifier)
        var typedefName = ExtractTypedefName(declaration);

        // Everything before the name is the underlying type
        var underlyingType = declaration.Substring(0,
            declaration.LastIndexOf(typedefName)).Trim();

        // Create TypeReference for what it points to
        var typeReference = TypeResolver.CreateTypeReference(underlyingType);

        return new TypeDefModel
        {
            Name = typedefName,
            Namespace = string.Empty,
            TypeReference = typeReference,
            Source = source
        };
    }

    /// <summary>
    /// Parses: typedef int (__stdcall *VERIFY_SIGNATURE_FN)(_SecHandle *, ...);
    /// typedef int (__stdcall *FARPROC)();
    /// </summary>
    private static TypeDefModel ParseFunctionPointerTypedef(string declaration, string source)
    {
        // Extract components using regex
        // Pattern: ReturnType (CallingConvention *Name)(Parameters)
        var match = Regex.Match(declaration,
            @"^(.+?)\s*\(\s*(__\w+)?\s*\*\s*(\w+)\s*\)\s*\((.*?)\)\s*$");

        if (!match.Success)
        {
            Console.WriteLine($"Warning: Failed to parse function pointer typedef: {declaration}");
            return ParseSimpleTypedef(declaration, source); // Fallback
        }

        var returnType = match.Groups[1].Value.Trim();
        var callingConvention = match.Groups[2].Value.Trim();
        if (string.IsNullOrEmpty(callingConvention))
        {
            callingConvention = string.Empty;
        }

        var typedefName = match.Groups[3].Value.Trim();
        var parameters = match.Groups[4].Value.Trim();

        // Create FunctionSignatureModel
        var functionSignature = new FunctionSignatureModel
        {
            Name = typedefName,
            ReturnType = returnType,
            CallingConvention = callingConvention,
            ReturnTypeReference = TypeResolver.CreateTypeReference(returnType),
            Parameters = FunctionParamParser.ParseFunctionSignatureParameters(parameters)
        };

        // Create a TypeReference pointing to a function pointer type
        var typeReference = new TypeReference
        {
            TypeString = $"{returnType} ({callingConvention} *)({parameters})",
            IsPointer = true,
            PointerDepth = 1
        };

        return new TypeDefModel
        {
            Name = typedefName,
            Namespace = string.Empty,
            TypeReference = typeReference,
            FunctionSignature = functionSignature,
            Source = source
        };
    }

    /// <summary>
    /// Parses: typedef bool __cdecl InputFilter(unsigned __int16);
    /// </summary>
    private static TypeDefModel ParseFunctionSignatureTypedef(string declaration, string source)
    {
        // Extract components
        var match = Regex.Match(declaration,
            @"^(.+?)\s+(__\w+)\s+(\w+)\s*\(([^)]*)\)");

        if (!match.Success)
        {
            Console.WriteLine($"Warning: Failed to parse function signature typedef: {declaration}");
            return ParseSimpleTypedef(declaration, source); // Fallback
        }

        var returnType = match.Groups[1].Value.Trim();
        var callingConvention = match.Groups[2].Value.Trim();
        var typedefName = match.Groups[3].Value.Trim();
        var parameters = match.Groups[4].Value.Trim();

        // Similar to function pointer, but no pointer indirection
        var functionSignature = new FunctionSignatureModel
        {
            Name = typedefName,
            ReturnType = returnType,
            CallingConvention = callingConvention,
            ReturnTypeReference = TypeResolver.CreateTypeReference(returnType),
            Parameters = FunctionParamParser.ParseFunctionSignatureParameters(parameters)
        };

        var typeReference = new TypeReference
        {
            TypeString = $"{returnType} {callingConvention} ({parameters})",
            IsPointer = false
        };

        return new TypeDefModel
        {
            Name = typedefName,
            Namespace = string.Empty,
            TypeReference = typeReference,
            FunctionSignature = functionSignature,
            Source = source
        };
    }

    private static string ExtractTypedefName(string declaration)
    {
        // First, check if this is a function pointer or function signature typedef
        // If so, we need to handle it differently since the simple extraction won't work
        if (declaration.Contains("(*") || declaration.Contains("("))
        {
            // For function pointers: ReturnType (*Name)(params)
            var funcPtrMatch = Regex.Match(declaration, @"\*\s*(\w+)\s*\)");
            if (funcPtrMatch.Success)
            {
                return funcPtrMatch.Groups[1].Value;
            }

            // For function signatures: ReturnType CallingConv Name(params)
            var funcSigMatch = Regex.Match(declaration, @"__\w+\s+(\w+)\s*\(");
            if (funcSigMatch.Success)
            {
                return funcSigMatch.Groups[1].Value;
            }
        }

        // For simple typedefs: remove trailing pointers, references, arrays
        var cleaned = declaration.TrimEnd('*', '&', ' ', '\t');

        // Find rightmost identifier
        var match = Regex.Match(cleaned, @"(\w+)$");
        return match.Success ? match.Value : declaration;
    }
}
