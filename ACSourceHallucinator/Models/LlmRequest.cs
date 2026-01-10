namespace ACSourceHallucinator.Models;

public record LlmRequest
{
    public required string Prompt { get; init; }
    public required string Model { get; init; }
    public int MaxTokens { get; init; } = 2048;
    public double Temperature { get; init; } = 0.7;
}
