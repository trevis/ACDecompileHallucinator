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
