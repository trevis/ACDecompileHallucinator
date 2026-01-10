using ACSourceHallucinator.Enums;

namespace ACSourceHallucinator.Models;

public record WorkItem
{
    public required EntityType EntityType { get; init; }
    public required int EntityId { get; init; }
    public required string EntityName { get; init; } // For display/logging
    public required string FullyQualifiedName { get; init; } // For debug filtering
    public Dictionary<string, object> Metadata { get; init; } = new(); // Stage-specific data
}
