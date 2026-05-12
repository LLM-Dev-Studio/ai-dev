namespace AiDevNet.Tests.Unit;

public class TestOutcomeTests
{
    [Fact]
    public void From_WhenPassed_ReturnsPassed()
    {
        var outcome = TestOutcome.From("passed");

        outcome.ShouldBe(TestOutcome.Passed);
        outcome.IsPassed.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenFailed_ReturnsFailed()
    {
        var outcome = TestOutcome.From("failed");

        outcome.ShouldBe(TestOutcome.Failed);
        outcome.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenSkipped_ReturnsSkipped()
    {
        var outcome = TestOutcome.From("skipped");

        outcome.ShouldBe(TestOutcome.Skipped);
        outcome.IsSkipped.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    public void From_WhenInvalid_ThrowsArgumentException(string? value)
    {
        Should.Throw<ArgumentException>(() => TestOutcome.From(value));
    }

    [Fact]
    public void JsonRoundTrip_SerializesAsString()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(TestOutcome.Passed);
        json.ShouldBe("\"passed\"");

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TestOutcome>(json);
        deserialized.ShouldBe(TestOutcome.Passed);
    }
}
