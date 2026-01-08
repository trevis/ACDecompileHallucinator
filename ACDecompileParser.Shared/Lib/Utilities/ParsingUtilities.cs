using System.Text.RegularExpressions;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Lib.Utilities;

public static class ParsingUtilities
{
    /// <summary>
    /// Extracts the definition line from source code (e.g., line starting with "struct" or "enum")
    /// </summary>
    public static string GetDefinitionLine(string source, string keyword)
    {
        return source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.TrimStart().StartsWith(keyword)) ?? string.Empty;
    }

    /// <summary>
    /// Cleans the definition line by removing common C++ keywords and attributes
    /// </summary>
    public static string CleanDefinitionLine(string defLine, string keyword)
    {
        defLine = defLine.Replace($"{keyword} ", string.Empty);
        defLine = defLine.Replace("__cppobj", string.Empty);
        defLine = defLine.Replace("/*VFT*/", string.Empty);
        defLine = Regex.Replace(defLine, @"__declspec(\(align(\(\d+\)\))?)?", string.Empty);
        defLine = defLine.Replace("__unaligned", string.Empty);
        // Remove volatile keyword if present
        defLine = defLine.Replace("volatile ", string.Empty);
        return defLine.Trim();
    }

    /// <summary>
    /// Strips C-style comments from a declaration
    /// </summary>
    public static string StripComments(string declaration)
    {
        // Handle null input to prevent exceptions
        if (declaration == null)
            return string.Empty;

        // Remove C-style comments /* */ from the declaration
        // This handles both offset comments like /* 0x08 */ and other comments like /*VFT*/
        return Regex.Replace(declaration, @"/\*\s*[^*]*\*+([^/*][^*]*\*+)*/", string.Empty);
    }

    /// <summary>
    /// Normalizes a type string by removing extra whitespace and normalizing pointer/reference syntax.
    /// Does NOT use TypeParser.ParseType to avoid issues with pointers inside templates.
    /// </summary>
    public static string NormalizeTypeString(string typeString)
    {
        if (string.IsNullOrWhiteSpace(typeString))
            return string.Empty;

        // Collapse multiple spaces into single spaces
        typeString = Regex.Replace(typeString, @"\s+", " ");

        // Remove spaces before * and &
        typeString = typeString.Replace(" *", "*").Replace(" &", "&");

        // Remove spaces before >
        typeString = typeString.Replace(" >", ">");

        return typeString.Trim();
    }

    /// <summary>
    /// Checks if the given word is a C++ type keyword.
    /// </summary>
    public static bool IsCppTypeKeyword(string word)
    {
        var cppTypeKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            // Basic types
            "int", "char", "float", "double", "bool", "void", "short", "long", "signed", "unsigned",
            "wchar_t", "char16_t", "char32_t", "auto", "nullptr_t",

            // Modifiers
            "const", "volatile", "restrict", "constexpr", "consteval", "constinit",

            // Storage specifiers
            "static", "extern", "register", "mutable", "thread_local",

            // Function specifiers
            "inline", "virtual", "explicit", "friend",

            // Access specifiers
            "public", "private", "protected",

            // Other keywords that can appear in type contexts
            "struct", "class", "union", "enum",

            // Microsoft-specific keywords that should not be parsed as types
            "__unaligned", "__declspec", "__cppobj", "__thiscall", "__stdcall",
            "__cdecl", "__fastcall", "__inline", "__forceinline", "__restrict", "__based",
            "__ptr32", "__ptr64", "__w64", "__try", "__except", "__finally"
        };

        return cppTypeKeywords.Contains(word);
    }

    /// <summary>
    /// Checks if the given word is a primitive type keyword (int, char, float, etc.)
    /// These are type keywords that should be treated as TypeName in type contexts.
    /// </summary>
    public static bool IsPrimitiveTypeKeyword(string word)
    {
        var primitiveTypeKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            // Basic types
            "int", "char", "float", "double", "bool", "void", "short", "long", "signed", "unsigned",
            "wchar_t", "char16_t", "char32_t", "auto", "nullptr_t",

            // Type declaration keywords
            "struct", "class", "union", "enum"
        };

        return primitiveTypeKeywords.Contains(word);
    }

    /// <summary>
    /// Checks if the given string is a single C++ type keyword (not a compound expression)
    /// </summary>
    public static bool IsSingleCppTypeKeyword(string text)
    {
        text = text.Trim();
        // Split by whitespace and check if it's a single word that is a type keyword
        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return IsCppTypeKeyword(parts[0]);
        }

        return false;
    }

    /// <summary>
    /// Extracts the type string by finding the member name and taking everything before it
    /// </summary>
    public static string ExtractTypeFromDeclaration(string declaration, string name, string? file = null,
        int? lineNumber = null)
    {
        // First, strip comments from the declaration
        string cleanDeclaration = StripComments(declaration);

        int nameIndex = cleanDeclaration.LastIndexOf(name);
        if (nameIndex > 0)
        {
            string typePart = cleanDeclaration.Substring(0, nameIndex).Trim();

            // Remove "struct" keyword when it appears in front of a type
            if (typePart.StartsWith("struct ", StringComparison.OrdinalIgnoreCase))
            {
                typePart = typePart.Substring(7).Trim(); // Remove "struct " (7 characters)
            }

            // Validate the extracted type
            ValidateParsedType(typePart, declaration, "ExtractTypeFromDeclaration", file, lineNumber);

            return typePart;
        }
        else if (nameIndex == 0)
        {
            // Name found at position 0 means there's no type before it
            Console.WriteLine(
                $"{((file != null && lineNumber != null) ? $"[{file}:{lineNumber}] " : "")}Warning: ExtractTypeFromDeclaration - Name '{name}' found at position 0, no type extracted from: {declaration}");
        }
        else
        {
            // Name not found in declaration
            Console.WriteLine(
                $"{((file != null && lineNumber != null) ? $"[{file}:{lineNumber}] " : "")}Warning: ExtractTypeFromDeclaration - Name '{name}' not found in declaration: {declaration}");
        }

        string result = cleanDeclaration.Trim();

        // Remove "struct" keyword when it appears in front of a type
        if (result.StartsWith("struct ", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Substring(7).Trim(); // Remove "struct " (7 characters)
        }

        // Validate the fallback type
        ValidateParsedType(result, declaration, "ExtractTypeFromDeclaration (fallback)", file, lineNumber);

        return result;
    }

    /// <summary>
    /// Validates that a name looks like a valid C++ identifier
    /// </summary>
    public static bool IsValidCppIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Must match: [a-zA-Z_][a-zA-Z0-9_]* (or auto-generated __param/member patterns)
        return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    /// <summary>
    /// Validates that a parsed name is reasonable and logs warnings if suspicious
    /// </summary>
    public static void ValidateParsedName(string name, string declaration, string context, string? file = null,
        int? lineNumber = null)
    {
        string location = (file != null && lineNumber != null) ? $"[{file}:{lineNumber}] " : "";

        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine($"{location}Warning: {context} - Extracted empty name from: {declaration}");
            return;
        }

        // Destructor names (starting with ~) can contain spaces due to template arguments
        // Example: ~PackableHashIterator<unsigned long, SpellBookPage>
        bool isDestructor = name.StartsWith("~");

        if (!isDestructor && name.Contains(' '))
        {
            Console.WriteLine($"{location}Warning: {context} - Name contains spaces: '{name}' from: {declaration}");
        }

        // Skip identifier validation for destructors since they can have template syntax
        if (!isDestructor && !IsValidCppIdentifier(name))
        {
            Console.WriteLine(
                $"{location}Warning: {context} - Name contains invalid characters: '{name}' from: {declaration}");
        }

        if (IsCppTypeKeyword(name))
        {
            Console.WriteLine($"{location}Warning: {context} - Name is a C++ keyword: '{name}' from: {declaration}");
        }
    }

    /// <summary>
    /// Validates that a parsed type is reasonable and logs warnings if suspicious
    /// </summary>
    public static void ValidateParsedType(string type, string declaration, string context, string? file = null,
        int? lineNumber = null)
    {
        string location = (file != null && lineNumber != null) ? $"[{file}:{lineNumber}] " : "";

        if (string.IsNullOrWhiteSpace(type))
        {
            Console.WriteLine(
                $"{location}Warning: {context} - Extracted empty or whitespace-only type from: {declaration}");
            return;
        }

        // Check for unbalanced angle brackets (common in template parsing failures)
        // Skip this check if the type contains "operator" to avoid false positives from operator< and operator>
        if (!type.Contains("operator"))
        {
            int openBrackets = type.Count(c => c == '<');
            int closeBrackets = type.Count(c => c == '>');
            if (openBrackets != closeBrackets)
            {
                Console.WriteLine(
                    $"{location}Warning: {context} - Type has unbalanced angle brackets: '{type}' from: {declaration}");
            }
        }
    }

    /// <summary>
    /// Extracts the member name from a declaration by looking for the identifier at the end
    /// </summary>
    public static string ExtractNameFromDeclaration(string declaration, string? file = null, int? lineNumber = null)
    {
        // First, strip comments from the declaration
        string cleanDeclaration = StripComments(declaration);

        // Find the member name by looking for the identifier at the end
        // This handles cases like "Type *name", "Type name[10]", "const Type& name", etc.

        // First, remove any array brackets and their contents
        string withoutArray = System.Text.RegularExpressions.Regex.Replace(cleanDeclaration, @"\[.*?\]", string.Empty);

        // Remove trailing pointer asterisks and spaces before extracting the name
        string withoutTrailingPointers = withoutArray;
        int lastNonSpaceIndex = withoutTrailingPointers.Length - 1;
        while (lastNonSpaceIndex >= 0 && char.IsWhiteSpace(withoutTrailingPointers[lastNonSpaceIndex]))
        {
            lastNonSpaceIndex--;
        }

        // Remove trailing asterisks (pointers) and any whitespace before them
        while (lastNonSpaceIndex >= 0 && withoutTrailingPointers[lastNonSpaceIndex] == '*')
        {
            // Move past the asterisk
            lastNonSpaceIndex--;
            // Move past any whitespace before the asterisk
            while (lastNonSpaceIndex >= 0 && char.IsWhiteSpace(withoutTrailingPointers[lastNonSpaceIndex]))
            {
                lastNonSpaceIndex--;
            }
        }

        if (lastNonSpaceIndex >= 0)
        {
            withoutTrailingPointers = withoutTrailingPointers.Substring(0, lastNonSpaceIndex + 1);
        }
        else
        {
            withoutTrailingPointers = string.Empty;
        }

        // Look for pointer/reference symbols and extract the identifier after them
        // Match the last sequence of word characters that might be preceded by *, &, or other symbols
        var matches = System.Text.RegularExpressions.Regex.Matches(withoutTrailingPointers, @"[\w]+[\w\d_]*$");
        if (matches.Count > 0)
        {
            string extractedName = matches[matches.Count - 1].Value;
            // Validate the extracted name
            ValidateParsedName(extractedName, declaration, "ExtractNameFromDeclaration", file, lineNumber);
            return extractedName;
        }

        // Fallback: just get the last word
        var parts = withoutTrailingPointers.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            string extractedName = parts[parts.Length - 1];
            // Validate the extracted name
            ValidateParsedName(extractedName, declaration, "ExtractNameFromDeclaration", file, lineNumber);
            return extractedName;
        }

        // No name could be extracted
        Console.WriteLine(
            $"{((file != null && lineNumber != null) ? $"[{file}:{lineNumber}] " : "")}Warning: ExtractNameFromDeclaration - Could not extract any name from: {declaration}");
        return string.Empty;
    }

    /// <summary>
    /// Extracts an identifier that may include template arguments, balancing angle brackets and parentheses
    /// </summary>
    private static string ExtractIdentifierWithTemplates(string str, int startIndex)
    {
        if (startIndex >= str.Length)
            return string.Empty;

        var result = new System.Text.StringBuilder();
        int i = startIndex;
        int angleBracketDepth = 0;
        int parenDepth = 0;

        // Extract identifier characters, template arguments, and scope resolution
        while (i < str.Length)
        {
            char c = str[i];

            if (c == '<')
            {
                angleBracketDepth++;
                result.Append(c);
                i++;
            }
            else if (c == '>')
            {
                if (angleBracketDepth > 0)
                {
                    angleBracketDepth--;
                    result.Append(c);
                    i++;
                }
                else
                {
                    // Found unmatched '>', stop here
                    break;
                }
            }
            else if (c == '(')
            {
                // Parentheses inside templates (e.g., function pointer types)
                if (angleBracketDepth > 0)
                {
                    parenDepth++;
                    result.Append(c);
                    i++;
                }
                else
                {
                    // Parentheses outside templates end the identifier
                    break;
                }
            }
            else if (c == ')')
            {
                // Closing parenthesis inside templates
                if (angleBracketDepth > 0 && parenDepth > 0)
                {
                    parenDepth--;
                    result.Append(c);
                    i++;
                }
                else
                {
                    // Unmatched or outside templates, stop here
                    break;
                }
            }
            else if (char.IsLetterOrDigit(c) || c == '_' || c == '~')
            {
                result.Append(c);
                i++;
            }
            else if (c == ':' && i + 1 < str.Length && str[i + 1] == ':')
            {
                // Namespace separator
                result.Append("::");
                i += 2;
            }
            else if (c == ' ' || c == '\t')
            {
                // Whitespace inside templates or function pointer types
                if (angleBracketDepth > 0 || parenDepth > 0)
                {
                    result.Append(c);
                    i++;
                }
                else
                {
                    // Whitespace outside templates ends the identifier
                    break;
                }
            }
            else if (c == ',' || c == '*' || c == '&')
            {
                // These characters inside templates or function pointers are part of it
                if (angleBracketDepth > 0 || parenDepth > 0)
                {
                    result.Append(c);
                    i++;
                }
                else
                {
                    // Outside templates, they end the identifier
                    break;
                }
            }
            else
            {
                // Other special characters inside templates (semicolons, etc.)
                if (angleBracketDepth > 0 || parenDepth > 0)
                {
                    result.Append(c);
                    i++;
                }
                else
                {
                    // Outside templates, any other character ends the identifier
                    break;
                }
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Finds the last asterisk that's not inside angle brackets (template arguments)
    /// </summary>
    private static int FindLastAsteriskOutsideAngleBrackets(string str)
    {
        int angleBracketDepth = 0;
        int lastAsterisk = -1;
        bool inOperatorContext = false;

        for (int i = 0; i < str.Length; i++)
        {
            // Check if we're entering an operator context
            if (i + 8 <= str.Length && str.Substring(i, 8) == "operator")
            {
                inOperatorContext = true;
                i += 7; // Skip "operator" - will increment by 1 in loop
                continue;
            }

            // If we're in operator context, skip until we hit whitespace or parenthesis
            if (inOperatorContext)
            {
                char c = str[i];
                if (char.IsWhiteSpace(c) || c == '(' || c == ')')
                {
                    inOperatorContext = false;
                }
                else
                {
                    continue; // Skip angle bracket tracking in operator names
                }
            }

            char ch = str[i];

            if (ch == '<')
            {
                angleBracketDepth++;
            }
            else if (ch == '>')
            {
                if (angleBracketDepth > 0)
                    angleBracketDepth--;
            }
            else if (ch == '*' && angleBracketDepth == 0)
            {
                // Found an asterisk outside angle brackets
                lastAsterisk = i;
            }
        }

        return lastAsterisk;
    }

    /// <summary>
    /// Extracts the function name from the pointer part of a function pointer declaration
    /// Handles: *Name, *const *Name, *~ClassName<...>, etc.
    /// </summary>
    private static string ExtractFunctionNameFromPointerPart(string pointerPart)
    {
        if (string.IsNullOrWhiteSpace(pointerPart))
            return string.Empty;

        // Find the last asterisk that's not inside angle brackets
        int lastAsterisk = FindLastAsteriskOutsideAngleBrackets(pointerPart);
        if (lastAsterisk < 0)
            return pointerPart.Trim();

        // Extract everything after the last asterisk
        string afterAsterisk = pointerPart.Substring(lastAsterisk + 1).Trim();

        // Handle "const" keyword (for patterns like "*const *Name")
        if (afterAsterisk.StartsWith("const "))
        {
            afterAsterisk = afterAsterisk.Substring(6).Trim();

            // If there's another asterisk after const, skip it
            if (afterAsterisk.StartsWith("*"))
            {
                afterAsterisk = afterAsterisk.Substring(1).Trim();
            }
        }

        // Check if this is a destructor name (starts with ~)
        if (afterAsterisk.StartsWith("~"))
        {
            // Extract destructor name with template arguments if present
            return ExtractIdentifierWithTemplates(afterAsterisk, 0);
        }

        // Check if this is an operator overload
        if (afterAsterisk.StartsWith("operator"))
        {
            // Return the full operator string (it will be cleaned up later)
            return afterAsterisk.Trim();
        }

        // For regular names, extract until the first non-identifier character
        // But handle template arguments if present
        if (afterAsterisk.Contains('<'))
        {
            return ExtractIdentifierWithTemplates(afterAsterisk, 0);
        }

        // Simple identifier without templates
        var match = System.Text.RegularExpressions.Regex.Match(afterAsterisk, @"^[a-zA-Z_][a-zA-Z0-9_]*");
        return match.Success ? match.Value : afterAsterisk;
    }

    /// <summary>
    /// Finds the matching opening parenthesis for a closing parenthesis, accounting for angle brackets and operator names
    /// </summary>
    public static int FindMatchingOpenParen(string str, int closeParenIndex)
    {
        int parenDepth = 0;
        int angleBracketDepth = 0;

        for (int i = closeParenIndex; i >= 0; i--)
        {
            // Check if we're at the end of "operator" keyword (scanning backwards)
            if (i >= 7 && str.Substring(i - 7, 8) == "operator")
            {
                // Skip past the operator keyword and any operator symbols that follow it
                // This prevents < and > in operator names from being treated as angle brackets
                i -= 7; // Move to the 'o' in "operator", loop will decrement to before it
                continue;
            }

            char c = str[i];

            // Before treating < or > as angle brackets, check if we're inside an operator name
            // by looking backwards for "operator" keyword followed by symbols
            bool insideOperatorName = false;
            if ((c == '<' || c == '>') && i > 8)
            {
                // Search backwards from current position to see if we're in an operator name
                for (int j = i; j >= 8; j--)
                {
                    if (str.Substring(j - 8, 8) == "operator")
                    {
                        // Found "operator" keyword ending at position j
                        // Check if there's only operator symbols (and no spaces/identifiers) between end of "operator" and current position
                        int operatorStart = j - 8;
                        string between = str.Substring(operatorStart, i - operatorStart + 1);
                        if (between.StartsWith("operator"))
                        {
                            string afterOperator = between.Substring(8);
                            // Check if everything after "operator" is operator symbols (no letters, digits, or spaces)
                            if (afterOperator.Length == 0 ||
                                !afterOperator.Any(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)))
                            {
                                insideOperatorName = true;
                                break;
                            }
                        }
                    }

                    // Stop searching if we hit a space, asterisk, or opening paren (we've left the operator context)
                    if (str[j] == ' ' || str[j] == '*' || str[j] == '(')
                        break;
                }
            }

            if (!insideOperatorName && c == '>')
            {
                angleBracketDepth++;
            }
            else if (!insideOperatorName && c == '<')
            {
                if (angleBracketDepth > 0)
                    angleBracketDepth--;
            }
            else if (angleBracketDepth == 0)
            {
                // Only count parentheses when not inside angle brackets
                if (c == ')')
                {
                    parenDepth++;
                }
                else if (c == '(')
                {
                    parenDepth--;
                    if (parenDepth == 0)
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Extracts function pointer information from a declaration
    /// </summary>
    public static (bool IsFunctionPointer, string ReturnType, string Name, string CallingConvention, string Parameters)
        ExtractFunctionPointerInfo(string declaration)
    {
        var trimmed = declaration.Trim();
        if (!trimmed.Contains("(") || !trimmed.Contains(")") || !trimmed.Contains("*"))
            return (false, "", "", "", "");

        // Find last parameter parentheses pair (accounting for angle brackets)
        int lastCloseParen = trimmed.LastIndexOf(')');
        if (lastCloseParen < 0) return (false, "", "", "", "");

        int paramOpen = FindMatchingOpenParen(trimmed, lastCloseParen);
        if (paramOpen == -1) return (false, "", "", "", "");

        string parameters = trimmed.Substring(paramOpen + 1, lastCloseParen - paramOpen - 1).Trim();
        string beforeParams = trimmed.Substring(0, paramOpen).Trim();

        // Pointer part parentheses (also accounting for angle brackets)
        int pointerClose = beforeParams.Length - 1;
        if (beforeParams[pointerClose] != ')') return (false, "", "", "", "");

        int pointerOpen = FindMatchingOpenParen(beforeParams, pointerClose);
        if (pointerOpen == -1) return (false, "", "", "", "");

        string returnType = beforeParams.Substring(0, pointerOpen).Trim();
        string pointerPart = beforeParams.Substring(pointerOpen + 1, pointerClose - pointerOpen - 1).Trim();

        // Extract calling convention
        string callingConvention = "";
        var ccMatch =
            System.Text.RegularExpressions.Regex.Match(pointerPart,
                @"\b(__thiscall|__stdcall|__cdecl|__fastcall)\b\s*");
        if (ccMatch.Success)
        {
            callingConvention = ccMatch.Value.Trim();
            pointerPart = pointerPart.Remove(ccMatch.Index, ccMatch.Length).Trim();
        }

        // ───────────────────────────────────────────────────
        // Critical part: robust name extraction from pointerPart
        // Handles: *Name, *const *Name, *~ClassName<...>, *operator+=, etc.
        // ───────────────────────────────────────────────────────────
        string functionName = ExtractFunctionNameFromPointerPart(pointerPart);

        if (string.IsNullOrWhiteSpace(functionName))
            return (false, "", "", "", "");

        // Handle operator overloading - fix spacing issues like "operator ==" → "operator=="
        if (functionName.StartsWith("operator ") && functionName.Length > "operator ".Length)
        {
            var afterOperator = functionName.Substring("operator ".Length).Trim();
            if (afterOperator.Length <= 3 && !char.IsLetterOrDigit(afterOperator[0])) // short operators
            {
                functionName = "operator" + afterOperator.Replace(" ", "");
            }
        }

        // Apply operator name normalization for known operators
        if (functionName.StartsWith("operator"))
        {
            functionName = ExtractOperatorName(functionName);
        }

        return (true, returnType, functionName, callingConvention, parameters);
    }

    /// <summary>
    /// Extracts operator name from function pointer declarations
    /// </summary>
    private static string ExtractOperatorName(string input)
    {
        if (!input.StartsWith("operator"))
            return "";

        // Two-character compound assignment operators
        if (input.Length >= 10)
        {
            string twoChar = input.Substring(8, 2);
            if (twoChar == "+=" || twoChar == "-=" || twoChar == "*=" || twoChar == "/=" ||
                twoChar == "%=" || twoChar == "&=" || twoChar == "|=" || twoChar == "^=" ||
                twoChar == "<<" || twoChar == ">>" || twoChar == "<=" || twoChar == ">=" ||
                twoChar == "==" || twoChar == "!=" || twoChar == "++" || twoChar == "--" ||
                twoChar == "[]" || twoChar == "()" || twoChar == "->")
            {
                return "operator" + twoChar;
            }
        }

        // Three-character operators
        if (input.Length >= 11)
        {
            string threeChar = input.Substring(8, 3);
            if (threeChar == "<<=" || threeChar == ">>=" || threeChar == "->*")
            {
                return "operator" + threeChar;
            }
        }

        // Single character operators
        if (input.Length >= 9)
        {
            char op = input[8];
            if ("+-*/%&|^~!<>=".Contains(op))
            {
                return "operator" + op;
            }
        }

        // Named operators (new, delete, etc.)
        if (input.Length > 8)
        {
            char firstChar = input[8];
            if (char.IsLetter(firstChar))
            {
                int pos = 8;
                while (pos < input.Length && (char.IsLetterOrDigit(input[pos]) || input[pos] == '_'))
                    pos++;
                return input.Substring(0, pos);
            }
        }

        return input;
    }

    /// <summary>
    /// Extracts a clean base name from a type string, removing modifiers like const, *, & and template arguments
    /// </summary>
    /// <param name="typeString">The type string to clean</param>
    /// <returns>A clean base name without modifiers and template arguments</returns>
    public static string ExtractBaseTypeName(string typeString)
    {
        // Remove leading/trailing whitespace
        var cleanType = typeString.Trim();

        // Remove modifiers like const, *, & from the end
        while (cleanType.EndsWith("*") || cleanType.EndsWith("&"))
        {
            cleanType = cleanType.Substring(0, cleanType.Length - 1).Trim();
        }

        // Remove "const" if it's at the beginning
        if (cleanType.StartsWith("const "))
        {
            cleanType = cleanType.Substring(6).Trim();
        }

        // Extract base name from template arguments if present
        // Find the first '<' character that starts template arguments
        int templateStart = cleanType.IndexOf('<');
        if (templateStart > 0)
        {
            // Extract the base name before the template arguments
            cleanType = cleanType.Substring(0, templateStart).Trim();
        }

        // Extract the base name after the last namespace separator (::)
        // This handles cases like "NS::SomeTemplate" -> "SomeTemplate"
        int lastNamespaceSeparator = cleanType.LastIndexOf("::");
        if (lastNamespaceSeparator >= 0 && lastNamespaceSeparator < cleanType.Length - 2)
        {
            cleanType = cleanType.Substring(lastNamespaceSeparator + 2);
        }

        return cleanType;
    }

    /// <summary>
    /// Extracts bit field information from a declaration string
    /// </summary>
    /// <param name="declaration">The declaration string (e.g., "unsigned int x : 4;" or "int flags : 1;")</param>
    /// <returns>A tuple with (IsBitField, BitFieldWidth) where BitFieldWidth is the number of bits</returns>
    public static (bool IsBitField, int? BitFieldWidth) ExtractBitFieldInfo(string declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration))
            return (false, null);

        // Strip comments first
        string cleanDeclaration = StripComments(declaration);

        // Match bit field syntax: "name : width" where width is a number
        // The pattern looks for a colon followed by a number, possibly with whitespace
        // Must not be inside parentheses (to avoid matching C++ scope resolution or ternary operators)
        // Pattern: identifier followed by optional whitespace, colon, optional whitespace, digits, then semicolon or end
        var match = Regex.Match(cleanDeclaration, @"\w+\s*:\s*(\d+)\s*[;]?\s*$");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int width))
            {
                return (true, width);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Removes the bit field syntax from a declaration, returning the clean declaration
    /// </summary>
    /// <param name="declaration">The declaration string (e.g., "unsigned int x : 4;" or "unsigned int x : 4")</param>
    /// <returns>The declaration without the bit field syntax (e.g., "unsigned int x;" or "unsigned int x")</returns>
    public static string RemoveBitFieldSyntax(string declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration))
            return declaration;

        // Match and remove the " : width" part, preserving the rest
        // Handle both with and without semicolon
        return Regex.Replace(declaration, @"(\w+)\s*:\s*\d+(\s*;?)$", "$1$2");
    }

    /// <summary>
    /// Extracts alignment information from a declaration string
    /// </summary>
    /// <param name="declaration">The declaration string (e.g., "int x __declspec(align(8))" or "void (*func)() __declspec(align(16))")</param>
    /// <returns>A tuple with (HasAlignment, Alignment) where Alignment is the alignment value in bytes</returns>
    public static (bool HasAlignment, int? Alignment) ExtractAlignmentInfo(string declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration))
            return (false, null);

        // Match __declspec(align(N)) pattern
        var match = Regex.Match(declaration, @"__declspec\s*\(\s*align\s*\(\s*(\d+)\s*\)\s*\)");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int alignment))
            {
                return (true, alignment);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Removes the __declspec(align(N)) syntax from a declaration, returning the clean declaration
    /// </summary>
    /// <param name="declaration">The declaration string (e.g., "int x __declspec(align(8))")</param>
    /// <returns>The declaration without the alignment syntax (e.g., "int x")</returns>
    public static string RemoveAlignmentSyntax(string declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration))
            return declaration;

        // Match and remove __declspec(align(N)) pattern
        return Regex.Replace(declaration, @"\s*__declspec\s*\(\s*align\s*\(\s*\d+\s*\)\s*\)", "").Trim();
    }

    /// <summary>
    /// Extracts array information from a declaration string
    /// </summary>
    /// <param name="declaration">The declaration string (e.g., "char name[32]" or "int arr[]")</param>
    /// <returns>A tuple with (IsArray, ArraySize) where ArraySize is null for unsized arrays</returns>
    public static (bool IsArray, int? ArraySize) ExtractArrayInfo(string declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration))
            return (false, null);

        // Strip comments first
        string cleanDeclaration = StripComments(declaration);

        // Match array brackets with optional size
        var match = Regex.Match(cleanDeclaration, @"\[(\d*)\]");
        if (match.Success)
        {
            string sizeStr = match.Groups[1].Value;
            if (string.IsNullOrEmpty(sizeStr))
            {
                // Unsized/flexible array like "char Format[]"
                return (true, null);
            }
            else if (int.TryParse(sizeStr, out int size))
            {
                return (true, size);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Checks if a parameter string represents a function pointer type.
    /// Function pointer parameters look like: HRESULT (__cdecl *)(const unsigned __int16 *)
    /// </summary>
    /// <param name="paramString">The parameter string to check</param>
    /// <returns>True if this is a function pointer type parameter</returns>
    public static bool IsFunctionPointerParameter(string paramString)
    {
        if (string.IsNullOrWhiteSpace(paramString))
            return false;

        var trimmed = paramString.Trim();

        // Function pointer parameters have the pattern: ReturnType (CallingConvention *)(Params)
        // Key indicators:
        // 1. Contains "(*)" or "(CallingConvention *)" pattern
        // 2. Has at least two pairs of parentheses
        // 3. The second pair of parentheses contains the function parameters

        // Check for the pointer-in-parens pattern followed by a parameter list
        var pattern = @"\(\s*(__thiscall|__stdcall|__cdecl|__fastcall)?\s*\*\s*\)\s*\(";
        return Regex.IsMatch(trimmed, pattern);
    }

    /// <summary>
    /// Extracts function pointer information from a parameter string.
    /// Handles parameters like: HRESULT (__cdecl *)(const unsigned __int16 *)
    /// </summary>
    /// <param name="paramString">The function pointer parameter string</param>
    /// <returns>Tuple with (ReturnType, CallingConvention, Parameters) or nulls if not a function pointer</returns>
    public static (string? ReturnType, string? CallingConvention, string? Parameters)
        ExtractFunctionPointerParameterInfo(string paramString)
    {
        if (!IsFunctionPointerParameter(paramString))
            return (null, null, null);

        var trimmed = paramString.Trim();

        // Pattern to match function pointer: ReturnType (CallingConvention *)(Params)
        // Groups: 1=ReturnType, 2=CallingConvention (optional), 3=Parameters
        var pattern = @"^(.+?)\s*\(\s*(__thiscall|__stdcall|__cdecl|__fastcall)?\s*\*\s*\)\s*\((.*)?\)$";
        var match = Regex.Match(trimmed, pattern);

        if (match.Success)
        {
            string returnType = match.Groups[1].Value.Trim();
            string callingConvention = match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty;
            string parameters = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty;

            return (returnType, callingConvention, parameters);
        }

        return (null, null, null);
    }

    /// <summary>
    /// Checks if a function pointer has a function pointer return type.
    /// These look like: HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))(IKeystoneWindow *, unsigned int, unsigned int, int)
    /// </summary>
    /// <param name="declaration">The full function pointer declaration</param>
    /// <returns>True if the return type is a function pointer</returns>
    public static bool HasFunctionPointerReturnType(string declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration))
            return false;

        var trimmed = declaration.Trim();

        // Pattern: ReturnType (CallingConvention *(CallingConvention *FuncName)(Params))(ReturnFuncParams)
        // The key is: the return type itself contains a function pointer pattern before the main function pointer

        // Count parentheses pairs - if we have more than 2 pairs of balanced parens, it's likely a return function pointer
        int openParen = 0;
        int closeParen = 0;
        int parenPairs = 0;

        for (int i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '(')
            {
                openParen++;
            }
            else if (trimmed[i] == ')')
            {
                closeParen++;
                if (closeParen <= openParen)
                {
                    parenPairs++;
                }
            }
        }

        // Function pointers with function pointer returns have 3+ paren pairs
        // Regular function pointers have 2 paren pairs
        if (parenPairs < 3)
            return false;

        // More specific pattern check: Look for nested pointer declarations
        // Pattern like: Type (__cc *(__cc *Name)(Params))(ReturnParams)
        var pattern =
            @"\(\s*(__thiscall|__stdcall|__cdecl|__fastcall)?\s*\*\s*\(\s*(__thiscall|__stdcall|__cdecl|__fastcall)?\s*\*";
        return Regex.IsMatch(trimmed, pattern);
    }

    /// <summary>
    /// Extracts information from a function pointer that returns a function pointer.
    /// Input format: HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))(IKeystoneWindow *, unsigned int, unsigned int, int)
    /// </summary>
    /// <param name="declaration">The full declaration</param>
    /// <returns>Tuple with (OuterReturnType, OuterCallingConvention, InnerCallingConvention, FunctionName, InnerParams, OuterParams) or nulls</returns>
    public static (string? OuterReturnType, string? OuterCallingConvention, string? InnerCallingConvention, string?
        FunctionName, string? InnerParams, string? OuterParams)
        ExtractNestedFunctionPointerInfo(string declaration)
    {
        if (!HasFunctionPointerReturnType(declaration))
            return (null, null, null, null, null, null);

        var trimmed = declaration.Trim();

        // The structure is:
        // ReturnType (OuterCC *(InnerCC *FuncName)(InnerParams))(OuterParams)
        //
        // We need to find the balanced parens groups from the end

        // First, find the outermost parameter list (OuterParams) - the last balanced ()
        int lastCloseParen = trimmed.LastIndexOf(')');
        if (lastCloseParen < 0)
            return (null, null, null, null, null, null);

        int depth = 0;
        int outerParamOpen = -1;
        for (int i = lastCloseParen; i >= 0; i--)
        {
            if (trimmed[i] == ')')
                depth++;
            else if (trimmed[i] == '(')
            {
                depth--;
                if (depth == 0)
                {
                    outerParamOpen = i;
                    break;
                }
            }
        }

        if (outerParamOpen < 0)
            return (null, null, null, null, null, null);

        string outerParams = trimmed.Substring(outerParamOpen + 1, lastCloseParen - outerParamOpen - 1).Trim();
        string beforeOuterParams = trimmed.Substring(0, outerParamOpen).Trim();

        // beforeOuterParams is now: HRESULT (__cdecl *(__thiscall *GetWndProc)(IKeystoneDocument *this))
        // We need to strip the outer ) and find the second-to-last balanced paren group (InnerParams)

        if (!beforeOuterParams.EndsWith(")"))
            return (null, null, null, null, null, null);

        // Strip the trailing )
        string withoutTrailingParen = beforeOuterParams.Substring(0, beforeOuterParams.Length - 1).Trim();

        // Now find the InnerParams - the last balanced () in this string
        int innerLastCloseParen = withoutTrailingParen.LastIndexOf(')');
        if (innerLastCloseParen < 0)
            return (null, null, null, null, null, null);

        depth = 0;
        int innerParamOpen = -1;
        for (int i = innerLastCloseParen; i >= 0; i--)
        {
            if (withoutTrailingParen[i] == ')')
                depth++;
            else if (withoutTrailingParen[i] == '(')
            {
                depth--;
                if (depth == 0)
                {
                    innerParamOpen = i;
                    break;
                }
            }
        }

        if (innerParamOpen < 0)
            return (null, null, null, null, null, null);

        string innerParams = withoutTrailingParen
            .Substring(innerParamOpen + 1, innerLastCloseParen - innerParamOpen - 1).Trim();
        string beforeInnerParams = withoutTrailingParen.Substring(0, innerParamOpen).Trim();

        // beforeInnerParams is now: HRESULT (__cdecl *(__thiscall *GetWndProc
        // We need to extract the function name and calling conventions

        // Find the first ( to split the return type from the pointer section
        int firstParen = beforeInnerParams.IndexOf('(');
        if (firstParen < 0)
            return (null, null, null, null, null, null);

        string outerReturnType = beforeInnerParams.Substring(0, firstParen).Trim();
        string pointerSection = beforeInnerParams.Substring(firstParen + 1).Trim();

        // pointerSection is: __cdecl *(__thiscall *GetWndProc
        // Extract outer calling convention
        var outerCcMatch = Regex.Match(pointerSection, @"^(__thiscall|__stdcall|__cdecl|__fastcall)");
        string outerCallingConvention = outerCcMatch.Success ? outerCcMatch.Value : string.Empty;

        // Skip past outer calling convention and *
        string afterOuterCc = pointerSection;
        if (outerCcMatch.Success)
        {
            afterOuterCc = pointerSection.Substring(outerCcMatch.Length).Trim();
        }

        // afterOuterCc should start with *( or just *
        if (!afterOuterCc.StartsWith("*"))
            return (null, null, null, null, null, null);

        afterOuterCc = afterOuterCc.Substring(1).Trim();

        // afterOuterCc is: (__thiscall *GetWndProc
        // Find the inner pointer section
        if (!afterOuterCc.StartsWith("("))
            return (null, null, null, null, null, null);

        afterOuterCc = afterOuterCc.Substring(1).Trim();

        // afterOuterCc is: __thiscall *GetWndProc
        // Extract inner calling convention
        var innerCcMatch = Regex.Match(afterOuterCc, @"^(__thiscall|__stdcall|__cdecl|__fastcall)");
        string innerCallingConvention = innerCcMatch.Success ? innerCcMatch.Value : string.Empty;

        // Extract function name
        string funcNamePart = afterOuterCc;
        if (innerCcMatch.Success)
        {
            funcNamePart = afterOuterCc.Substring(innerCcMatch.Length).Trim();
        }

        // funcNamePart is: *GetWndProc
        // Remove * and get the name
        string functionName = Regex.Replace(funcNamePart, @"^\*\s*", "").Trim();

        // Remove any trailing ) that might be left from the inner paren group
        functionName = functionName.TrimEnd(')');

        return (outerReturnType, outerCallingConvention, innerCallingConvention, functionName, innerParams,
            outerParams);
    }

    /// <summary>
    /// Generates a normalized function signature string for consistent storage and lookup.
    /// Format: ReturnType [CallingConv] Name(ParamType1,ParamType2)
    /// </summary>
    public static string GenerateNormalizedFunctionSignature(string returnType, string callingConvention, string name,
        List<FunctionParamModel>? parameters)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(NormalizeTypeString(returnType));
        sb.Append(" ");

        if (!string.IsNullOrEmpty(callingConvention))
        {
            sb.Append(callingConvention);
            sb.Append(" ");
        }

        sb.Append(name);
        sb.Append("(");

        if (parameters != null && parameters.Any())
        {
            var pList = parameters.OrderBy(p => p.Position).ToList();
            for (int i = 0; i < pList.Count; i++)
            {
                var p = pList[i];
                sb.Append(NormalizeTypeString(p.ParameterType ?? ""));

                if (i < pList.Count - 1)
                {
                    sb.Append(",");
                }
            }
        }

        sb.Append(")");
        return sb.ToString();
    }
}
