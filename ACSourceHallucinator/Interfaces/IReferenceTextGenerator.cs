namespace ACSourceHallucinator.Interfaces;

public interface IReferenceTextGenerator
{
    /// <summary>
    /// Generates formatted reference text for a struct, including members and base types.
    /// </summary>
    Task<string> GenerateStructReferenceAsync(
        string fullyQualifiedName,
        ReferenceOptions options,
        CancellationToken ct = default);

    Task<string> GenerateEnumReferenceAsync(
        string fullyQualifiedName,
        ReferenceOptions options,
        CancellationToken ct = default);

    Task<string> GenerateFunctionReferenceAsync(
        string fullyQualifiedName,
        ReferenceOptions options,
        CancellationToken ct = default);

    Task<string> GenerateReferencesForFunctionAsync(
        string fullyQualifiedName,
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

    /// <summary>Include member function signatures and comments in struct references.</summary>
    public bool IncludeMemberFunctions { get; init; } = false;

    /// <summary>Include function bodies that reference this enum (only for enums).</summary>
    public bool IncludeReferencingFunctions { get; init; } = false;

    /// <summary>Include the primary definition (source or tokens) of the type itself.</summary>
    public bool IncludeDefinition { get; init; } = true;
}
