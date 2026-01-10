using ACSourceHallucinator.Enums;
using ACSourceHallucinator.Models;
using Microsoft.EntityFrameworkCore;

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

            // Add any new logs
            foreach (var log in result.RequestLogs)
            {
                // Only add if not already present (though result.RequestLogs should be new ones)
                // Since LlmRequestLog ID is 0 for new ones, we can just add them.
                // We need to associate them with the existing entity
                log.StageResultId = existing.Id;
                _db.LlmRequestLogs.Add(log);
            }
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

    public async Task<List<StageResult>> GetResultsWithLogsAsync(EntityType entityType, int entityId)
    {
        return await _db.StageResults
            .Include(r => r.RequestLogs)
            .Where(r => r.EntityType == entityType && r.EntityId == entityId)
            .OrderBy(r => r.StageName)
            .ToListAsync();
    }
}
