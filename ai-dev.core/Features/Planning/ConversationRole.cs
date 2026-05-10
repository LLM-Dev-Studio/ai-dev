namespace AiDev.Features.Planning;

/// <summary>
/// The author of a conversation turn. Serialised as "User" / "Assistant".
/// </summary>
public readonly struct ConversationRole : IEquatable<ConversationRole>
{
    private readonly string? _key;

    private ConversationRole(string key) => _key = key;

    public static readonly ConversationRole User      = new("User");
    public static readonly ConversationRole Assistant = new("Assistant");

    /// <summary>Lowercase role string expected by LLM APIs ("user" / "assistant").</summary>
    public string ApiRole => Serialize() == "User" ? "user" : "assistant";

    public string Serialize() => _key ?? "User";

    public static ConversationRole Parse(string? value) => value switch
    {
        "User"      => User,
        "Assistant" => Assistant,
        _           => User,
    };

    public bool Equals(ConversationRole other) => Serialize() == other.Serialize();
    public override bool Equals(object? obj) => obj is ConversationRole other && Equals(other);
    public override int GetHashCode() => Serialize().GetHashCode();
    public static bool operator ==(ConversationRole left, ConversationRole right) => left.Equals(right);
    public static bool operator !=(ConversationRole left, ConversationRole right) => !left.Equals(right);
    public override string ToString() => Serialize();
}
