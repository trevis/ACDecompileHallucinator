namespace ACDecompileParser.Shared.Lib.Utilities;

public interface IProgressReporter
{
    void Start(string taskName, int totalSteps);
    void Report(int stepsCompleted, string? message = null);
    void Finish(string? message = null);
}
