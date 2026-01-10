using ACSourceHallucinator.Enums;

namespace ACSourceHallucinator.Models;

public class StageResult
{
    public int Id { get; set; }
    public required string StageName { get; set; }
    public required EntityType EntityType { get; set; }
    public required int EntityId { get; set; }
    public required string FullyQualifiedName { get; set; }

    public StageResultStatus Status { get; set; }
    public string? GeneratedContent { get; set; }
    public int RetryCount { get; set; }
    public string? LastFailureReason { get; set; }

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public TimeSpan TotalLlmTime { get; set; }
    public bool IsCacheHit { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
