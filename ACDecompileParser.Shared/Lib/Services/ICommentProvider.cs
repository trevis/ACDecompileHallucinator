namespace ACDecompileParser.Shared.Lib.Services;

/// <summary>
/// Provides XML documentation comments for types and methods.
/// </summary>
public interface ICommentProvider
{
    /// <summary>
    /// Gets a comment for an Enum.
    /// </summary>
    Task<string?> GetEnumCommentAsync(int typeId);

    /// <summary>
    /// Gets a comment for a Struct or Class.
    /// </summary>
    Task<string?> GetStructCommentAsync(int typeId);

    /// <summary>
    /// Gets a comment for a method.
    /// </summary>
    Task<string?> GetMethodCommentAsync(int methodId);
}
