using System.ComponentModel.DataAnnotations;

namespace ACSourceHallucinator.Data.Entities;

public class LlmCacheEntry
{
    public int Id { get; set; }
    public required string CacheKey { get; set; }        // SHA256 hash
    public required string PromptText { get; set; }      // For debugging
    public required string Model { get; set; }
    public double Temperature { get; set; }
    public required string ResponseContent { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
