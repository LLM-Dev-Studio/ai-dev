namespace AiDevNet.Tests.Unit;

public class SessionStatusTests
{
    [Fact]
    public void From_WhenCompleted_ReturnsCompleted()
    {
        var status = SessionStatus.From("completed");

        status.ShouldBe(SessionStatus.Completed);
        status.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenFailed_ReturnsFailed()
    {
        var status = SessionStatus.From("failed");

        status.ShouldBe(SessionStatus.Failed);
        status.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenPartial_ReturnsPartial()
    {
        var status = SessionStatus.From("partial");

        status.ShouldBe(SessionStatus.Partial);
        status.IsPartial.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("other")]
    public void From_WhenInvalid_ThrowsArgumentException(string? value)
    {
        Should.Throw<ArgumentException>(() => SessionStatus.From(value));
    }

    [Fact]
    public void JsonRoundTrip_SerializesAsString()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(SessionStatus.Completed);
        json.ShouldBe("\"completed\"");

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SessionStatus>(json);
        deserialized.ShouldBe(SessionStatus.Completed);
    }
}
