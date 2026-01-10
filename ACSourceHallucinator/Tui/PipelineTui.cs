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
    private const int MaxLogEntries = 200;
    private readonly object _lock = new();

    // Rendering Cache
    private int _lastWidth;
    private int _lastEventCountSnapshot;
    private List<(string Content, string Color)> _cachedWrappedLines = new();

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
            .StartAsync(async ctx =>
            {
                // Refresh loop
                var refreshCts = new CancellationTokenSource();
                var refreshTask = Task.Run(async () =>
                {
                    while (!refreshCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            ctx.UpdateTarget(GetRenderable());
                        }
                        catch
                        {
                            // Silent failure for TUI update
                        }

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

                    try
                    {
                        ctx.UpdateTarget(GetRenderable());
                    }
                    catch
                    {
                        // Silent failure for final TUI update
                    }
                }
            });
    }

    public void SetCurrentStage(string stageName)
    {
        lock (_lock)
        {
            _currentStageName = stageName;
            _stageStartTime = DateTime.UtcNow;
            _totalItems = 0;
            _pendingItems = 0;
            _currentItemIndex = 0;
            _currentItemName = "Waiting for work items...";
            _eventLog.Clear(); // Clear log when starting new stage
            _lastEventCountSnapshot = -1; // Invalidate cache
        }
    }

    public void SetTotalItems(int total, int pending)
    {
        lock (_lock)
        {
            _totalItems = total;
            _pendingItems = pending;
        }
    }

    public void UpdateProgress(int current, string itemName)
    {
        lock (_lock)
        {
            _currentItemIndex = current;
            _currentItemName = itemName;
        }
    }

    public void UpdateStats(PipelineStats stats)
    {
        lock (_lock)
        {
            _stats = stats;
        }
    }

    public void LogEvent(StageProgressEvent evt)
    {
        lock (_lock)
        {
            _eventLog.Enqueue(evt);
            if (_eventLog.Count > MaxLogEntries)
            {
                _eventLog.Dequeue();
            }
        }
    }

    public void DisplayFinalStats(PipelineStats stats)
    {
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
        table.AddRow("Total Prompt Tokens", $"{stats.TotalPromptTokens:N0}");
        table.AddRow("Total Completion Tokens", $"{stats.TotalCompletionTokens:N0}");

        _console.Write(table);
    }

    private IRenderable GetRenderable()
    {
        try
        {
            // Take a snapshot of everything needed for rendering
            PipelineStats stats;
            string stageName;
            DateTime stageStartTime;
            int totalItems, pendingItems, currentItemIndex;
            string currentItemName;
            List<StageProgressEvent> logSnapshot;

            lock (_lock)
            {
                stats = _stats;
                stageName = _currentStageName;
                stageStartTime = _stageStartTime;
                totalItems = _totalItems;
                pendingItems = _pendingItems;
                currentItemIndex = _currentItemIndex;
                currentItemName = _currentItemName;
                logSnapshot = _eventLog.ToList();
            }

            var totalWidth = Math.Max(80, _console.Profile.Width);
            var totalHeight = Math.Max(24, _console.Profile.Height);

            // 1. Header
            var header = new Panel(
                    Align.Center(
                        new Markup(
                            $"[bold blue]ACHallucinator Pipeline[/] - [bold yellow]Stage:[/] {Markup.Escape(stageName)}"),
                        VerticalAlignment.Middle))
                .Border(BoxBorder.Rounded)
                .Expand();

            // 2. Stats Table
            var statsTable = new Table()
                .Border(TableBorder.Rounded)
                .Title("Statistics")
                .AddColumn("Metric")
                .AddColumn("Value")
                .Expand();

            statsTable.AddRow("Processed", $"{currentItemIndex}/{pendingItems} (Skipped: {totalItems - pendingItems})");
            statsTable.AddRow("Success", $"[green]{stats.Successful}[/]");
            statsTable.AddRow("Failed", $"[red]{stats.Failed}[/]");
            statsTable.AddRow("Retries", $"[yellow]{stats.TotalRetries}[/]");
            statsTable.AddRow("Cache Rate", $"{stats.CacheHitRate:P0}");
            statsTable.AddRow("Prompt Tokens", stats.TotalPromptTokens < 10000
                ? $"{stats.TotalPromptTokens:N0}"
                : $"{stats.TotalPromptTokens / 1000.0:F1}k");
            statsTable.AddRow("Compl. Tokens", stats.TotalCompletionTokens < 10000
                ? $"{stats.TotalCompletionTokens:N0}"
                : $"{stats.TotalCompletionTokens / 1000.0:F1}k");

            var itemsDone = currentItemIndex > 0 ? currentItemIndex - 1 : 0;
            if (itemsDone > 0)
            {
                var elapsed = DateTime.UtcNow - stageStartTime;
                var avgTimePerItem = elapsed / itemsDone;
                var itemsLeft = pendingItems - itemsDone;
                var eta = avgTimePerItem * itemsLeft;

                statsTable.AddRow("Avg Time/Item", $"{avgTimePerItem.TotalSeconds:F1}s");
                statsTable.AddRow("ETA", $"[bold magenta]{eta:hh\\:mm\\:ss}[/]");
            }
            else
            {
                statsTable.AddRow("Avg Time/Item", "Calculating...");
                statsTable.AddRow("ETA", "Calculating...");
            }

            // 3. Event Log
            var logHeight = Math.Max(5, totalHeight - 3 - 5 - 2);
            var availableLogWidth = Math.Max(10, totalWidth - 44 - 4);

            if (_lastWidth != availableLogWidth || _lastEventCountSnapshot != logSnapshot.Count)
            {
                _cachedWrappedLines = WrapLog(logSnapshot, availableLogWidth);
                _lastWidth = availableLogWidth;
                _lastEventCountSnapshot = logSnapshot.Count;
            }

            var logGrid = new Grid().Expand().AddColumn();
            var linesToShow = _cachedWrappedLines.Skip(Math.Max(0, _cachedWrappedLines.Count - logHeight))
                .Take(logHeight).ToList();

            foreach (var line in linesToShow)
            {
                logGrid.AddRow($"[{line.Color}]{line.Content}[/]");
            }

            for (int i = linesToShow.Count; i < logHeight; i++)
            {
                logGrid.AddRow("");
            }

            var logPanel = new Panel(logGrid)
                .Header("Event Log")
                .Border(BoxBorder.Rounded)
                .Expand();

            // 4. Progress Bar
            var pct = pendingItems > 0 ? (double)currentItemIndex / pendingItems : 0;
            var barWidth = Math.Max(20, totalWidth - 60);
            var filled = (int)(Math.Clamp(pct, 0, 1) * barWidth);
            var bar = $"[green]{new string('█', filled)}[/][grey]{new string('░', barWidth - filled)}[/] {pct:P0}";

            var progressPanel = new Panel(new Rows(
                new Markup($"[bold]Processing:[/] {Markup.Escape(currentItemName)}"),
                new Markup(bar)
            )).Border(BoxBorder.Rounded).Expand();

            // 5. Final Layout
            return new Layout("Root")
                .SplitRows(
                    new Layout("Top").Update(header).Size(3),
                    new Layout("Main").SplitColumns(
                        new Layout("Stats").Update(statsTable).Size(44),
                        new Layout("Log").Update(logPanel)
                    ),
                    new Layout("Bottom").Update(progressPanel).Size(5)
                );
        }
        catch (Exception ex)
        {
            return new Markup($"[red]UI Error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private List<(string Content, string Color)> WrapLog(List<StageProgressEvent> events, int width)
    {
        var allLines = new List<(string Content, string Color)>();
        foreach (var evt in events)
        {
            var color = evt.Type switch
            {
                ProgressEventType.Error => "red",
                ProgressEventType.Warning => "yellow",
                ProgressEventType.Success => "green",
                ProgressEventType.GeneratedContent => "cyan",
                _ => "grey"
            };

            var lines = evt.Message.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                var escapedLine = Markup.Escape(rawLine);
                if (string.IsNullOrEmpty(escapedLine))
                {
                    allLines.Add(("", color));
                    continue;
                }

                var words = escapedLine.Split(' ');
                var currentLine = new System.Text.StringBuilder();

                foreach (var word in words)
                {
                    if (currentLine.Length + word.Length + 1 > width)
                    {
                        if (currentLine.Length > 0)
                        {
                            allLines.Add((currentLine.ToString(), color));
                            currentLine.Clear();
                        }

                        var remainingWord = word;
                        while (remainingWord.Length > width)
                        {
                            if (width <= 0) break;
                            allLines.Add((remainingWord.Substring(0, width), color));
                            remainingWord = remainingWord.Substring(width);
                        }

                        currentLine.Append(remainingWord);
                    }
                    else
                    {
                        if (currentLine.Length > 0) currentLine.Append(' ');
                        currentLine.Append(word);
                    }
                }

                if (currentLine.Length > 0)
                {
                    allLines.Add((currentLine.ToString(), color));
                }
            }
        }

        return allLines;
    }
}
