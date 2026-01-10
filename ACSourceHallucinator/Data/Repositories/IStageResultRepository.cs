using ACSourceHallucinator.Enums;
using ACSourceHallucinator.Models;

namespace ACSourceHallucinator.Data.Repositories;

public interface IStageResultRepository
{
    Task<HashSet<(EntityType, int)>> GetCompletedEntityIdsAsync(string stageName);
    Task SaveResultAsync(StageResult result);
    Task<StageResult?> GetSuccessfulResultAsync(string stageName, EntityType entityType, int entityId);
}
