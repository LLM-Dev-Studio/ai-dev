namespace AiDevNet.Tests.Unit;

public class TaskIdTests
{
    // -------------------------------------------------------------------------
    // Construction & validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("task-1711234567890-a3f2b")]
    [InlineData("task-0-00000")]
    [InlineData("task-9999999999999-fffff")]
    public void Constructor_ValidFormat_Succeeds(string value)
    {
        var id = new TaskId(value);
        id.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("task-abc-a3f2b")]        // digits required for middle segment
    [InlineData("task-123-ABCDE")]        // uppercase hex not allowed
    [InlineData("task-123-a3f2")]         // only 4 hex chars
    [InlineData("task-123-a3f2bc")]       // 6 hex chars
    [InlineData("123-a3f2b")]             // missing task- prefix
    [InlineData("task-123")]              // missing hex suffix
    public void Constructor_InvalidFormat_Throws(string? value)
    {
        Should.Throw<ArgumentException>(() => new TaskId(value!));
    }

    // -------------------------------------------------------------------------
    // New()
    // -------------------------------------------------------------------------

    [Fact]
    public void New_ReturnsValidTaskId()
    {
        var id = TaskId.New();
        id.Value.ShouldStartWith("task-");
        TaskId.TryParse(id.Value, out _).ShouldBeTrue();
    }

    [Fact]
    public void New_TwoCallsReturnDistinctIds()
    {
        var a = TaskId.New();
        var b = TaskId.New();
        a.ShouldNotBe(b);
    }

    // -------------------------------------------------------------------------
    // TryParse
    // -------------------------------------------------------------------------

    [Fact]
    public void TryParse_ValidValue_ReturnsTrueAndId()
    {
        var result = TaskId.TryParse("task-1234567890123-abcde", out var id);
        result.ShouldBeTrue();
        id.ShouldNotBeNull();
        id.Value.ShouldBe("task-1234567890123-abcde");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-task-id")]
    [InlineData("task-123-ZZZZZ")]
    public void TryParse_InvalidValue_ReturnsFalse(string? value)
    {
        var result = TaskId.TryParse(value, out var id);
        result.ShouldBeFalse();
        id.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Equality (record semantics)
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new TaskId("task-111-aaaaa");
        var b = new TaskId("task-111-aaaaa");
        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_NotEqual()
    {
        var a = new TaskId("task-111-aaaaa");
        var b = new TaskId("task-222-bbbbb");
        a.ShouldNotBe(b);
    }

    // -------------------------------------------------------------------------
    // Conversions
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsValue()
    {
        var id = new TaskId("task-123-abcde");
        id.ToString().ShouldBe("task-123-abcde");
    }

    [Fact]
    public void ImplicitToString_ReturnsValue()
    {
        TaskId id = new("task-123-abcde");
        string s = id;
        s.ShouldBe("task-123-abcde");
    }

    [Fact]
    public void ImplicitFromString_CreatesId()
    {
        TaskId id = "task-123-abcde";
        id.Value.ShouldBe("task-123-abcde");
    }

    // -------------------------------------------------------------------------
    // JSON round-trip (value and dictionary key)
    // -------------------------------------------------------------------------

    [Fact]
    public void JsonRoundTrip_AsValue_Succeeds()
    {
        var id = new TaskId("task-111-aaaaa");
        var json = System.Text.Json.JsonSerializer.Serialize(id);
        json.ShouldBe("\"task-111-aaaaa\"");

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TaskId>(json);
        deserialized.ShouldBe(id);
    }

    [Fact]
    public void JsonRoundTrip_AsDictionaryKey_Succeeds()
    {
        var id = new TaskId("task-111-aaaaa");
        var dict = new Dictionary<TaskId, string> { [id] = "hello" };

        var json = System.Text.Json.JsonSerializer.Serialize(dict);
        json.ShouldContain("task-111-aaaaa");

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Dictionary<TaskId, string>>(json);
        deserialized.ShouldNotBeNull();
        deserialized[id].ShouldBe("hello");
    }
}
