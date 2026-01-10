using ACSourceHallucinator.Models;

namespace ACSourceHallucinator.Interfaces;

public interface IStage
{
    string Name { get; }
    IReadOnlyList<string> Dependencies { get; } // Names of stages that must complete first

    Task<IReadOnlyList<WorkItem>> CollectWorkItemsAsync(
        string? debugFilterFqn = null,
        CancellationToken ct = default);

    Task<StageResult> ProcessWorkItemAsync(
        WorkItem item,
        CancellationToken ct = default);

    event EventHandler<StageProgressEvent> ProgressUpdated;
}

public class StageProgressEvent : EventArgs
{
    public required string Message { get; init; }
    public ProgressEventType Type { get; init; } = ProgressEventType.Info;
}

public enum ProgressEventType
{
    Info,
    Warning,
    Error,
    Success,
    GeneratedContent
}
