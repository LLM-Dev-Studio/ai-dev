namespace AiDevNet.Tests.Unit;

public class DecisionStatusTests
{
    [Fact]
    public void From_WhenNull_ReturnsPending()
    {
        var status = DecisionStatus.From(null);

        status.ShouldBe(DecisionStatus.Pending);
    }

    [Fact]
    public void From_WhenResolved_ReturnsResolved()
    {
        var status = DecisionStatus.From("resolved");

        status.ShouldBe(DecisionStatus.Resolved);
    }

    [Fact]
    public void IsResolved_WhenPending_ReturnsFalse()
    {
        DecisionStatus.Pending.IsResolved.ShouldBeFalse();
    }
}
