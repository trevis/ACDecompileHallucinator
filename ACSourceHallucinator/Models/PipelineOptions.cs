namespace ACSourceHallucinator.Models;

public record PipelineOptions
{
    public string? DebugFilterFqn { get; init; }
    public bool SkipCache { get; init; }
    public int MaxRetries { get; init; } = 5;
    public required string Model { get; init; }
    public bool ForceRegeneration { get; init; }
}
