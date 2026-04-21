namespace AiDev.Features.Planning;

/// <summary>
/// A single constraint violation found during Solution.dsl validation (AD-10).
/// </summary>
public sealed record SolutionDslValidationError(
    /// <summary>Short rule code, e.g. "INVALID_TYPE", "MODULE_INCOMPATIBLE".</summary>
    string Rule,
    /// <summary>Human-readable explanation suitable for display in the UI.</summary>
    string Message);

/// <summary>
/// The outcome of validating a Solution.dsl YAML document against the VSA stack taxonomy.
/// </summary>
public sealed record SolutionDslValidationResult(
    bool IsValid,
    IReadOnlyList<SolutionDslValidationError> Errors)
{
    public static SolutionDslValidationResult Valid() => new(true, []);

    public static SolutionDslValidationResult Invalid(IEnumerable<SolutionDslValidationError> errors)
        => new(false, [.. errors]);
}
