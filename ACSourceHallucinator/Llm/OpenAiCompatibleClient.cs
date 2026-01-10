using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
            .ReadFromJsonAsync<OpenAiChatResponse>(cancellationToken: ct);

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
    public required string BaseUrl { get; set; }
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public int TimeoutMinutes { get; set; } = 15;
    public bool SkipCache { get; set; }
}

// OpenAI response DTOs
internal record OpenAiChatResponse
{
    [JsonPropertyName("choices")] public List<OpenAiChoice> Choices { get; init; } = new();

    [JsonPropertyName("usage")] public OpenAiUsage Usage { get; init; } = new();
}

internal record OpenAiChoice
{
    [JsonPropertyName("message")] public OpenAiMessage Message { get; init; } = new();
}

internal record OpenAiMessage
{
    [JsonPropertyName("content")] public string Content { get; init; } = "";
}

internal record OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }
}
