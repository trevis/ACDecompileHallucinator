using ACSourceHallucinator.Enums;

namespace ACSourceHallucinator.Models;

public class PipelineStats
{
    public int TotalProcessed { get; private set; }
    public int Successful { get; private set; }
    public int Failed { get; private set; }
    public int CacheHits { get; private set; }
    public TimeSpan TotalLlmTime { get; private set; }
    public int TotalPromptTokens { get; private set; }
    public int TotalCompletionTokens { get; private set; }

    public int TotalRetries { get; private set; }

    public double CacheHitRate => TotalProcessed > 0
        ? (double)CacheHits / TotalProcessed
        : 0;

    public TimeSpan AverageResponseTime => TotalProcessed - CacheHits > 0
        ? TotalLlmTime / (TotalProcessed - CacheHits)
        : TimeSpan.Zero;

    public void Record(StageResult result)
    {
        TotalProcessed++;
        TotalRetries += result.RetryCount;

        if (result.Status == StageResultStatus.Success)
            Successful++;
        else if (result.Status == StageResultStatus.Failed)
            Failed++;

        if (result.IsCacheHit)
            CacheHits++;
        else
            TotalLlmTime += result.TotalLlmTime;

        TotalPromptTokens += result.PromptTokens;
        TotalCompletionTokens += result.CompletionTokens;
    }
}
