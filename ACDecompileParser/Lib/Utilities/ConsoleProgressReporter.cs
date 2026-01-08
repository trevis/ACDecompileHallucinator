using ACDecompileParser.Shared.Lib.Utilities;
using System.Diagnostics;

namespace ACDecompileParser.Lib.Utilities;

public class ConsoleProgressReporter : IProgressReporter
{
    private string _currentTask = "";
    private int _totalSteps;
    private Stopwatch _stopwatch = new Stopwatch();
    private int _lastPercentage = -1;

    public void Start(string taskName, int totalSteps)
    {
        _currentTask = taskName;
        _totalSteps = totalSteps;
        _stopwatch.Restart();
        _lastPercentage = -1;
        Console.WriteLine($"{taskName}...");
    }

    public void Report(int stepsCompleted, string? message = null)
    {
        int percentage = (int)((double)stepsCompleted / _totalSteps * 100);
        
        // Don't spam the console if percentage hasn't changed, unless there is a specific message
        if (percentage == _lastPercentage && string.IsNullOrEmpty(message))
            return;
            
        _lastPercentage = percentage;
        
        var spinner = "|/-\\"[stepsCompleted % 4];
        var progressBar = new string('=', percentage / 2) + new string(' ', 50 - percentage / 2);
        var msg = message != null ? $" - {message}" : "";
        
        // Keep it on one line using \r
        Console.Write($"\r[{progressBar}] {percentage}% {spinner} {msg}");
    }

    public void Finish(string? message = null)
    {
        _stopwatch.Stop();
        // Clear the progress line and print done
        Console.Write($"\r[{new string('=', 50)}] 100% Done");
        Console.WriteLine(); // New line
        Console.WriteLine($"{_currentTask} completed in {_stopwatch.ElapsedMilliseconds}ms. {message ?? ""}");
    }
}
