namespace AiDevNet.Tests.Unit;

public class PriorityTests
{
    [Fact]
    public void From_WhenNull_ReturnsNormal()
    {
        var priority = Priority.From(null);

        priority.ShouldBe(Priority.Normal);
    }

    [Fact]
    public void From_WhenCritical_ReturnsCritical()
    {
        var priority = Priority.From("critical");

        priority.ShouldBe(Priority.Critical);
    }

    [Fact]
    public void IsUrgent_WhenHigh_ReturnsTrue()
    {
        Priority.High.IsUrgent.ShouldBeTrue();
    }
}
