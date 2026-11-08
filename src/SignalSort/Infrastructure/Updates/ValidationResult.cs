namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// Outcome of validating an update manifest against this installation.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; }
    public string Reason { get; } = string.Empty;

    private ValidationResult(bool isValid, string reason)
    {
        IsValid = isValid;
        Reason = reason;
    }

    public static ValidationResult Success() => new(true, string.Empty);
    public static ValidationResult Failure(string reason) => new(false, reason);
}
