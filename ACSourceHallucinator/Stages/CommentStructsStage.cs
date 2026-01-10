using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Models;
using ACSourceHallucinator.Pipeline;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACSourceHallucinator.Stages;

public class CommentStructsStage : StageBase
{
    private readonly TypeContext _typeDb;

    public override string Name => "CommentStructs";

    public CommentStructsStage(
        TypeContext typeDb,
        ILlmClient llmClient,
        IReferenceTextGenerator referenceGenerator,
        IStageResultRepository resultRepo,
        PipelineOptions options)
        : base(llmClient, referenceGenerator, resultRepo, options)
    {
        _typeDb = typeDb;
    }

    public override Task<IReadOnlyList<WorkItem>> CollectWorkItemsAsync(
        string? debugFilterFqn = null, CancellationToken ct = default)
    {
        // Placeholder implementation
        return Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
    }

    protected override Task<string> BuildPromptAsync(
        WorkItem item, string? previousFailureReason, string? previousResponse, CancellationToken ct)
    {
        return Task.FromResult("Placeholder Prompt");
    }

    protected override VerificationResult VerifyResponseFormat(string response)
    {
        return new VerificationResult { IsValid = true };
    }
}
