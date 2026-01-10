using System.Xml.Linq;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Enums;
using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Models;
using ACSourceHallucinator.Pipeline;
using ACSourceHallucinator.Prompts;
using Microsoft.EntityFrameworkCore;

namespace ACSourceHallucinator.Stages;

public class CommentEnumsStage : StageBase
{
    private readonly TypeContext _typeDb;

    public override string Name => "CommentEnums";

    public CommentEnumsStage(
        TypeContext typeDb,
        ILlmClient llmClient,
        IReferenceTextGenerator referenceGenerator,
        IStageResultRepository resultRepo,
        PipelineOptions options)
        : base(llmClient, referenceGenerator, resultRepo, options)
    {
        _typeDb = typeDb;
    }

    public override async Task<IReadOnlyList<WorkItem>> CollectWorkItemsAsync(
        string? debugFilterFqn = null, CancellationToken ct = default)
    {
        var query = _typeDb.Types
            .Where(t => t.Type == TypeType.Enum)
            .Where(t => !t.IsIgnored);

        if (debugFilterFqn != null)
        {
            query = query.Where(t => t.StoredFullyQualifiedName == debugFilterFqn);
        }

        var enums = await query.ToListAsync(ct);

        return enums.Select(e => new WorkItem
        {
            EntityType = EntityType.Enum,
            EntityId = e.Id,
            EntityName = e.BaseName,
            FullyQualifiedName = e.StoredFullyQualifiedName
        }).ToList();
    }

    protected override async Task<string> BuildPromptAsync(
        WorkItem item, IReadOnlyList<string> failureHistory, string? previousResponse, CancellationToken ct)
    {
        var references = await ReferenceGenerator.GenerateEnumReferenceAsync(
            item.EntityId,
            new ReferenceOptions { IncludeComments = false },
            ct);

        var builder = new PromptBuilder()
            .WithSystemMessage(SystemPrompt)
            .WithReferences(references)
            .WithRetryFeedback(failureHistory)
            .WithPreviousResponse(previousResponse)
            .WithFewShotExample(
                FewShotExamples.EnumInput1,
                FewShotExamples.EnumOutput1)
            .WithInput(references);

        return builder.Build();
    }

    protected override VerificationResult VerifyResponseFormat(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return new VerificationResult
            {
                IsValid = false,
                ErrorMessage = "Response was empty"
            };
        }

        try
        {
            var wrappedResponse = $"<root>{response}</root>";
            var xml = XElement.Parse(wrappedResponse);

            if (xml.Element("summary") == null)
            {
                return new VerificationResult
                {
                    IsValid = false,
                    ErrorMessage = "Missing <summary> tag"
                };
            }

            return new VerificationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            return new VerificationResult
            {
                IsValid = false,
                ErrorMessage = $"Invalid XML format: {ex.Message}"
            };
        }
    }

    protected override bool RequiresLlmVerification => true;

    protected override async Task<string> BuildLlmVerificationPromptAsync(
        WorkItem item, string generatedContent, CancellationToken ct)
    {
        var references = await ReferenceGenerator.GenerateEnumReferenceAsync(
            item.EntityId,
            new ReferenceOptions { IncludeComments = false },
            ct);

        return
            $@"You are a code review assistant. Verify whether the following XML documentation comment accurately describes the enum AND follows the provided guidelines.

=== GUIDELINES ===
{SystemPrompt}

=== REFERENCES ===
{references}

=== GENERATED COMMENT ===
{generatedContent}

Respond with a JSON object in exactly this format:
{{
    ""valid"": true/false,
    ""reason"": ""explanation if invalid (mention which guideline was violated or what is inaccurate), or 'OK' if valid""
}}

Only respond with the JSON object, no other text.";
    }

    private const string SystemPrompt =
        @"You are an expert C++ code analyst. Your task is to generate valid XML documentation comments for C++ enums, following C# xmldoc conventions.

Guidelines:
- Use <summary> to describe the purpose and usage of the enum concisely (1-3 sentences).
- Focus on what the enum represents in the system.
- Do not start with ""This enum"" - be direct.
- The output should contain the XML tags themselves (e.g., <summary>).
- Multiple top-level tags are allowed and expected. This is valid for xmldoc.
- Do not include any other text or markdown blocks, only the XML content.";

    private static class FewShotExamples
    {
        public const string EnumInput1 = @"enum WindowState {
    WINDOW_HIDDEN = 0,
    WINDOW_VISIBLE = 1,
    WINDOW_MINIMIZED = 2,
    WINDOW_MAXIMIZED = 3
};";

        public const string EnumOutput1 =
            @"<summary>Specifies the display and visibility state of a application window.</summary>";
    }
}
