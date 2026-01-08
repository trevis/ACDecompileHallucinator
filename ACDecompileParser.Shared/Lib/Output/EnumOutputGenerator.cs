using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output;

public class EnumOutputGenerator : TypeOutputGeneratorBase
{
    public EnumOutputGenerator(ITypeRepository? repository = null, ITypeTokenizationService? tokenizationService = null)
        : base(repository, tokenizationService)
    {
    }

    public override IEnumerable<CodeToken> Generate(TypeModel type)
    {
        // Output the reconstructed enum from database information
        foreach (var token in GenerateReconstructedTokens(type))
        {
            yield return token;
        }
    }

    private IEnumerable<CodeToken> GenerateReconstructedTokens(TypeModel type)
    {
        // Start the reconstructed enum
        yield return new CodeToken("// Reconstructed from database (WIP)", TokenType.Comment);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        yield return new CodeToken("enum", TokenType.Keyword);
        yield return new CodeToken(" ", TokenType.Whitespace);

        if (type.IsBitmask)
        {
            yield return new CodeToken("__bitmask", TokenType.Keyword);
            yield return new CodeToken(" ", TokenType.Whitespace);
        }

        // Include namespace inline in the enum name (e.g., AC1Modern::MyEnum)
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            yield return new CodeToken(type.Namespace + "::", TokenType.Identifier);
        }

        yield return new CodeToken(type.BaseName ?? string.Empty, TokenType.TypeName, type.Id.ToString());

        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        yield return new CodeToken("{", TokenType.Punctuation);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);

        // Get enum members from repository
        var members = Repository?.GetEnumMembers(type.Id) ?? new List<EnumMemberModel>();
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            yield return new CodeToken("  ", TokenType.Whitespace);
            yield return new CodeToken(member.Name, TokenType.Identifier);

            // Add value if present
            if (!string.IsNullOrEmpty(member.Value))
            {
                yield return new CodeToken(" = ", TokenType.Punctuation);
                yield return new CodeToken(member.Value, TokenType.NumberLiteral);
            }

            // Add comma for all but the last member
            if (i < members.Count - 1)
            {
                yield return new CodeToken(",", TokenType.Punctuation);
            }

            yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
        }

        yield return new CodeToken("};", TokenType.Punctuation);
        yield return new CodeToken(Environment.NewLine, TokenType.Whitespace);
    }
}
