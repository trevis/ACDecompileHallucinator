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
            query = query.Where(f => f.ParentType!.FullyQualifiedName == debugFilterFqn);
        }

        var functions = await query.ToListAsync(ct);

        return functions.Select(f => new WorkItem
        {
            EntityType = EntityType.StructMethod,
            EntityId = f.Id,
            EntityName = f.FunctionSignature?.Name ?? "Unknown",
            FullyQualifiedName = f.FullyQualifiedName,
            Metadata = new Dictionary<string, object>
            {
                ["ParentTypeId"] = f.ParentId!.Value
            }
        }).ToList();
    }

    protected override async Task<string> BuildPromptAsync(
        WorkItem item, string? previousFailureReason, CancellationToken ct)
    {
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature)
            .FirstAsync(f => f.Id == item.EntityId, ct);

        var references = await ReferenceGenerator.GenerateReferencesForFunctionAsync(
            item.EntityId,
            new ReferenceOptions { IncludeComments = false },
            ct);

        var builder = new PromptBuilder()
            .WithSystemMessage(SystemPrompt)
            .WithReferences(references)
            .WithRetryFeedback(previousFailureReason)
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

        if (response.Length < 10)
        {
            return new VerificationResult
            {
                IsValid = false,
                ErrorMessage = "Response too short (less than 10 characters)"
            };
        }

        return new VerificationResult { IsValid = true };
    }

    protected override bool RequiresLlmVerification => false;

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
            $@"You are a code review assistant. Verify whether the following comment accurately describes the function.

=== REFERENCES ===
{references}

=== FUNCTION ===
{function.BodyText}

=== GENERATED COMMENT ===
{generatedContent}

Respond with a JSON object in exactly this format:
{{
    ""valid"": true/false,
    ""reason"": ""explanation if invalid, or 'OK' if valid""
}}

Only respond with the JSON object, no other text.";
    }

    private const string SystemPrompt =
        @"You are an expert C++ code analyst. Your task is to generate a concise, informative comment for decompiled C++ functions.

Guidelines:
- Focus on WHAT the function does, not HOW it does it
- Mention key parameters and return values if relevant
- Keep comments to 1-3 sentences
- Do not include code formatting or markdown
- Do not start with ""This function"" - be direct
- If the purpose is unclear, describe the observable behavior

Output only the comment text, nothing else.";

    private static class FewShotExamples
    {
        public const string FunctionInput1 = @"void PlayerController::UpdatePosition(float deltaTime) {
    this->position.x += this->velocity.x * deltaTime;
    this->position.y += this->velocity.y * deltaTime;
    this->position.z += this->velocity.z * deltaTime;
}";

        public const string FunctionOutput1 =
            "Updates the player's position based on current velocity and elapsed time using simple Euler integration.";

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
            "Adds the specified quantity of an item to the inventory. Stacks with existing items if present, otherwise creates a new inventory slot. Returns false if item is null or quantity is invalid.";
    }
}
