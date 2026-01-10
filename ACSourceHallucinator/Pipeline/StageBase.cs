using System.Text.Json;
using System.Text.Json.Serialization;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Enums;
using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Models;

namespace ACSourceHallucinator.Pipeline;

public abstract class StageBase : IStage
{
    protected readonly ILlmClient LlmClient;
    protected readonly IReferenceTextGenerator ReferenceGenerator;
    protected readonly IStageResultRepository ResultRepo;
    protected readonly PipelineOptions Options;

    protected StageBase(
        ILlmClient llmClient,
        IReferenceTextGenerator referenceGenerator,
        IStageResultRepository resultRepo,
        PipelineOptions options)
    {
        LlmClient = llmClient;
        ReferenceGenerator = referenceGenerator;
        ResultRepo = resultRepo;
        Options = options;
    }

    public abstract string Name { get; }
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public abstract Task<IReadOnlyList<WorkItem>> CollectWorkItemsAsync(
        string? debugFilterFqn = null,
        CancellationToken ct = default);

    public async Task<StageResult> ProcessWorkItemAsync(WorkItem item, CancellationToken ct)
    {
        var result = new StageResult
        {
            StageName = Name,
            EntityType = item.EntityType,
            EntityId = item.EntityId,
            FullyQualifiedName = item.FullyQualifiedName,
            Status = StageResultStatus.Pending,
            IsCacheHit = true, // Assume cached until proven otherwise
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        string? previousFailureReason = null;
        string? previousResponse = null;

        for (int attempt = 0; attempt < Options.MaxRetries; attempt++)
        {
            result.RetryCount = attempt;

            // 1. Build and send generation prompt (async)
            var prompt = await BuildPromptAsync(item, previousFailureReason, previousResponse, ct);
            var response = await LlmClient.SendRequestAsync(
                new LlmRequest { Prompt = prompt, Model = Options.Model }, ct);

            // Accumulate stats
            result.PromptTokens += response.PromptTokens;
            result.CompletionTokens += response.CompletionTokens;
            if (response.FromCache)
            {
                // result.IsCacheHit remains true if it was true
            }
            else
            {
                result.IsCacheHit = false;
                result.TotalLlmTime += response.ResponseTime;
            }

            var sanitizedContent = SanitizeLlmResponse(response.Content);

            // 2. Verify response format
            var formatVerification = VerifyResponseFormat(sanitizedContent);
            if (!formatVerification.IsValid)
            {
                previousFailureReason = $"Format validation failed: {formatVerification.ErrorMessage}";
                previousResponse = sanitizedContent;
                OnProgressUpdated(
                    $"Item {item.FullyQualifiedName} failed format validation: {formatVerification.ErrorMessage}. Retrying...",
                    ProgressEventType.Warning);
                continue;
            }

            // 3. Optional LLM verification (if stage implements it)
            if (RequiresLlmVerification)
            {
                var llmVerifyResult = await RunLlmVerificationAsync(item, sanitizedContent, result, ct);
                if (!llmVerifyResult.IsValid)
                {
                    previousFailureReason = $"LLM verification failed: {llmVerifyResult.Reason}";
                    previousResponse = sanitizedContent;
                    OnProgressUpdated(
                        $"Item {item.FullyQualifiedName} failed LLM verification: {llmVerifyResult.Reason}. Retrying...",
                        ProgressEventType.Warning);
                    continue;
                }
            }

            // Success!
            result.Status = StageResultStatus.Success;
            result.GeneratedContent = sanitizedContent;
            result.UpdatedAt = DateTime.UtcNow;
            OnProgressUpdated($"Successfully processed {item.FullyQualifiedName}", ProgressEventType.Success);
            return result;
        }

        // Exhausted retries
        result.Status = StageResultStatus.Failed;
        result.LastFailureReason = previousFailureReason;
        result.UpdatedAt = DateTime.UtcNow;
        OnProgressUpdated($"Failed to process {item.FullyQualifiedName} after {Options.MaxRetries} attempts",
            ProgressEventType.Error);
        return result;
    }

    public event EventHandler<StageProgressEvent>? ProgressUpdated;

    protected void OnProgressUpdated(string message, ProgressEventType type = ProgressEventType.Info)
    {
        ProgressUpdated?.Invoke(this, new StageProgressEvent { Message = message, Type = type });
    }

    // --- Abstract/Virtual methods for stage customization ---

    /// <summary>
    /// Builds the generation prompt. Async to allow database queries and reference generation.
    /// </summary>
    protected abstract Task<string> BuildPromptAsync(
        WorkItem item,
        string? previousFailureReason,
        string? previousResponse,
        CancellationToken ct);

    /// <summary>
    /// Validates the LLM response format. Synchronous as it should only do simple checks.
    /// </summary>
    protected abstract VerificationResult VerifyResponseFormat(string response);

    /// <summary>
    /// Whether this stage requires LLM-based verification after format verification passes.
    /// </summary>
    protected virtual bool RequiresLlmVerification => true;

    /// <summary>
    /// Builds the LLM verification prompt. Async to allow database queries and reference generation.
    /// </summary>
    protected virtual Task<string> BuildLlmVerificationPromptAsync(
        WorkItem item,
        string generatedContent,
        CancellationToken ct)
    {
        throw new NotImplementedException("Override if RequiresLlmVerification is true");
    }

    /// <summary>
    /// Parses the LLM verification response. Synchronous as it only parses JSON.
    /// </summary>
    protected virtual VerificationResult ParseLlmVerificationResponse(string response)
    {
        // Default JSON parsing for { "valid": bool, "reason": string }
        try
        {
            var json = JsonSerializer.Deserialize<LlmVerificationJson>(response);
            return new VerificationResult
            {
                IsValid = json?.Valid ?? false,
                Reason = json?.Reason
            };
        }
        catch (JsonException ex)
        {
            return new VerificationResult
            {
                IsValid = false,
                Reason = $"JSON parse error: {ex.Message}",
                IsFormatError = true
            };
        }
    }

    private async Task<VerificationResult> RunLlmVerificationAsync(
        WorkItem item, string generatedContent, StageResult result, CancellationToken ct)
    {
        string? verifyFailureReason = null;

        for (int attempt = 0; attempt < Options.MaxRetries; attempt++)
        {
            var verifyPrompt = await BuildLlmVerificationPromptInternalAsync(
                item, generatedContent, verifyFailureReason, ct);
            var verifyResponse = await LlmClient.SendRequestAsync(
                new LlmRequest { Prompt = verifyPrompt, Model = Options.Model }, ct);

            // Accumulate stats
            result.PromptTokens += verifyResponse.PromptTokens;
            result.CompletionTokens += verifyResponse.CompletionTokens;
            if (verifyResponse.FromCache)
            {
                // result.IsCacheHit remains true if it was true
            }
            else
            {
                result.IsCacheHit = false;
                result.TotalLlmTime += verifyResponse.ResponseTime;
            }

            var sanitizedVerifyResponse = SanitizeLlmResponse(verifyResponse.Content);
            var parseResult = ParseLlmVerificationResponse(sanitizedVerifyResponse);

            if (parseResult.IsFormatError)
            {
                verifyFailureReason = parseResult.Reason;
                OnProgressUpdated($"Verification JSON parsing failed: {parseResult.Reason}. Retrying verification...",
                    ProgressEventType.Warning);
                continue; // Retry verification with format error feedback
            }

            return parseResult; // Valid JSON, return whether verification passed or failed
        }

        // Couldn't get valid JSON from verification after retries
        return new VerificationResult
        {
            IsValid = false,
            Reason = $"Verification failed to return valid JSON after {Options.MaxRetries} attempts"
        };
    }

    private async Task<string> BuildLlmVerificationPromptInternalAsync(
        WorkItem item, string generatedContent, string? previousError, CancellationToken ct)
    {
        var prompt = await BuildLlmVerificationPromptAsync(item, generatedContent, ct);

        if (previousError != null)
        {
            prompt +=
                $"\n\n=== PREVIOUS ATTEMPT ERROR ===\n{previousError}\n\nPlease ensure your response is valid JSON.";
        }

        return prompt;
    }

    protected string SanitizeLlmResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return response;

        var lines = response.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var filteredLines = new List<string>();
        bool inCodeBlock = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            filteredLines.Add(line);
        }

        return string.Join(Environment.NewLine, filteredLines).Trim();
    }
}

internal record LlmVerificationJson
{
    [JsonPropertyName("valid")] public bool Valid { get; init; }

    [JsonPropertyName("reason")] public string? Reason { get; init; }
}
