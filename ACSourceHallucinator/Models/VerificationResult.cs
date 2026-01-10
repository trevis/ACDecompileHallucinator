namespace ACSourceHallucinator.Models;

public record VerificationResult
{
    public bool IsValid { get; init; }
    public string? Reason { get; init; }
    public string? ErrorMessage { get; init; }  // Alias for Reason, used in format verification
    public bool IsFormatError { get; init; }    // True if JSON parsing failed
}
