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

    public override async Task<IReadOnlyList<WorkItem>> CollectWorkItemsAsync(
        string? debugFilterFqn = null, CancellationToken ct = default)
    {
        var query = _typeDb.Types
            .Where(t => t.Type == TypeType.Struct || t.Type == TypeType.Class)
            .Where(t => !t.IsIgnored);

        if (debugFilterFqn != null)
        {
            query = query.Where(t => t.StoredFullyQualifiedName == debugFilterFqn);
        }

        var structs = await query.ToListAsync(ct);

        return structs.Select(s => new WorkItem
        {
            EntityType = EntityType.Struct,
            EntityId = s.Id,
            EntityName = s.BaseName,
            FullyQualifiedName = s.StoredFullyQualifiedName
        }).ToList();
    }

    protected override async Task<string> BuildPromptAsync(
        WorkItem item, IReadOnlyList<string> failureHistory, string? previousResponse, CancellationToken ct)
    {
        var references = await ReferenceGenerator.GenerateStructReferenceAsync(
            item.EntityId,
            new ReferenceOptions
            {
                IncludeComments = false,
                IncludeBaseTypes = true,
                IncludeMemberFunctions = true
            },
            ct);

        var builder = new PromptBuilder()
            .WithSystemMessage(SystemPrompt)
            .WithTargetContext($"Generating comments for struct:\n{item.FullyQualifiedName}")
            .WithReferences(references)
            .WithRetryFeedback(failureHistory)
            .WithPreviousResponse(previousResponse)
            .WithFewShotExample(
                FewShotExamples.StructInput1,
                FewShotExamples.StructOutput1)
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
        var references = await ReferenceGenerator.GenerateStructReferenceAsync(
            item.EntityId,
            new ReferenceOptions
            {
                IncludeComments = false,
                IncludeBaseTypes = true,
                IncludeMemberFunctions = true
            },
            ct);

        return
            $@"You are a code review assistant. Verify whether the following XML documentation comment accurately describes the struct AND follows the provided guidelines.

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
        @"You are an expert C++ code analyst. Your task is to generate valid XML documentation comments for C++ structs/classes, following C# xmldoc conventions.

Guidelines:
- Use <summary> to describe the purpose and responsibility of the struct/class concisely (1-3 sentences).
- Focus on what the struct represents and its role in the system.
- Do not start with ""This struct"" or ""This class"" - be direct.
- The output should contain the XML tags themselves (e.g., <summary>).
- Multiple top-level tags are allowed and expected. This is valid for xmldoc.
- Do not include any other text or markdown blocks, only the XML content.";

    private static class FewShotExamples
    {
        public const string StructInput1 = @"struct Vector3 {
    float x;
    float y;
    float z;
};";

        public const string StructOutput1 =
            @"<summary>Represents a 3D vector with X, Y, and Z components, used for spatial coordinates and direction.</summary>";
    }
}
