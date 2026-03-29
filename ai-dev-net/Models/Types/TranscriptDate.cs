namespace AiDevNet.Models.Types;

/// <summary>
/// A validated transcript date in yyyy-MM-dd format.
/// </summary>
public sealed record TranscriptDate : IComparable<TranscriptDate>
{
    internal const string Format = "yyyy-MM-dd";

    public string Value { get; }

    public TranscriptDate(string value)
    {
        if (!DateOnly.TryParseExact(value, Format, out _))
            throw new ArgumentException(
                $"Invalid transcript date '{value}'. Must be in {Format} format.",
                nameof(value));
        Value = value;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out TranscriptDate? date)
    {
        if (value != null && DateOnly.TryParseExact(value, Format, out _))
        { date = new(value); return true; }
        date = null;
        return false;
    }

    /// <summary>Creates a TranscriptDate from a DateTime using UTC date.</summary>
    public static TranscriptDate From(DateTime dt) =>
        new(DateOnly.FromDateTime(dt).ToString(Format));

    public static TranscriptDate Today => From(DateTime.UtcNow);

    public bool IsToday => Value == Today.Value;

    public DateOnly ToDateOnly() => DateOnly.ParseExact(Value, Format);

    public int CompareTo(TranscriptDate? other) =>
        other == null ? 1 : string.Compare(Value, other.Value, StringComparison.Ordinal);

    public static implicit operator string(TranscriptDate date) => date.Value;
    public static implicit operator TranscriptDate(string value) => new(value);

    public override string ToString() => Value;
}
