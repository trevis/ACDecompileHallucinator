using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Models;
using ACSourceHallucinator.Tui;
using ACSourceHallucinator.Interfaces;

namespace ACSourceHallucinator.Pipeline;

public class PipelineRunner
{
    private readonly IStageResultRepository _resultRepo;
    private readonly PipelineTui _tui;
    private readonly PipelineStats _stats;

    public PipelineRunner(
        IStageResultRepository resultRepo,
        PipelineTui tui)
    {
        _resultRepo = resultRepo;
        _tui = tui;
        _stats = new PipelineStats();
    }

    public async Task RunAsync(Pipeline pipeline, PipelineOptions options, CancellationToken ct = default)
    {
        // Wrap execution in the TUI Live context
        await _tui.RunWrappedAsync(async () =>
        {
            foreach (var stage in pipeline.Stages)
            {
                await RunStageAsync(stage, options, ct);
            }
        });

        _tui.DisplayFinalStats(_stats);
    }

    private async Task RunStageAsync(IStage stage, PipelineOptions options, CancellationToken ct)
    {
        _tui.SetCurrentStage(stage.Name);

        // Subscribe to stage events
        stage.ProgressUpdated += OnStageProgress;

        try
        {
            // 1. Collect work items
            var allItems = await stage.CollectWorkItemsAsync(options.DebugFilterFqn, ct);

            // 2. Filter out already-completed items (resumability) unless force is requested
            var pendingItems = allItems;
            if (!options.ForceRegeneration)
            {
                var completedIds = await _resultRepo.GetCompletedEntityIdsAsync(stage.Name);
                pendingItems = allItems
                    .Where(item => !completedIds.Contains((item.EntityType, item.EntityId)))
                    .ToList();
            }

            _tui.SetTotalItems(allItems.Count, pendingItems.Count);

            // 3. Process each item
            for (int i = 0; i < pendingItems.Count; i++)
            {
                var item = pendingItems[i];
                _tui.UpdateProgress(i + 1, item.FullyQualifiedName);

                var result = await stage.ProcessWorkItemAsync(item, ct);
                await _resultRepo.SaveResultAsync(result);

                _stats.Record(result);
                _tui.UpdateStats(_stats);
            }
        }
        finally
        {
            stage.ProgressUpdated -= OnStageProgress;
        }
    }

    private void OnStageProgress(object? sender, StageProgressEvent e)
    {
        _tui.LogEvent(e);
    }
}
