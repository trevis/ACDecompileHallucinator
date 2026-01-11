using System.Security.Cryptography;
using System.Text;
using ACSourceHallucinator.Data;
using ACSourceHallucinator.Data.Entities;
using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Models;
using Microsoft.EntityFrameworkCore;

namespace ACSourceHallucinator.Llm;

public class LlmCache : ILlmCache
{
    private readonly LlmCacheDbContext _db;

    public LlmCache(LlmCacheDbContext db)
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
