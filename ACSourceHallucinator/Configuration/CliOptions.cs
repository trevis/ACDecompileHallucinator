namespace ACSourceHallucinator.Configuration;

public class CliOptions
{
    // Database paths
    public required string SourceDbPath { get; init; }                      // Existing type database
    public string HallucinatorDbPath { get; init; } = "out/hallucinator.db";    // Results + cache

    // LLM configuration
    public string LlmBaseUrl { get; init; } = "http://localhost:1234/v1";
    public required string LlmModel { get; init; }
    public int MaxTokens { get; init; } = 131072;
    public double Temperature { get; init; } = 0.3;
    public int TimeoutMinutes { get; init; } = 15;

    // Pipeline control
    public int MaxRetries { get; init; } = 10;
    public bool SkipCache { get; init; }
    public bool ForceRegeneration { get; init; }
    // Debug mode
    public string? DebugStructFqn { get; init; }
}
