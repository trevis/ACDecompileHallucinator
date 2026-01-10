# ACSourceHallucinator - Architecture Specification

## Overview

A C# console application that processes decompiled C++ code stored in a SQLite database, using a local LLM (via OpenAI-compatible API) to generate comments and clean up code through a series of configurable pipeline stages.

**External Dependency**: References a shared library containing the type repository (`TypeContext`, `TypeModel`, `FunctionBodyModel`, etc.).

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Core Abstractions](#core-abstractions)
3. [Pipeline Orchestration](#pipeline-orchestration)
4. [Stage Implementation](#stage-implementation)
5. [LLM Integration](#llm-integration)
6. [Caching Layer](#caching-layer)
7. [Reference Text Generation](#reference-text-generation)
8. [Prompt Building](#prompt-building)
9. [Database Schema Extensions](#database-schema-extensions)
10. [TUI & Progress Reporting](#tui--progress-reporting)
11. [CLI Interface](#cli-interface)
12. [Configuration](#configuration)
13. [Example Stage Implementation](#example-stage-implementation)
14. [Data Flow Diagrams](#data-flow-diagrams)
15. [Async Design Notes](#async-design-notes)

---

## 1. Project Structure

```
ACSourceHallucinator/
├── ACSourceHallucinator.csproj
├── Program.cs                        # Entry point, CLI parsing, DI setup
├── appsettings.json
│
├── Configuration/
│   └── CliOptions.cs
│
├── Models/
│   ├── WorkItem.cs
│   ├── StageResult.cs
│   ├── LlmRequest.cs
│   ├── LlmResponse.cs
│   ├── VerificationResult.cs
│   ├── PipelineOptions.cs
│   └── PipelineStats.cs
│
├── Enums/
│   ├── EntityType.cs
│   └── StageResultStatus.cs
│
├── Interfaces/
│   ├── IStage.cs
│   ├── ILlmClient.cs
│   ├── ILlmCache.cs
│   └── IReferenceTextGenerator.cs
│
├── Data/
│   ├── HallucinatorDbContext.cs      # Stage results + LLM cache tables
│   ├── Entities/
│   │   └── LlmCacheEntry.cs
│   ├── Repositories/
│   │   ├── IStageResultRepository.cs
│   │   └── StageResultRepository.cs
│   └── Migrations/
│
├── Llm/
│   ├── OpenAiCompatibleClient.cs
│   └── LlmCache.cs
│
├── Pipeline/
│   ├── PipelineBuilder.cs
│   ├── PipelineRunner.cs
│   └── StageBase.cs
│
├── Stages/
│   ├── CommentFunctionsStage.cs
│   ├── CommentStructsStage.cs
│   ├── CommentEnumsStage.cs
│   ├── CleanupFunctionBodiesStage.cs
│   └── RenameLocalVariablesStage.cs
│
├── ReferenceGeneration/
│   └── ReferenceTextGenerator.cs
│
├── Prompts/
│   └── PromptBuilder.cs
│
└── Tui/
    └── PipelineTui.cs
```

### 1.1 Project File (ACSourceHallucinator.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Shared library containing TypeContext, TypeModel, FunctionBodyModel, etc. -->
    <ProjectReference Include="..\ACShared\ACShared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="8.0.*" />
    <PackageReference Include="Spectre.Console" Version="0.49.*" />
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.*" />
  </ItemGroup>
</Project>
```

---

## 2. Core Abstractions

### 2.1 Work Item (Models/WorkItem.cs)

Represents a single unit of work for a stage to process.

```csharp
namespace ACSourceHallucinator.Models;

public record WorkItem
{
    public required EntityType EntityType { get; init; }
    public required int EntityId { get; init; }
    public required string EntityName { get; init; }           // For display/logging
    public required string FullyQualifiedName { get; init; }   // For debug filtering
    public Dictionary<string, object> Metadata { get; init; } = new();  // Stage-specific data
}
```

### 2.2 Entity Type (Enums/EntityType.cs)

```csharp
namespace ACSourceHallucinator.Enums;

public enum EntityType
{
    Struct,
    Enum,
    StructMember,
    EnumMember,
    StructMethod,       // FunctionBodyModel with ParentId != null
    FreeFunction        // FunctionBodyModel with ParentId == null
}
```

### 2.3 Stage Result (Models/StageResult.cs)

Represents the outcome of processing a single work item.

```csharp
namespace ACSourceHallucinator.Models;

public class StageResult
{
    public int Id { get; set; }
    public required string StageName { get; set; }
    public required EntityType EntityType { get; set; }
    public required int EntityId { get; set; }
    public required string FullyQualifiedName { get; set; }
    
    public StageResultStatus Status { get; set; }
    public string? GeneratedContent { get; set; }
    public int RetryCount { get; set; }
    public string? LastFailureReason { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### 2.4 Stage Result Status (Enums/StageResultStatus.cs)

```csharp
namespace ACSourceHallucinator.Enums;

public enum StageResultStatus
{
    Pending,
    Success,
    Failed
}
```

### 2.5 Stage Interface (Interfaces/IStage.cs)

```csharp
namespace ACSourceHallucinator.Interfaces;

public interface IStage
{
    string Name { get; }
    IReadOnlyList<string> Dependencies { get; }  // Names of stages that must complete first
    
    Task<IReadOnlyList<WorkItem>> CollectWorkItemsAsync(
        string? debugFilterFqn = null,
        CancellationToken ct = default);
    
    Task<StageResult> ProcessWorkItemAsync(
        WorkItem item,
        CancellationToken ct = default);
}
```

### 2.6 LLM Client Interface (Interfaces/ILlmClient.cs)

```csharp
namespace ACSourceHallucinator.Interfaces;

public interface ILlmClient
{
    Task<LlmResponse> SendRequestAsync(LlmRequest request, CancellationToken ct = default);
}
```

### 2.7 LLM Request (Models/LlmRequest.cs)

```csharp
namespace ACSourceHallucinator.Models;

public record LlmRequest
{
    public required string Prompt { get; init; }
    public required string Model { get; init; }
    public int MaxTokens { get; init; } = 2048;
    public double Temperature { get; init; } = 0.7;
}
```

### 2.8 LLM Response (Models/LlmResponse.cs)

```csharp
namespace ACSourceHallucinator.Models;

public record LlmResponse
{
    public required string Content { get; init; }
    public required int PromptTokens { get; init; }
    public required int CompletionTokens { get; init; }
    public required TimeSpan ResponseTime { get; init; }
    public bool FromCache { get; init; }
}
```

### 2.9 Verification Result (Models/VerificationResult.cs)

```csharp
namespace ACSourceHallucinator.Models;

public record VerificationResult
{
    public bool IsValid { get; init; }
    public string? Reason { get; init; }
    public string? ErrorMessage { get; init; }  // Alias for Reason, used in format verification
    public bool IsFormatError { get; init; }    // True if JSON parsing failed
}
```

### 2.10 Pipeline Options (Models/PipelineOptions.cs)

```csharp
namespace ACSourceHallucinator.Models;

public record PipelineOptions
{
    public string? DebugFilterFqn { get; init; }
    public bool SkipCache { get; init; }
    public int MaxRetries { get; init; } = 5;
    public required string Model { get; init; }
}
```

---

## 3. Pipeline Orchestration

### 3.1 Pipeline Builder (Pipeline/PipelineBuilder.cs)

Fluent API for constructing the pipeline with explicit stage ordering.

```csharp
namespace ACSourceHallucinator.Pipeline;

public class PipelineBuilder
{
    private readonly List<IStage> _stages = new();
    private readonly IServiceProvider _services;
    
    public PipelineBuilder(IServiceProvider services)
    {
        _services = services;
    }
    
    public PipelineBuilder AddStage<TStage>() where TStage : IStage
    {
        var stage = _services.GetRequiredService<TStage>();
        _stages.Add(stage);
        return this;
    }
    
    public Pipeline Build()
    {
        ValidateDependencies();
        return new Pipeline(_stages);
    }
    
    private void ValidateDependencies()
    {
        var registered = new HashSet<string>();
        foreach (var stage in _stages)
        {
            foreach (var dep in stage.Dependencies)
            {
                if (!registered.Contains(dep))
                    throw new InvalidOperationException(
                        $"Stage '{stage.Name}' depends on '{dep}' which is not registered or comes later");
            }
            registered.Add(stage.Name);
        }
    }
}

public class Pipeline
{
    public IReadOnlyList<IStage> Stages { get; }
    
    public Pipeline(IReadOnlyList<IStage> stages)
    {
        Stages = stages;
    }
}
```

### 3.2 Pipeline Runner (Pipeline/PipelineRunner.cs)

Executes stages in order, handling resumability and progress tracking.

```csharp
namespace ACSourceHallucinator.Pipeline;

public class PipelineRunner
{
    private readonly IStageResultRepository _resultRepo;
    private readonly PipelineTui _tui;
    private readonly PipelineStats _stats;
    
    public PipelineRunner(
        IStageResultRepository resultRepo,
        PipelineTui tui)
    {
        _resultRepo = resultRepo;
        _tui = tui;
        _stats = new PipelineStats();
    }
    
    public async Task RunAsync(Pipeline pipeline, PipelineOptions options, CancellationToken ct = default)
    {
        _tui.Initialize();
        
        foreach (var stage in pipeline.Stages)
        {
            await RunStageAsync(stage, options, ct);
        }
        
        _tui.DisplayFinalStats(_stats);
    }
    
    private async Task RunStageAsync(IStage stage, PipelineOptions options, CancellationToken ct)
    {
        _tui.SetCurrentStage(stage.Name);
        
        // 1. Collect work items
        var allItems = await stage.CollectWorkItemsAsync(options.DebugFilterFqn, ct);
        
        // 2. Filter out already-completed items (resumability)
        var completedIds = await _resultRepo.GetCompletedEntityIdsAsync(stage.Name);
        var pendingItems = allItems
            .Where(item => !completedIds.Contains((item.EntityType, item.EntityId)))
            .ToList();
        
        _tui.SetTotalItems(allItems.Count, pendingItems.Count);
        
        // 3. Process each item
        for (int i = 0; i < pendingItems.Count; i++)
        {
            var item = pendingItems[i];
            _tui.UpdateProgress(i + 1, item.FullyQualifiedName);
            
            var result = await stage.ProcessWorkItemAsync(item, ct);
            await _resultRepo.SaveResultAsync(result);
            
            _stats.Record(result);
            _tui.UpdateStats(_stats);
        }
    }
}
```

---

## 4. Stage Implementation

### 4.1 Stage Base Class (Pipeline/StageBase.cs)

Provides common functionality for all stages. All prompt-building and verification methods are async to properly support database and reference generation calls.

```csharp
namespace ACSourceHallucinator.Pipeline;

public abstract class StageBase : IStage
{
    protected readonly ILlmClient LlmClient;
    protected readonly IReferenceTextGenerator ReferenceGenerator;
    protected readonly IStageResultRepository ResultRepo;
    protected readonly PipelineOptions Options;
    
    protected StageBase(
        ILlmClient llmClient,
        IReferenceTextGenerator referenceGenerator,
        IStageResultRepository resultRepo,
        PipelineOptions options)
    {
        LlmClient = llmClient;
        ReferenceGenerator = referenceGenerator;
        ResultRepo = resultRepo;
        Options = options;
    }
    
    public abstract string Name { get; }
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();
    
    public abstract Task<IReadOnlyList<WorkItem>> CollectWorkItemsAsync(
        string? debugFilterFqn = null,
        CancellationToken ct = default);
    
    public async Task<StageResult> ProcessWorkItemAsync(WorkItem item, CancellationToken ct)
    {
        var result = new StageResult
        {
            StageName = Name,
            EntityType = item.EntityType,
            EntityId = item.EntityId,
            FullyQualifiedName = item.FullyQualifiedName,
            Status = StageResultStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        string? previousFailureReason = null;
        
        for (int attempt = 0; attempt < Options.MaxRetries; attempt++)
        {
            result.RetryCount = attempt;
            
            // 1. Build and send generation prompt (async)
            var prompt = await BuildPromptAsync(item, previousFailureReason, ct);
            var response = await LlmClient.SendRequestAsync(
                new LlmRequest { Prompt = prompt, Model = Options.Model }, ct);
            
            // 2. Verify response format
            var formatVerification = VerifyResponseFormat(response.Content);
            if (!formatVerification.IsValid)
            {
                previousFailureReason = $"Format validation failed: {formatVerification.ErrorMessage}";
                continue;
            }
            
            // 3. Optional LLM verification (if stage implements it)
            if (RequiresLlmVerification)
            {
                var llmVerifyResult = await RunLlmVerificationAsync(item, response.Content, ct);
                if (!llmVerifyResult.IsValid)
                {
                    previousFailureReason = $"LLM verification failed: {llmVerifyResult.Reason}";
                    continue;
                }
            }
            
            // Success!
            result.Status = StageResultStatus.Success;
            result.GeneratedContent = response.Content;
            result.UpdatedAt = DateTime.UtcNow;
            return result;
        }
        
        // Exhausted retries
        result.Status = StageResultStatus.Failed;
        result.LastFailureReason = previousFailureReason;
        result.UpdatedAt = DateTime.UtcNow;
        return result;
    }
    
    // --- Abstract/Virtual methods for stage customization ---
    
    /// <summary>
    /// Builds the generation prompt. Async to allow database queries and reference generation.
    /// </summary>
    protected abstract Task<string> BuildPromptAsync(
        WorkItem item, 
        string? previousFailureReason, 
        CancellationToken ct);
    
    /// <summary>
    /// Validates the LLM response format. Synchronous as it should only do simple checks.
    /// </summary>
    protected abstract VerificationResult VerifyResponseFormat(string response);
    
    /// <summary>
    /// Whether this stage requires LLM-based verification after format verification passes.
    /// </summary>
    protected virtual bool RequiresLlmVerification => true;
    
    /// <summary>
    /// Builds the LLM verification prompt. Async to allow database queries and reference generation.
    /// </summary>
    protected virtual Task<string> BuildLlmVerificationPromptAsync(
        WorkItem item, 
        string generatedContent, 
        CancellationToken ct)
    {
        throw new NotImplementedException("Override if RequiresLlmVerification is true");
    }
    
    /// <summary>
    /// Parses the LLM verification response. Synchronous as it only parses JSON.
    /// </summary>
    protected virtual VerificationResult ParseLlmVerificationResponse(string response)
    {
        // Default JSON parsing for { "valid": bool, "reason": string }
        try
        {
            var json = JsonSerializer.Deserialize<LlmVerificationJson>(response);
            return new VerificationResult
            {
                IsValid = json?.Valid ?? false,
                Reason = json?.Reason
            };
        }
        catch (JsonException ex)
        {
            return new VerificationResult
            {
                IsValid = false,
                Reason = $"JSON parse error: {ex.Message}",
                IsFormatError = true
            };
        }
    }
    
    private async Task<VerificationResult> RunLlmVerificationAsync(
        WorkItem item, string generatedContent, CancellationToken ct)
    {
        string? verifyFailureReason = null;
        
        for (int attempt = 0; attempt < Options.MaxRetries; attempt++)
        {
            var verifyPrompt = await BuildLlmVerificationPromptInternalAsync(
                item, generatedContent, verifyFailureReason, ct);
            var verifyResponse = await LlmClient.SendRequestAsync(
                new LlmRequest { Prompt = verifyPrompt, Model = Options.Model }, ct);
            
            var parseResult = ParseLlmVerificationResponse(verifyResponse.Content);
            
            if (parseResult.IsFormatError)
            {
                verifyFailureReason = parseResult.Reason;
                continue;  // Retry verification with format error feedback
            }
            
            return parseResult;  // Valid JSON, return whether verification passed or failed
        }
        
        // Couldn't get valid JSON from verification after retries
        return new VerificationResult
        {
            IsValid = false,
            Reason = $"Verification failed to return valid JSON after {Options.MaxRetries} attempts"
        };
    }
    
    private async Task<string> BuildLlmVerificationPromptInternalAsync(
        WorkItem item, string generatedContent, string? previousError, CancellationToken ct)
    {
        var prompt = await BuildLlmVerificationPromptAsync(item, generatedContent, ct);
        
        if (previousError != null)
        {
            prompt += $"\n\n=== PREVIOUS ATTEMPT ERROR ===\n{previousError}\n\nPlease ensure your response is valid JSON.";
        }
        
        return prompt;
    }
}

internal record LlmVerificationJson
{
    [JsonPropertyName("valid")]
    public bool Valid { get; init; }
    
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
```

---

## 5. LLM Integration

### 5.1 OpenAI-Compatible Client (Llm/OpenAiCompatibleClient.cs)

```csharp
namespace ACSourceHallucinator.Llm;

public class OpenAiCompatibleClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILlmCache _cache;
    private readonly LlmClientOptions _options;
    private readonly ILogger<OpenAiCompatibleClient> _logger;
    
    public OpenAiCompatibleClient(
        HttpClient httpClient,
        ILlmCache cache,
        IOptions<LlmClientOptions> options,
        ILogger<OpenAiCompatibleClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(_options.TimeoutMinutes);
    }
    
    public async Task<LlmResponse> SendRequestAsync(LlmRequest request, CancellationToken ct)
    {
        // Check cache first
        if (!_options.SkipCache)
        {
            var cached = await _cache.GetAsync(request);
            if (cached != null)
            {
                return cached with { FromCache = true };
            }
        }
        
        // Build request
        var apiRequest = new
        {
            model = request.Model,
            messages = new[]
            {
                new { role = "user", content = request.Prompt }
            },
            max_tokens = request.MaxTokens,
            temperature = request.Temperature
        };
        
        var stopwatch = Stopwatch.StartNew();
        
        var httpResponse = await _httpClient.PostAsJsonAsync(
            "/v1/chat/completions", apiRequest, ct);
        
        httpResponse.EnsureSuccessStatusCode();
        
        stopwatch.Stop();
        
        var responseBody = await httpResponse.Content
            .ReadFromJsonAsync<OpenAiChatResponse>(ct);
        
        var response = new LlmResponse
        {
            Content = responseBody!.Choices[0].Message.Content,
            PromptTokens = responseBody.Usage.PromptTokens,
            CompletionTokens = responseBody.Usage.CompletionTokens,
            ResponseTime = stopwatch.Elapsed,
            FromCache = false
        };
        
        // Cache the response
        await _cache.SetAsync(request, response);
        
        return response;
    }
}

public record LlmClientOptions
{
    public required string BaseUrl { get; init; }
    public int MaxTokens { get; init; } = 2048;
    public double Temperature { get; init; } = 0.7;
    public int TimeoutMinutes { get; init; } = 15;
    public bool SkipCache { get; init; }
}

// OpenAI response DTOs
internal record OpenAiChatResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAiChoice> Choices { get; init; } = new();
    
    [JsonPropertyName("usage")]
    public OpenAiUsage Usage { get; init; } = new();
}

internal record OpenAiChoice
{
    [JsonPropertyName("message")]
    public OpenAiMessage Message { get; init; } = new();
}

internal record OpenAiMessage
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = "";
}

internal record OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }
    
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }
}
```

---

## 6. Caching Layer

### 6.1 Cache Interface (Interfaces/ILlmCache.cs)

```csharp
namespace ACSourceHallucinator.Interfaces;

public interface ILlmCache
{
    Task<LlmResponse?> GetAsync(LlmRequest request);
    Task SetAsync(LlmRequest request, LlmResponse response);
}
```

### 6.2 Cache Implementation (Llm/LlmCache.cs)

```csharp
namespace ACSourceHallucinator.Llm;

public class LlmCache : ILlmCache
{
    private readonly HallucinatorDbContext _db;
    
    public LlmCache(HallucinatorDbContext db)
    {
        _db = db;
    }
    
    public async Task<LlmResponse?> GetAsync(LlmRequest request)
    {
        var cacheKey = ComputeCacheKey(request);
        
        var entry = await _db.LlmCacheEntries
            .FirstOrDefaultAsync(e => e.CacheKey == cacheKey);
        
        if (entry == null) return null;
        
        return new LlmResponse
        {
            Content = entry.ResponseContent,
            PromptTokens = entry.PromptTokens,
            CompletionTokens = entry.CompletionTokens,
            ResponseTime = TimeSpan.FromMilliseconds(entry.ResponseTimeMs),
            FromCache = true
        };
    }
    
    public async Task SetAsync(LlmRequest request, LlmResponse response)
    {
        var cacheKey = ComputeCacheKey(request);
        
        var entry = new LlmCacheEntry
        {
            CacheKey = cacheKey,
            PromptText = request.Prompt,
            Model = request.Model,
            Temperature = request.Temperature,
            ResponseContent = response.Content,
            PromptTokens = response.PromptTokens,
            CompletionTokens = response.CompletionTokens,
            ResponseTimeMs = (int)response.ResponseTime.TotalMilliseconds,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.LlmCacheEntries.Add(entry);
        await _db.SaveChangesAsync();
    }
    
    private static string ComputeCacheKey(LlmRequest request)
    {
        var input = $"{request.Model}|{request.Temperature}|{request.Prompt}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
```

---

## 7. Reference Text Generation

### 7.1 Reference Generator Interface (Interfaces/IReferenceTextGenerator.cs)

```csharp
namespace ACSourceHallucinator.Interfaces;

public interface IReferenceTextGenerator
{
    /// <summary>
    /// Generates formatted reference text for a struct, including members and base types.
    /// </summary>
    Task<string> GenerateStructReferenceAsync(
        int structId, 
        ReferenceOptions options, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Generates formatted reference text for an enum, including members.
    /// </summary>
    Task<string> GenerateEnumReferenceAsync(
        int enumId, 
        ReferenceOptions options, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Generates formatted reference text for a function signature.
    /// </summary>
    Task<string> GenerateFunctionReferenceAsync(
        int functionBodyId, 
        ReferenceOptions options, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Collects all referenced types from a function signature (params, return type)
    /// and generates their reference text.
    /// </summary>
    Task<string> GenerateReferencesForFunctionAsync(
        int functionBodyId, 
        ReferenceOptions options, 
        CancellationToken ct = default);
}

public record ReferenceOptions
{
    /// <summary>Include existing comments from previous stages if available.</summary>
    public bool IncludeComments { get; init; } = true;
    
    /// <summary>Include struct members in struct references.</summary>
    public bool IncludeMembers { get; init; } = true;
    
    /// <summary>Recursively include base type definitions.</summary>
    public bool IncludeBaseTypes { get; init; } = true;
    
    /// <summary>Stage name to query for existing comments.</summary>
    public string? CommentsFromStage { get; init; }
}
```

### 7.2 Reference Generator Implementation (ReferenceGeneration/ReferenceTextGenerator.cs)

```csharp
namespace ACSourceHallucinator.ReferenceGeneration;

public class ReferenceTextGenerator : IReferenceTextGenerator
{
    private readonly TypeContext _typeDb;  // From shared library
    private readonly IStageResultRepository _resultRepo;
    
    public ReferenceTextGenerator(TypeContext typeDb, IStageResultRepository resultRepo)
    {
        _typeDb = typeDb;
        _resultRepo = resultRepo;
    }
    
    public async Task<string> GenerateReferencesForFunctionAsync(
        int functionBodyId, ReferenceOptions options, CancellationToken ct = default)
    {
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature)
                .ThenInclude(s => s.Parameters)
                    .ThenInclude(p => p.TypeReference)
            .Include(f => f.FunctionSignature)
                .ThenInclude(s => s.ReturnTypeReference)
            .Include(f => f.ParentType)
            .FirstOrDefaultAsync(f => f.Id == functionBodyId, ct);
        
        if (function == null)
            throw new ArgumentException($"Function body {functionBodyId} not found");
        
        var referencedTypeIds = new HashSet<int>();
        var sections = new List<string>();
        
        // Collect parent struct
        if (function.ParentId.HasValue)
        {
            referencedTypeIds.Add(function.ParentId.Value);
        }
        
        // Collect return type
        if (function.FunctionSignature?.ReturnTypeReference?.ReferencedTypeId.HasValue == true)
        {
            referencedTypeIds.Add(function.FunctionSignature.ReturnTypeReference.ReferencedTypeId.Value);
        }
        
        // Collect parameter types
        foreach (var param in function.FunctionSignature?.Parameters ?? Enumerable.Empty<FunctionParamModel>())
        {
            if (param.TypeReference?.ReferencedTypeId.HasValue == true)
            {
                referencedTypeIds.Add(param.TypeReference.ReferencedTypeId.Value);
            }
        }
        
        // Recursively collect base types
        if (options.IncludeBaseTypes)
        {
            await CollectBaseTypesRecursivelyAsync(referencedTypeIds, ct);
        }
        
        // Generate sections
        if (function.ParentId.HasValue)
        {
            var parentRef = await GenerateStructReferenceAsync(function.ParentId.Value, options, ct);
            sections.Add($"=== PARENT STRUCT ===\n{parentRef}");
        }
        
        var paramTypeIds = function.FunctionSignature?.Parameters
            .Where(p => p.TypeReference?.ReferencedTypeId.HasValue == true)
            .Select(p => p.TypeReference!.ReferencedTypeId!.Value)
            .Distinct()
            .Where(id => id != function.ParentId)  // Don't duplicate parent
            .ToList() ?? new List<int>();
        
        if (paramTypeIds.Any())
        {
            var paramRefs = new List<string>();
            foreach (var typeId in paramTypeIds)
            {
                paramRefs.Add(await GenerateTypeReferenceAsync(typeId, options, ct));
            }
            sections.Add($"=== PARAMETER TYPES ===\n{string.Join("\n\n", paramRefs)}");
        }
        
        var returnTypeId = function.FunctionSignature?.ReturnTypeReference?.ReferencedTypeId;
        if (returnTypeId.HasValue && returnTypeId != function.ParentId && !paramTypeIds.Contains(returnTypeId.Value))
        {
            var returnRef = await GenerateTypeReferenceAsync(returnTypeId.Value, options, ct);
            sections.Add($"=== RETURN TYPE ===\n{returnRef}");
        }
        
        // Base types (excluding already-shown types)
        var baseTypeIds = referencedTypeIds
            .Except(paramTypeIds)
            .Where(id => id != function.ParentId && id != returnTypeId)
            .ToList();
        
        if (baseTypeIds.Any() && options.IncludeBaseTypes)
        {
            var baseRefs = new List<string>();
            foreach (var typeId in baseTypeIds)
            {
                baseRefs.Add(await GenerateTypeReferenceAsync(typeId, options, ct));
            }
            sections.Add($"=== BASE TYPES ===\n{string.Join("\n\n", baseRefs)}");
        }
        
        return string.Join("\n\n", sections);
    }
    
    private async Task<string> GenerateTypeReferenceAsync(
        int typeId, ReferenceOptions options, CancellationToken ct)
    {
        var type = await _typeDb.Types
            .Include(t => t.Members)
                .ThenInclude(m => m.TypeReference)
            .Include(t => t.EnumMembers)
            .FirstOrDefaultAsync(t => t.Id == typeId, ct);
        
        if (type == null) return $"// Type {typeId} not found";
        
        return type.Kind switch
        {
            TypeKind.Struct or TypeKind.Class => await GenerateStructReferenceAsync(typeId, options, ct),
            TypeKind.Enum => await GenerateEnumReferenceAsync(typeId, options, ct),
            _ => $"// {type.FullyQualifiedName} ({type.Kind})"
        };
    }
    
    public async Task<string> GenerateStructReferenceAsync(
        int structId, ReferenceOptions options, CancellationToken ct = default)
    {
        var structType = await _typeDb.Types
            .Include(t => t.Members.OrderBy(m => m.Offset))
                .ThenInclude(m => m.TypeReference)
            .FirstOrDefaultAsync(t => t.Id == structId, ct);
        
        if (structType == null)
            return $"// Struct {structId} not found";
        
        var sb = new StringBuilder();
        
        // Add comment if available
        if (options.IncludeComments && options.CommentsFromStage != null)
        {
            var comment = await _resultRepo.GetSuccessfulResultAsync(
                options.CommentsFromStage, EntityType.Struct, structId);
            if (comment != null)
            {
                sb.AppendLine($"// {comment.GeneratedContent}");
            }
        }
        
        sb.AppendLine($"struct {structType.FullyQualifiedName} {{");
        
        if (options.IncludeMembers)
        {
            foreach (var member in structType.Members.Where(m => !m.IsFunctionPointer))
            {
                // Add member comment if available
                if (options.IncludeComments && options.CommentsFromStage != null)
                {
                    var memberComment = await _resultRepo.GetSuccessfulResultAsync(
                        options.CommentsFromStage, EntityType.StructMember, member.Id);
                    if (memberComment != null)
                    {
                        sb.AppendLine($"    // {memberComment.GeneratedContent}");
                    }
                }
                
                var typeStr = member.TypeReference?.TypeString ?? "unknown";
                sb.AppendLine($"    {typeStr} {member.Name}; // offset: 0x{member.Offset:X}");
            }
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    public async Task<string> GenerateEnumReferenceAsync(
        int enumId, ReferenceOptions options, CancellationToken ct = default)
    {
        var enumType = await _typeDb.Types
            .Include(t => t.EnumMembers.OrderBy(m => m.Value))
            .FirstOrDefaultAsync(t => t.Id == enumId, ct);
        
        if (enumType == null)
            return $"// Enum {enumId} not found";
        
        var sb = new StringBuilder();
        
        if (options.IncludeComments && options.CommentsFromStage != null)
        {
            var comment = await _resultRepo.GetSuccessfulResultAsync(
                options.CommentsFromStage, EntityType.Enum, enumId);
            if (comment != null)
            {
                sb.AppendLine($"// {comment.GeneratedContent}");
            }
        }
        
        sb.AppendLine($"enum {enumType.FullyQualifiedName} {{");
        
        foreach (var member in enumType.EnumMembers)
        {
            sb.AppendLine($"    {member.Name} = {member.Value},");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    public async Task<string> GenerateFunctionReferenceAsync(
        int functionBodyId, ReferenceOptions options, CancellationToken ct = default)
    {
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature)
                .ThenInclude(s => s.Parameters)
            .Include(f => f.ParentType)
            .FirstOrDefaultAsync(f => f.Id == functionBodyId, ct);
        
        if (function == null)
            return $"// Function {functionBodyId} not found";
        
        var sb = new StringBuilder();
        
        // Add comment if available
        if (options.IncludeComments && options.CommentsFromStage != null)
        {
            var comment = await _resultRepo.GetSuccessfulResultAsync(
                options.CommentsFromStage, EntityType.StructMethod, functionBodyId);
            if (comment != null)
            {
                sb.AppendLine($"// {comment.GeneratedContent}");
            }
        }
        
        sb.AppendLine(function.Source);  // Full function source
        
        return sb.ToString();
    }
    
    private async Task CollectBaseTypesRecursivelyAsync(HashSet<int> typeIds, CancellationToken ct)
    {
        var toProcess = new Queue<int>(typeIds);
        
        while (toProcess.Count > 0)
        {
            var typeId = toProcess.Dequeue();
            
            var inheritances = await _typeDb.TypeInheritances
                .Where(i => i.ParentTypeId == typeId)
                .Select(i => i.RelatedTypeId)
                .ToListAsync(ct);
            
            foreach (var baseTypeId in inheritances)
            {
                if (typeIds.Add(baseTypeId))
                {
                    toProcess.Enqueue(baseTypeId);
                }
            }
        }
    }
}
```

---

## 8. Prompt Building

### 8.1 Prompt Builder (Prompts/PromptBuilder.cs)

```csharp
namespace ACSourceHallucinator.Prompts;

public class PromptBuilder
{
    private string? _systemMessage;
    private string? _referencesSection;
    private string? _retryFeedback;
    private readonly List<(string Input, string Output)> _fewShotExamples = new();
    private string? _input;
    
    public PromptBuilder WithSystemMessage(string message)
    {
        _systemMessage = message;
        return this;
    }
    
    public PromptBuilder WithReferences(string references)
    {
        _referencesSection = references;
        return this;
    }
    
    public PromptBuilder WithRetryFeedback(string? feedback)
    {
        _retryFeedback = feedback;
        return this;
    }
    
    public PromptBuilder WithFewShotExample(string input, string output)
    {
        _fewShotExamples.Add((input, output));
        return this;
    }
    
    public PromptBuilder WithInput(string input)
    {
        _input = input;
        return this;
    }
    
    public string Build()
    {
        var sb = new StringBuilder();
        
        if (_systemMessage != null)
        {
            sb.AppendLine(_systemMessage);
            sb.AppendLine();
        }
        
        if (_referencesSection != null)
        {
            sb.AppendLine("=== REFERENCES ===");
            sb.AppendLine(_referencesSection);
            sb.AppendLine();
        }
        
        if (_retryFeedback != null)
        {
            sb.AppendLine("=== PREVIOUS ATTEMPT FEEDBACK ===");
            sb.AppendLine(_retryFeedback);
            sb.AppendLine("Please address the above feedback in your response.");
            sb.AppendLine();
        }
        
        if (_fewShotExamples.Any())
        {
            sb.AppendLine("=== EXAMPLES ===");
            foreach (var (input, output) in _fewShotExamples)
            {
                sb.AppendLine($"Input: {input}");
                sb.AppendLine($"Output: {output}");
                sb.AppendLine();
            }
        }
        
        if (_input != null)
        {
            sb.AppendLine($"Input: {_input}");
        }
        
        return sb.ToString();
    }
}
```

---

## 9. Database Schema Extensions

### 9.1 Hallucinator DbContext (Data/HallucinatorDbContext.cs)

```csharp
namespace ACSourceHallucinator.Data;

public class HallucinatorDbContext : DbContext
{
    public DbSet<StageResult> StageResults { get; set; }
    public DbSet<LlmCacheEntry> LlmCacheEntries { get; set; }
    
    public HallucinatorDbContext(DbContextOptions<HallucinatorDbContext> options) 
        : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StageResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StageName, e.EntityType, e.EntityId }).IsUnique();
            entity.HasIndex(e => e.FullyQualifiedName);
            entity.Property(e => e.GeneratedContent).HasColumnType("TEXT");
            entity.Property(e => e.LastFailureReason).HasColumnType("TEXT");
            entity.Property(e => e.EntityType).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });
        
        modelBuilder.Entity<LlmCacheEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CacheKey).IsUnique();
            entity.Property(e => e.PromptText).HasColumnType("TEXT");
            entity.Property(e => e.ResponseContent).HasColumnType("TEXT");
        });
    }
}
```

### 9.2 LLM Cache Entry (Data/Entities/LlmCacheEntry.cs)

```csharp
namespace ACSourceHallucinator.Data.Entities;

public class LlmCacheEntry
{
    public int Id { get; set; }
    public required string CacheKey { get; set; }        // SHA256 hash
    public required string PromptText { get; set; }      // For debugging
    public required string Model { get; set; }
    public double Temperature { get; set; }
    public required string ResponseContent { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 9.3 Stage Result Repository Interface (Data/Repositories/IStageResultRepository.cs)

```csharp
namespace ACSourceHallucinator.Data.Repositories;

public interface IStageResultRepository
{
    Task<HashSet<(EntityType, int)>> GetCompletedEntityIdsAsync(string stageName);
    Task SaveResultAsync(StageResult result);
    Task<StageResult?> GetSuccessfulResultAsync(string stageName, EntityType entityType, int entityId);
}
```

### 9.4 Stage Result Repository (Data/Repositories/StageResultRepository.cs)

```csharp
namespace ACSourceHallucinator.Data.Repositories;

public class StageResultRepository : IStageResultRepository
{
    private readonly HallucinatorDbContext _db;
    
    public StageResultRepository(HallucinatorDbContext db)
    {
        _db = db;
    }
    
    public async Task<HashSet<(EntityType, int)>> GetCompletedEntityIdsAsync(string stageName)
    {
        var results = await _db.StageResults
            .Where(r => r.StageName == stageName && r.Status == StageResultStatus.Success)
            .Select(r => new { r.EntityType, r.EntityId })
            .ToListAsync();
        
        return results.Select(r => (r.EntityType, r.EntityId)).ToHashSet();
    }
    
    public async Task SaveResultAsync(StageResult result)
    {
        var existing = await _db.StageResults
            .FirstOrDefaultAsync(r => 
                r.StageName == result.StageName && 
                r.EntityType == result.EntityType && 
                r.EntityId == result.EntityId);
        
        if (existing != null)
        {
            existing.Status = result.Status;
            existing.GeneratedContent = result.GeneratedContent;
            existing.RetryCount = result.RetryCount;
            existing.LastFailureReason = result.LastFailureReason;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.StageResults.Add(result);
        }
        
        await _db.SaveChangesAsync();
    }
    
    public async Task<StageResult?> GetSuccessfulResultAsync(
        string stageName, EntityType entityType, int entityId)
    {
        return await _db.StageResults
            .FirstOrDefaultAsync(r => 
                r.StageName == stageName && 
                r.EntityType == entityType && 
                r.EntityId == entityId &&
                r.Status == StageResultStatus.Success);
    }
}
```

---

## 10. TUI & Progress Reporting

### 10.1 TUI Implementation (Tui/PipelineTui.cs)

Using Spectre.Console for rich terminal UI.

```csharp
namespace ACSourceHallucinator.Tui;

public class PipelineTui
{
    private readonly IAnsiConsole _console;
    
    public PipelineTui(IAnsiConsole console)
    {
        _console = console;
    }
    
    public void Initialize()
    {
        _console.Clear();
        _console.Write(new FigletText("ACSourceHallucinator").Color(Color.Blue));
    }
    
    public void SetCurrentStage(string stageName)
    {
        _console.MarkupLine($"\n[bold blue]Stage: {stageName}[/]");
    }
    
    public void SetTotalItems(int total, int pending)
    {
        _console.MarkupLine($"[grey]Total: {total}, Pending: {pending}, Skipped: {total - pending}[/]");
    }
    
    public void UpdateProgress(int current, string itemName)
    {
        _console.MarkupLine($"  [grey]Processing:[/] {Markup.Escape(itemName)}");
    }
    
    public void UpdateStats(PipelineStats stats)
    {
        // Could update a live display here
    }
    
    public void DisplayFinalStats(PipelineStats stats)
    {
        _console.WriteLine();
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");
        
        table.AddRow("Total Processed", stats.TotalProcessed.ToString());
        table.AddRow("Successful", $"[green]{stats.Successful}[/]");
        table.AddRow("Failed", $"[red]{stats.Failed}[/]");
        table.AddRow("Cache Hits", $"[blue]{stats.CacheHits}[/]");
        table.AddRow("Cache Hit Rate", $"{stats.CacheHitRate:P1}");
        table.AddRow("Total LLM Time", stats.TotalLlmTime.ToString(@"hh\:mm\:ss"));
        table.AddRow("Avg Response Time", $"{stats.AverageResponseTime.TotalSeconds:F1}s");
        table.AddRow("Total Tokens", $"{stats.TotalPromptTokens + stats.TotalCompletionTokens:N0}");
        
        _console.Write(table);
    }
}
```

### 10.2 Pipeline Stats (Models/PipelineStats.cs)

```csharp
namespace ACSourceHallucinator.Models;

public class PipelineStats
{
    public int TotalProcessed { get; private set; }
    public int Successful { get; private set; }
    public int Failed { get; private set; }
    public int CacheHits { get; private set; }
    public TimeSpan TotalLlmTime { get; private set; }
    public int TotalPromptTokens { get; private set; }
    public int TotalCompletionTokens { get; private set; }
    
    public double CacheHitRate => TotalProcessed > 0 
        ? (double)CacheHits / TotalProcessed 
        : 0;
    
    public TimeSpan AverageResponseTime => TotalProcessed - CacheHits > 0
        ? TotalLlmTime / (TotalProcessed - CacheHits)
        : TimeSpan.Zero;
    
    public void Record(StageResult result, LlmResponse? response = null)
    {
        TotalProcessed++;
        
        if (result.Status == StageResultStatus.Success)
            Successful++;
        else if (result.Status == StageResultStatus.Failed)
            Failed++;
        
        if (response != null)
        {
            if (response.FromCache)
                CacheHits++;
            else
                TotalLlmTime += response.ResponseTime;
            
            TotalPromptTokens += response.PromptTokens;
            TotalCompletionTokens += response.CompletionTokens;
        }
    }
}
```

---

## 11. CLI Interface

### 11.1 CLI Options (Configuration/CliOptions.cs)

```csharp
namespace ACSourceHallucinator.Configuration;

public class CliOptions
{
    // Database paths
    public required string SourceDbPath { get; init; }                      // Existing type database
    public string HallucinatorDbPath { get; init; } = "hallucinator.db";    // Results + cache
    
    // LLM configuration
    public string LlmBaseUrl { get; init; } = "http://localhost:1234/v1";
    public required string LlmModel { get; init; }
    public int MaxTokens { get; init; } = 2048;
    public double Temperature { get; init; } = 0.7;
    public int TimeoutMinutes { get; init; } = 15;
    
    // Pipeline control
    public int MaxRetries { get; init; } = 5;
    public bool SkipCache { get; init; }
    
    // Debug mode
    public string? DebugStructFqn { get; init; }
}
```

### 11.2 Program Entry Point (Program.cs)

```csharp
using System.CommandLine;
using ACSourceHallucinator.Configuration;
using ACSourceHallucinator.Data;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Llm;
using ACSourceHallucinator.Models;
using ACSourceHallucinator.Pipeline;
using ACSourceHallucinator.ReferenceGeneration;
using ACSourceHallucinator.Stages;
using ACSourceHallucinator.Tui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

// Assumes TypeContext is from the shared library
using ACShared.Data;

namespace ACSourceHallucinator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("ACSourceHallucinator - LLM-powered decompiled code enhancement");
        
        var sourceDbOption = new Option<string>(
            "--source-db",
            "Path to the source type database")
        { IsRequired = true };
        
        var hallucinatorDbOption = new Option<string>(
            "--hallucinator-db",
            () => "hallucinator.db",
            "Path to the hallucinator results database");
        
        var llmUrlOption = new Option<string>(
            "--llm-url",
            () => "http://localhost:1234/v1",
            "Base URL for the LLM API");
        
        var modelOption = new Option<string>(
            "--model",
            "LLM model name")
        { IsRequired = true };
        
        var maxTokensOption = new Option<int>(
            "--max-tokens",
            () => 2048,
            "Maximum tokens for LLM responses");
        
        var temperatureOption = new Option<double>(
            "--temperature",
            () => 0.7,
            "LLM temperature");
        
        var timeoutOption = new Option<int>(
            "--timeout",
            () => 15,
            "LLM request timeout in minutes");
        
        var maxRetriesOption = new Option<int>(
            "--max-retries",
            () => 5,
            "Maximum retry attempts per item");
        
        var skipCacheOption = new Option<bool>(
            "--skip-cache",
            "Skip the LLM response cache");
        
        var debugStructOption = new Option<string?>(
            "--debug-struct",
            "Process only a single struct by FullyQualifiedName");
        
        rootCommand.AddOption(sourceDbOption);
        rootCommand.AddOption(hallucinatorDbOption);
        rootCommand.AddOption(llmUrlOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(maxTokensOption);
        rootCommand.AddOption(temperatureOption);
        rootCommand.AddOption(timeoutOption);
        rootCommand.AddOption(maxRetriesOption);
        rootCommand.AddOption(skipCacheOption);
        rootCommand.AddOption(debugStructOption);
        
        rootCommand.SetHandler(async (context) =>
        {
            var options = new CliOptions
            {
                SourceDbPath = context.ParseResult.GetValueForOption(sourceDbOption)!,
                HallucinatorDbPath = context.ParseResult.GetValueForOption(hallucinatorDbOption)!,
                LlmBaseUrl = context.ParseResult.GetValueForOption(llmUrlOption)!,
                LlmModel = context.ParseResult.GetValueForOption(modelOption)!,
                MaxTokens = context.ParseResult.GetValueForOption(maxTokensOption),
                Temperature = context.ParseResult.GetValueForOption(temperatureOption),
                TimeoutMinutes = context.ParseResult.GetValueForOption(timeoutOption),
                MaxRetries = context.ParseResult.GetValueForOption(maxRetriesOption),
                SkipCache = context.ParseResult.GetValueForOption(skipCacheOption),
                DebugStructFqn = context.ParseResult.GetValueForOption(debugStructOption)
            };
            
            await RunPipelineAsync(options, context.GetCancellationToken());
        });
        
        return await rootCommand.InvokeAsync(args);
    }
    
    private static async Task RunPipelineAsync(CliOptions options, CancellationToken ct)
    {
        var services = ConfigureServices(options);
        
        // Ensure hallucinator database is created
        using (var scope = services.CreateScope())
        {
            var hallucinatorDb = scope.ServiceProvider.GetRequiredService<HallucinatorDbContext>();
            await hallucinatorDb.Database.EnsureCreatedAsync(ct);
        }
        
        using var scope2 = services.CreateScope();
        var provider = scope2.ServiceProvider;
        
        var pipeline = new PipelineBuilder(provider)
            .AddStage<CommentFunctionsStage>()
            .AddStage<CommentStructsStage>()
            .AddStage<CommentEnumsStage>()
            // Add more stages as needed
            .Build();
        
        var pipelineOptions = new PipelineOptions
        {
            DebugFilterFqn = options.DebugStructFqn,
            SkipCache = options.SkipCache,
            MaxRetries = options.MaxRetries,
            Model = options.LlmModel
        };
        
        var runner = provider.GetRequiredService<PipelineRunner>();
        await runner.RunAsync(pipeline, pipelineOptions, ct);
    }
    
    private static ServiceProvider ConfigureServices(CliOptions options)
    {
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Database contexts
        services.AddDbContext<TypeContext>(opt =>
            opt.UseSqlite($"Data Source={options.SourceDbPath}"));
        
        services.AddDbContext<HallucinatorDbContext>(opt =>
            opt.UseSqlite($"Data Source={options.HallucinatorDbPath}"));
        
        // LLM client options
        services.Configure<LlmClientOptions>(opt =>
        {
            opt.BaseUrl = options.LlmBaseUrl;
            opt.MaxTokens = options.MaxTokens;
            opt.Temperature = options.Temperature;
            opt.TimeoutMinutes = options.TimeoutMinutes;
            opt.SkipCache = options.SkipCache;
        });
        
        services.AddHttpClient<ILlmClient, OpenAiCompatibleClient>();
        services.AddScoped<ILlmCache, LlmCache>();
        
        // Repositories
        services.AddScoped<IStageResultRepository, StageResultRepository>();
        
        // Reference generation
        services.AddScoped<IReferenceTextGenerator, ReferenceTextGenerator>();
        
        // Stages
        services.AddScoped<CommentFunctionsStage>();
        services.AddScoped<CommentStructsStage>();
        services.AddScoped<CommentEnumsStage>();
        
        // Pipeline
        services.AddScoped<PipelineRunner>();
        
        // TUI
        services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
        services.AddScoped<PipelineTui>();
        
        return services.BuildServiceProvider();
    }
}
```

---

## 12. Configuration

### 12.1 appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

---

## 13. Example Stage Implementation

### 13.1 CommentFunctionsStage (Stages/CommentFunctionsStage.cs)

Complete implementation of the function commenting stage with proper async patterns.

```csharp
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
            .Where(f => f.ParentId != null)           // Methods only, not free functions
            .Where(f => !f.ParentType!.IsIgnored);    // Exclude ignored types
        
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
            FullyQualifiedName = $"{f.ParentType?.FullyQualifiedName}::{f.FunctionSignature?.Name}",
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
            .WithInput(function.Source);
        
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
        
        return $@"You are a code review assistant. Verify whether the following comment accurately describes the function.

=== REFERENCES ===
{references}

=== FUNCTION ===
{function.Source}

=== GENERATED COMMENT ===
{generatedContent}

Respond with a JSON object in exactly this format:
{{
    ""valid"": true/false,
    ""reason"": ""explanation if invalid, or 'OK' if valid""
}}

Only respond with the JSON object, no other text.";
    }
    
    private const string SystemPrompt = @"You are an expert C++ code analyst. Your task is to generate a concise, informative comment for decompiled C++ functions.

Guidelines:
- Focus on WHAT the function does, not HOW it does it
- Mention key parameters and return values if relevant
- Keep comments to 1-4 sentences
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
```

---

## 14. Data Flow Diagrams

### 14.1 Overall Pipeline Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              CLI Entry Point                                 │
│                                                                              │
│  dotnet run -- --source-db types.db --model llama3 --debug-struct MyClass   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                             Pipeline Builder                                 │
│                                                                              │
│  Registers stages in order, validates dependencies                          │
│  [CommentFunctions] → [CommentStructs] → [CommentEnums] → [CleanupCode]     │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                             Pipeline Runner                                  │
│                                                                              │
│  For each stage:                                                             │
│    1. Collect work items (with debug filter if set)                         │
│    2. Filter out completed items (resumability)                             │
│    3. Process each item with progress tracking                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
                        ┌─────────────────────────┐
                        │      TUI Display        │
                        │                         │
                        │  Stage: CommentFuncs    │
                        │  [████████░░] 80%       │
                        │  Processing: Foo::Bar   │
                        │  Cache hits: 142        │
                        └─────────────────────────┘
```

### 14.2 Single Work Item Processing Flow

```
┌──────────────────┐
│    WorkItem      │
│  (FunctionBody)  │
└────────┬─────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────┐
│                    Reference Generator                            │
│                        (async)                                    │
│  Collects: Parent struct, param types, return type, base types   │
│  Formats into structured sections                                 │
└──────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────┐
│                     Prompt Builder                                │
│                                                                   │
│  [System Message]                                                 │
│  [References]                                                     │
│  [Retry Feedback?]                                                │
│  [Few-Shot Examples]                                              │
│  [Input: function source]                                         │
└──────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────┐
│                       LLM Cache                                   │
│                        (async)                                    │
│  Check: SHA256(model + temp + prompt) → cached response?          │
└──────────────────────────────────────────────────────────────────┘
         │
         ├─── Cache Hit ───▶ Return cached response
         │
         ▼ Cache Miss
┌──────────────────────────────────────────────────────────────────┐
│                       LLM Client                                  │
│                        (async)                                    │
│  POST /v1/chat/completions                                        │
│  { model, messages: [{role: user, content: prompt}], ... }        │
└──────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────┐
│                 VerifyResponseFormat()                            │
│                        (sync)                                     │
│  Stage-specific validation (length > 10, etc.)                    │
│  If fails → retry with feedback                                   │
└──────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────┐
│                  LLM Verification (Optional)                      │
│                        (async)                                    │
│  Separate prompt asking LLM to verify comment validity            │
│  Expects JSON: { "valid": bool, "reason": string }                │
│  If JSON parse fails → retry verification                         │
│  If valid=false → retry generation with reason                    │
└──────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────┐
│                    Save StageResult                               │
│                        (async)                                    │
│  Status: Success/Failed                                           │
│  GeneratedContent: the comment text                               │
│  RetryCount, LastFailureReason                                    │
└──────────────────────────────────────────────────────────────────┘
```

---

## 15. Async Design Notes

### 15.1 Async Methods Summary

| Method | Async? | Reason |
|--------|--------|--------|
| `CollectWorkItemsAsync` | Yes | Database queries |
| `ProcessWorkItemAsync` | Yes | Orchestrates async operations |
| `BuildPromptAsync` | Yes | Database queries + reference generation |
| `VerifyResponseFormat` | No | Simple string validation only |
| `BuildLlmVerificationPromptAsync` | Yes | Database queries + reference generation |
| `ParseLlmVerificationResponse` | No | JSON parsing only |
| `SendRequestAsync` (LLM) | Yes | HTTP calls |
| `GetAsync` / `SetAsync` (Cache) | Yes | Database queries |
| All `IReferenceTextGenerator` methods | Yes | Database queries + result repo queries |

### 15.2 Key Design Principles

1. **No blocking calls in async context**: All database and I/O operations use async/await properly
2. **CancellationToken propagation**: All async methods accept and pass through CancellationToken
3. **Sync methods stay sync**: `VerifyResponseFormat` and `ParseLlmVerificationResponse` do simple CPU-bound work and remain synchronous
4. **Clear async suffixes**: All async methods end with `Async` for clarity

---

## Open Questions / Future Considerations

1. **Batch Processing**: Could batch multiple functions into a single LLM call for efficiency, but adds complexity for retry handling.

2. **Async Parallelism**: Architecture supports adding parallelism later by processing multiple work items concurrently (with semaphore for rate limiting).

3. **Export**: May want a stage/tool to export all comments back into the source database or generate annotated source files.

4. **Prompt Versioning**: If prompt templates change significantly, may want to version them and invalidate cache entries for old versions.

5. **Quality Metrics**: Could track metrics like average comment length, verification pass rate per stage, etc. for quality monitoring.

---

## Acceptance Criteria

- [ ] Pipeline runs end-to-end with at least CommentFunctions stage
- [ ] Progress TUI shows real-time updates
- [ ] Cache prevents duplicate LLM calls
- [ ] Resumability works (restart continues from where it left off)
- [ ] Debug mode filters to single struct
- [ ] Failed items are recorded with failure reasons
- [ ] Final stats displayed on completion
- [ ] CLI accepts all documented options