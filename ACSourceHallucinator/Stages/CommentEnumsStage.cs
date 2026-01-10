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
        var enumDefinition = await ReferenceGenerator.GenerateEnumReferenceAsync(
            item.EntityId,
            new ReferenceOptions { IncludeComments = false, IncludeReferencingFunctions = false },
            ct);

        var referencingFunctions = await ReferenceGenerator.GenerateEnumReferenceAsync(
            item.EntityId,
            new ReferenceOptions
                { IncludeComments = false, IncludeReferencingFunctions = true, IncludeDefinition = false },
            ct);

        var builder = new PromptBuilder()
            .WithSystemMessage(GetSystemPrompt(item.FullyQualifiedName))
            .WithReferences(referencingFunctions)
            .WithRetryFeedback(failureHistory)
            .WithPreviousResponse(previousResponse)
            .WithFewShotExample(
                FewShotExamples.EnumInput1,
                FewShotExamples.EnumOutput1)
            .WithInput(enumDefinition);

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
        var enumDefinition = await ReferenceGenerator.GenerateEnumReferenceAsync(
            item.EntityId,
            new ReferenceOptions { IncludeComments = false, IncludeReferencingFunctions = false },
            ct);

        var referencingFunctions = await ReferenceGenerator.GenerateEnumReferenceAsync(
            item.EntityId,
            new ReferenceOptions
                { IncludeComments = false, IncludeReferencingFunctions = true, IncludeDefinition = false },
            ct);

        return
            $@"<role>You are a code review assistant.</role>
<task>Verify whether the following XML documentation comment accurately describes the enum AND follows the provided guidelines.</task>

<guidelines>
{GetSystemPrompt(item.FullyQualifiedName)}
</guidelines>

<enum_definition>
{enumDefinition}
</enum_definition>

<references>
{referencingFunctions}
</references>

<generated_comment>
{generatedContent}
</generated_comment>

First, analyze the generated comment step-by-step:
1. Check if the <summary> accurately reflects the enum's purpose.
2. Verify that the description aligns with the enum members and references.
3. Check for any style violations (e.g., starting with ""This enum"").
4. Identify any hallucinations or inaccuracies.

After your analysis, provide a JSON object in exactly this format:
{{
    ""valid"": true/false,
    ""reason"": ""explanation if invalid (mention which guideline was violated or what is inaccurate), or 'OK' if valid""
}}";
    }

    private string GetSystemPrompt(string fullyQualifiedName) =>
        $@"<role>You are an expert C++ code analyst.</role>
<task>Your task is to generate valid XML documentation comments for C++ enums, following C# xmldoc conventions.</task>

<guidelines>
- Use <summary> to describe the purpose and usage of the enum {fullyQualifiedName} concisely (1-3 sentences).
- Focus on what the enum represents in the system.
- Do not start with ""This enum"" - be direct.
- Include an overview of the enum, you do not need to describe each member.
</guidelines>

<output_format>
- The output should contain the XML tags themselves (e.g., <summary>).
- Multiple top-level tags are allowed and expected. This is valid for xmldoc.
- Do not include any other text or markdown blocks, only the XML content.
</output_format>";

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
