using ACSourceHallucinator.Enums;
using ACSourceHallucinator.Models;

namespace ACSourceHallucinator.Data.Repositories;

public interface IStageResultRepository
{
    Task<HashSet<(EntityType, string)>> GetCompletedEntityNamesAsync(string stageName);
    Task SaveResultAsync(StageResult result);
    Task<StageResult?> GetSuccessfulResultAsync(string stageName, EntityType entityType, string fullyQualifiedName);
    Task<List<StageResult>> GetResultsWithLogsAsync(EntityType entityType, string fullyQualifiedName);
}
