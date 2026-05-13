namespace AiDev.Models.Types;

/// <summary>
/// Classifies the kind of work represented by an insights session.
/// </summary>
[JsonConverter(typeof(TaskClassificationJsonConverter))]
public readonly record struct TaskClassification
{
    /// <summary>
    /// Represents feature work.
    /// </summary>
    public static readonly TaskClassification Feature = new("feature");

    /// <summary>
    /// Represents bug-fix work.
    /// </summary>
    public static readonly TaskClassification Bug = new("bug");

    /// <summary>
    /// Represents refactoring work.
    /// </summary>
    public static readonly TaskClassification Refactor = new("refactor");

    /// <summary>
    /// Represents investigation work.
    /// </summary>
    public static readonly TaskClassification Investigation = new("investigation");

    /// <summary>
    /// Represents uncategorized work.
    /// </summary>
    public static readonly TaskClassification Other = new("other");

    /// <summary>
    /// Gets the persisted task classification value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a task classification from a validated value.
    /// </summary>
    /// <param name="value">The task classification value.</param>
    public TaskClassification(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Task classification cannot be empty.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Creates a <see cref="TaskClassification"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw task classification value.</param>
    /// <returns>The parsed classification, defaulting to <see cref="Other"/>.</returns>
    public static TaskClassification From(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "feature" => Feature,
        "bug" => Bug,
        "refactor" => Refactor,
        "investigation" => Investigation,
        "other" => Other,
        _ => Other,
    };

    /// <summary>
    /// Gets a value indicating whether the classification is feature work.
    /// </summary>
    public bool IsFeature => this == Feature;

    /// <summary>
    /// Gets a value indicating whether the classification is bug work.
    /// </summary>
    public bool IsBug => this == Bug;

    /// <summary>
    /// Gets a value indicating whether the classification is refactor work.
    /// </summary>
    public bool IsRefactor => this == Refactor;

    /// <summary>
    /// Gets a value indicating whether the classification is investigation work.
    /// </summary>
    public bool IsInvestigation => this == Investigation;

    /// <summary>
    /// Gets a value indicating whether the classification is other work.
    /// </summary>
    public bool IsOther => this == Other;

    /// <summary>
    /// Converts the classification to its persisted string value.
    /// </summary>
    /// <param name="classification">The classification to convert.</param>
    public static implicit operator string(TaskClassification classification) => classification.Value;
    /// <summary>
    /// Converts a raw value to a task classification.
    /// </summary>
    /// <param name="value">The raw classification value.</param>
    public static implicit operator TaskClassification(string value) => From(value);

    public override string ToString() => Value;
}
