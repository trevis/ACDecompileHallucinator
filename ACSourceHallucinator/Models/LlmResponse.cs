namespace ACSourceHallucinator.Models;

public record LlmResponse
{
    public required string Content { get; init; }
    public required int PromptTokens { get; init; }
    public required int CompletionTokens { get; init; }
    public required TimeSpan ResponseTime { get; init; }
    public bool FromCache { get; init; }
}
