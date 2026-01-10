using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ACSourceHallucinator.Tui;

public class PipelineTui
{
    private readonly IAnsiConsole _console;

    // UI State
    private PipelineStats _stats = new();
    private string _currentStageName = "Initializing...";
    private DateTime _stageStartTime;
    private int _totalItems;
    private int _pendingItems;
    private int _currentItemIndex;
    private string _currentItemName = string.Empty;
    private readonly Queue<StageProgressEvent> _eventLog = new();
    private const int MaxLogEntries = 12;

    public PipelineTui(IAnsiConsole console)
    {
        _console = console;
    }

    /// <summary>
    /// Runs the pipeline action within a Live rendering context.
    /// </summary>
    public async Task RunWrappedAsync(Func<Task> action)
    {
        _console.Clear();

        await _console.Live(GetRenderable())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .StartAsync(async ctx =>
            {
                // Refresh loop
                var refreshCts = new CancellationTokenSource();
                var refreshTask = Task.Run(async () =>
                {
                    while (!refreshCts.Token.IsCancellationRequested)
                    {
                        ctx.UpdateTarget(GetRenderable());
                        try
                        {
                            await Task.Delay(100, refreshCts.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }, refreshCts.Token);

                try
                {
                    await action();
                }
                finally
                {
                    refreshCts.Cancel();
                    try
                    {
                        await refreshTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    ctx.UpdateTarget(GetRenderable());
                }
            });
    }

    public void SetCurrentStage(string stageName)
    {
        _currentStageName = stageName;
        _stageStartTime = DateTime.UtcNow;
        // Reset stage-specific counters
        _totalItems = 0;
        _pendingItems = 0;
        _currentItemIndex = 0;
        _currentItemName = "Waiting for work items...";
    }

    public void SetTotalItems(int total, int pending)
    {
        _totalItems = total;
        _pendingItems = pending;
    }

    public void UpdateProgress(int current, string itemName)
    {
        _currentItemIndex = current;
        _currentItemName = itemName;
    }

    public void UpdateStats(PipelineStats stats)
    {
        _stats = stats;
    }

    public void LogEvent(StageProgressEvent evt)
    {
        _eventLog.Enqueue(evt);
        if (_eventLog.Count > MaxLogEntries)
        {
            _eventLog.Dequeue();
        }
    }

    public void DisplayFinalStats(PipelineStats stats)
    {
        // The Live view might disappear or stay. We can print a final summary below it.
        _console.WriteLine();
        _console.MarkupLine("[bold green]Pipeline Completed![/]");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Total Processed", stats.TotalProcessed.ToString());
        table.AddRow("Successful", $"[green]{stats.Successful}[/]");
        table.AddRow("Failed", $"[red]{stats.Failed}[/]");
        table.AddRow("Retries", $"[yellow]{stats.TotalRetries}[/]");
        table.AddRow("Cache Hits", $"[blue]{stats.CacheHits}[/]");
        table.AddRow("Cache Hit Rate", $"{stats.CacheHitRate:P1}");
        table.AddRow("Total LLM Time", stats.TotalLlmTime.ToString(@"hh\:mm\:ss"));
        table.AddRow("Avg Response Time", $"{stats.AverageResponseTime.TotalSeconds:F1}s");
        table.AddRow("Total Tokens", $"{stats.TotalPromptTokens + stats.TotalCompletionTokens:N0}");

        _console.Write(table);
    }

    private IRenderable GetRenderable()
    {
        // 1. Header
        var header = new Panel($"[bold blue]Stage:[/] {_currentStageName}")
            .Border(BoxBorder.None)
            .Expand();

        // 2. Stats Table
        var statsTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("Statistics")
            .AddColumn("Metric")
            .AddColumn("Value");

        statsTable.AddRow("Processed", $"{_currentItemIndex}/{_pendingItems} (Skipped: {_totalItems - _pendingItems})");
        statsTable.AddRow("Success", $"[green]{_stats.Successful}[/]");
        statsTable.AddRow("Failed", $"[red]{_stats.Failed}[/]");
        statsTable.AddRow("Retries", $"[yellow]{_stats.TotalRetries}[/]");
        statsTable.AddRow("Cache Rate", $"{_stats.CacheHitRate:P0}");
        statsTable.AddRow("Tokens", $"{(_stats.TotalPromptTokens + _stats.TotalCompletionTokens) / 1000.0:F1}k");

        // ETA Calculation
        var itemsDone = _currentItemIndex > 0 ? _currentItemIndex - 1 : 0;
        if (itemsDone > 0)
        {
            var elapsed = DateTime.UtcNow - _stageStartTime;
            var avgTimePerItem = elapsed / itemsDone;
            // pendingItems includes the one currently being processed.
            // So if currentItemIndex is 5 (processing 5th item), 4 are done.
            // Items remaining = _pendingItems - 4.
            // Wait, pendingItems is the total items to process in this stage.
            // So remaining = _pendingItems - itemsDone.
            var itemsLeft = _pendingItems - itemsDone;

            // ETA logic: avg * itemsLeft
            var eta = avgTimePerItem * itemsLeft;

            statsTable.AddRow("Avg Time/Item", $"{avgTimePerItem.TotalSeconds:F1}s");
            // Highlight ETA if it's long?
            statsTable.AddRow("ETA", $"[bold magenta]{eta:hh\\:mm\\:ss}[/]");
        }
        else
        {
            statsTable.AddRow("Avg Time/Item", "Calculating...");
            statsTable.AddRow("ETA", "Calculating...");
        }

        // 3. Event Log
        var logGrid = new Grid().Expand();
        logGrid.AddColumn();
        foreach (var evt in _eventLog)
        {
            var color = evt.Type switch
            {
                ProgressEventType.Error => "red",
                ProgressEventType.Warning => "yellow",
                ProgressEventType.Success => "green",
                _ => "grey"
            };
            logGrid.AddRow($"[{color}]{Markup.Escape(evt.Message)}[/]");
        }

        var logPanel = new Panel(logGrid)
            .Header("Event Log")
            .Border(BoxBorder.Rounded)
            .Expand();

        // 4. Progress Bar (simulated)
        var pct = _pendingItems > 0 ? (double)_currentItemIndex / _pendingItems : 0;
        var barWidth = 40;
        var filled = (int)(Math.Clamp(pct, 0, 1) * barWidth);
        var bar = $"[green]{new string('█', filled)}[/][grey]{new string('░', barWidth - filled)}[/] {pct:P0}";
        var progressPanel = new Panel(new Rows(
            new Markup($"[bold]Processing:[/] {Markup.Escape(_currentItemName)}"),
            new Markup(bar)
        )).Border(BoxBorder.Heavy).Expand();

        // Layout
        // Header
        // Split: Stats | Log
        // Footer: Progress

        var contentGrid = new Grid().Expand();
        contentGrid.AddColumn(new GridColumn().Width(36)); // Stats
        contentGrid.AddColumn(new GridColumn()); // Log
        contentGrid.AddRow(statsTable, logPanel);

        return new Rows(
            header,
            contentGrid,
            progressPanel
        );
    }
}
