namespace ACDecompileParser.Shared.Lib.Output.Models;

public enum TokenType
{
    Keyword,
    Identifier,
    Punctuation,
    Comment,
    Whitespace,
    TypeName,
    StringLiteral,
    NumberLiteral,
    Text
}

public class CodeToken
{
    public string Text { get; set; } = string.Empty;
    public TokenType Type { get; set; }
    public string? ReferenceId { get; set; }

    public CodeToken() { }

    public CodeToken(string text, TokenType type, string? referenceId = null)
    {
        Text = text;
        Type = type;
        ReferenceId = referenceId;
    }

    public override string ToString() => Text;
}
