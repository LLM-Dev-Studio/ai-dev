namespace AiDevNet.Tests.Unit;

public class SessionSizeRatingTests
{
    [Fact]
    public void From_WhenSmall_ReturnsSmall()
    {
        var size = SessionSizeRating.From("small");

        size.ShouldBe(SessionSizeRating.Small);
        size.IsSmall.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenUnknown_ReturnsMedium()
    {
        var size = SessionSizeRating.From("x-large");

        size.ShouldBe(SessionSizeRating.Medium);
        size.IsMedium.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenNull_ReturnsMedium()
    {
        var size = SessionSizeRating.From(null);

        size.ShouldBe(SessionSizeRating.Medium);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void Constructor_WhenEmpty_Throws(string value)
    {
        Should.Throw<ArgumentException>(() => new SessionSizeRating(value));
    }

    [Fact]
    public void JsonRoundTrip_SerializesAsString()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(SessionSizeRating.Large);
        json.ShouldBe("\"large\"");

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SessionSizeRating>(json);
        deserialized.ShouldBe(SessionSizeRating.Large);
    }
}
