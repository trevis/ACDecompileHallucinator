using System.Xml.Linq;
using ACDecompileParser.Shared.Lib.Storage;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Enums;
using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Models;
using ACSourceHallucinator.Pipeline;
using ACSourceHallucinator.Prompts;
using Microsoft.EntityFrameworkCore;

namespace ACSourceHallucinator.Stages;

public class CommentFunctionsStage : StageBase
{
    private readonly TypeContext _typeDb;

    public override string Name => "CommentFunctions";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public CommentFunctionsStage(
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
        var query = _typeDb.FunctionBodies
            .Include(f => f.ParentType)
            .Include(f => f.FunctionSignature)
            .Where(f => f.ParentId != null) // Methods only, not free functions
            .Where(f => !f.ParentType!.IsIgnored); // Exclude ignored types

        if (debugFilterFqn != null)
        {
            query = query.Where(f => f.ParentType!.StoredFullyQualifiedName == debugFilterFqn);
        }

        var functions = await query.ToListAsync(ct);

        return functions.Select(f => new WorkItem
        {
            EntityType = EntityType.StructMethod,
            EntityId = f.Id,
            EntityName = f.FunctionSignature?.Name ?? "Unknown",
            FullyQualifiedName = f.FunctionSignature?.FullyQualifiedName ?? f.FullyQualifiedName,
            Metadata = new Dictionary<string, object>
            {
                ["ParentTypeId"] = f.ParentId!.Value
            }
        }).ToList();
    }

    protected override async Task<string> BuildPromptAsync(
        WorkItem item, IReadOnlyList<string> failureHistory, string? previousResponse, CancellationToken ct)
    {
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature)
            .FirstAsync(f => f.Id == item.EntityId, ct);

        var references = await ReferenceGenerator.GenerateReferencesForFunctionAsync(
            item.EntityId,
            new ReferenceOptions { IncludeComments = false },
            ct);

        var builder = new PromptBuilder()
            .WithSystemMessage(GetSystemPrompt(item.FullyQualifiedName))
            .WithTargetContext($"Generating comments for function:\n{function.FullyQualifiedName}")
            .WithReferences(references)
            .WithRetryFeedback(failureHistory)
            .WithPreviousResponse(previousResponse)
            .WithFewShotExample(
                FewShotExamples.FunctionInput1,
                FewShotExamples.FunctionOutput1)
            .WithFewShotExample(
                FewShotExamples.FunctionInput2,
                FewShotExamples.FunctionOutput2)
            .WithInput(function.BodyText);

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
            // Wrap in a root element to allow multiple top-level elements like <summary> and <param>
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
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature)
            .FirstAsync(f => f.Id == item.EntityId, ct);

        var references = await ReferenceGenerator.GenerateReferencesForFunctionAsync(
            item.EntityId,
            new ReferenceOptions { IncludeComments = false },
            ct);

        return
            $@"You are a code review assistant. Verify whether the following XML documentation comment accurately describes the function AND follows the provided guidelines.

=== GUIDELINES ===
{GetSystemPrompt(item.FullyQualifiedName)}

=== REFERENCES ===
{references}

=== FUNCTION ===
{function.BodyText}

=== GENERATED COMMENT ===
{generatedContent}

Respond with a JSON object in exactly this format:
{{
    ""valid"": true/false,
    ""reason"": ""explanation if invalid (mention which guideline was violated or what is inaccurate), or 'OK' if valid""
}}

Only respond with the JSON object, no other text.";
    }

    private string GetSystemPrompt(string fullyQualifiedName) =>
        $@"You are an expert C++ code analyst. Your task is to generate valid XML documentation comments for decompiled C++ functions, following C# xmldoc conventions.

Guidelines:
- Use <summary> to describe WHAT the function {fullyQualifiedName} does concisely (1-3 sentences).
- Use <param name=""parameterName""> to describe each parameter if relevant.
- Use <returns> to describe the return value if relevant.
- Focus on behavior and purpose, not implementation details.
- Do not start descriptions with ""This function"" - be direct.
- The output should contain the XML tags themselves (e.g., <summary>, <param>, <returns>).
- Multiple top-level tags are allowed and expected (e.g., a <summary> followed by several <param> tags). This is valid for xmldoc.
- Do not include any other text or markdown blocks, only the XML content.";

    private static class FewShotExamples
    {
        public const string FunctionInput1 = @"void PlayerController::UpdatePosition(float deltaTime) {
    this->position.x += this->velocity.x * deltaTime;
    this->position.y += this->velocity.y * deltaTime;
    this->position.z += this->velocity.z * deltaTime;
}";

        public const string FunctionOutput1 =
            @"<summary>Updates the player's position based on current velocity and elapsed time using simple Euler integration.</summary>
<param name=""deltaTime"">The time elapsed since the last update.</param>";

        public const string FunctionInput2 = @"bool InventoryManager::AddItem(Item* item, int quantity) {
    if (item == nullptr || quantity <= 0) return false;
    auto existing = this->FindItem(item->itemId);
    if (existing != nullptr) {
        existing->count += quantity;
    } else {
        this->items.push_back(new InventorySlot(item, quantity));
    }
    return true;
}";

        public const string FunctionOutput2 =
            @"<summary>Adds the specified quantity of an item to the inventory, stacking with existing items if present.</summary>
<param name=""item"">The item to add.</param>
<param name=""quantity"">The number of items to add.</param>
<returns>True if the item was successfully added; otherwise, false if the item is null or quantity is invalid.</returns>";
    }
}
