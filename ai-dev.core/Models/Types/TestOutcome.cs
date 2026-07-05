namespace AiDev.Models.Types;

/// <summary>
/// Represents the test execution result reported by a completed session.
/// </summary>
[JsonConverter(typeof(TestOutcomeJsonConverter))]
public readonly record struct TestOutcome
{
    /// <summary>
    /// Represents a passed test outcome.
    /// </summary>
    public static readonly TestOutcome Passed = new("passed");

    /// <summary>
    /// Represents a failed test outcome.
    /// </summary>
    public static readonly TestOutcome Failed = new("failed");

    /// <summary>
    /// Represents a skipped test outcome.
    /// </summary>
    public static readonly TestOutcome Skipped = new("skipped");

    /// <summary>
    /// Gets the persisted test outcome value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a test outcome from a validated value.
    /// </summary>
    /// <param name="value">The test outcome value.</param>
    public TestOutcome(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Test outcome cannot be empty.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Creates a <see cref="TestOutcome"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw test outcome value.</param>
    /// <returns>The parsed test outcome.</returns>
    public static TestOutcome From(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "passed" => Passed,
        "failed" => Failed,
        "skipped" => Skipped,
        _ => throw new ArgumentException("Test outcome must be one of: passed, failed, skipped.", nameof(value)),
    };

    /// <summary>
    /// Gets a value indicating whether the outcome is passed.
    /// </summary>
    public bool IsPassed => this == Passed;

    /// <summary>
    /// Gets a value indicating whether the outcome is failed.
    /// </summary>
    public bool IsFailed => this == Failed;

    /// <summary>
    /// Gets a value indicating whether the outcome is skipped.
    /// </summary>
    public bool IsSkipped => this == Skipped;

    /// <summary>
    /// Converts the test outcome to its persisted string value.
    /// </summary>
    /// <param name="testOutcome">The test outcome to convert.</param>
    public static implicit operator string(TestOutcome testOutcome) => testOutcome.Value;
    /// <summary>
    /// Converts a raw value to a test outcome.
    /// </summary>
    /// <param name="value">The raw test outcome value.</param>
    public static implicit operator TestOutcome(string value) => From(value);

    public override string ToString() => Value;
}
