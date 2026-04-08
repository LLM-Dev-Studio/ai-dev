namespace AiDevNet.Tests.Unit;

public class ColumnIdTests
{
    [Fact]
    public void From_WhenKnownValue_ReturnsSharedInstance()
    {
        var columnId = ColumnId.From("done");

        columnId.ShouldBe(ColumnId.Done);
    }

    [Fact]
    public void From_WhenCustomValue_ReturnsCustomColumn()
    {
        var columnId = ColumnId.From("triage");

        columnId.Value.ShouldBe("triage");
    }

    [Fact]
    public void From_WhenBlank_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => ColumnId.From(" "));
    }
}
