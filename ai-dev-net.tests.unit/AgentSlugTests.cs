namespace AiDevNet.Tests.Unit;

public class AgentSlugTests
{
    [Theory]
    [InlineData("my-agent")]
    [InlineData("agent1")]
    [InlineData("a1")]
    [InlineData("abc-123-def")]
    public void Constructor_ValidSlug_Succeeds(string value)
    {
        var slug = new AgentSlug(value);
        slug.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]               // too short (min 2 chars)
    [InlineData("-agent")]          // starts with hyphen
    [InlineData("agent-")]          // ends with hyphen
    [InlineData("My-Agent")]        // uppercase
    [InlineData("agent/sub")]       // slash not allowed
    [InlineData("agent\\sub")]      // backslash not allowed
    [InlineData("agent..sub")]      // double dot not allowed
    public void Constructor_InvalidSlug_Throws(string? value)
    {
        Should.Throw<ArgumentException>(() => new AgentSlug(value!));
    }

    [Fact]
    public void TryParse_ValidSlug_ReturnsTrueAndSlug()
    {
        var result = AgentSlug.TryParse("my-agent", out var slug);
        result.ShouldBeTrue();
        slug.ShouldNotBeNull();
        slug.Value.ShouldBe("my-agent");
    }

    [Fact]
    public void TryParse_InvalidSlug_ReturnsFalse()
    {
        var result = AgentSlug.TryParse("INVALID", out var slug);
        result.ShouldBeFalse();
        slug.ShouldBeNull();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        new AgentSlug("my-agent").ToString().ShouldBe("my-agent");
    }

    [Fact]
    public void ImplicitToString_ReturnsValue()
    {
        AgentSlug slug = new("my-agent");
        string s = slug!;
        s.ShouldBe("my-agent");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        new AgentSlug("my-agent").ShouldBe(new AgentSlug("my-agent"));
    }
}
