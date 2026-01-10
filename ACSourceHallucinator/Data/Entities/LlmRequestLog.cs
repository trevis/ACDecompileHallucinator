using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ACSourceHallucinator.Models;

namespace ACSourceHallucinator.Data.Entities;

public class LlmRequestLog
{
    [Key] public int Id { get; set; }

    public int StageResultId { get; set; }

    [ForeignKey(nameof(StageResultId))] public virtual StageResult StageResult { get; set; } = null!;

    public int Attempt { get; set; }

    public required string Prompt { get; set; }
    public required string Response { get; set; }
    public required string Model { get; set; }

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int DurationMs { get; set; }

    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }

    public DateTime Timestamp { get; set; }
}
