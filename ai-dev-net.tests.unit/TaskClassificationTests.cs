namespace AiDevNet.Tests.Unit;

public class TaskClassificationTests
{
    [Fact]
    public void From_WhenFeature_ReturnsFeature()
    {
        var classification = TaskClassification.From("feature");

        classification.ShouldBe(TaskClassification.Feature);
        classification.IsFeature.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenUnknown_ReturnsOther()
    {
        var classification = TaskClassification.From("custom");

        classification.ShouldBe(TaskClassification.Other);
        classification.IsOther.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenNull_ReturnsOther()
    {
        var classification = TaskClassification.From(null);

        classification.ShouldBe(TaskClassification.Other);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void Constructor_WhenEmpty_Throws(string value)
    {
        Should.Throw<ArgumentException>(() => new TaskClassification(value));
    }

    [Fact]
    public void JsonRoundTrip_SerializesAsString()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(TaskClassification.Refactor);
        json.ShouldBe("\"refactor\"");

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TaskClassification>(json);
        deserialized.ShouldBe(TaskClassification.Refactor);
    }
}
