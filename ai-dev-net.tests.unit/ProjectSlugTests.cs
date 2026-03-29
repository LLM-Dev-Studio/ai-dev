namespace AiDevNet.Tests.Unit;

public class ProjectSlugTests
{
    [Theory]
    [InlineData("my-project")]
    [InlineData("proj1")]
    [InlineData("a1")]
    public void Constructor_ValidSlug_Succeeds(string value)
    {
        var slug = new ProjectSlug(value);
        slug.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("-project")]
    [InlineData("project-")]
    [InlineData("My-Project")]
    [InlineData("project/sub")]
    public void Constructor_InvalidSlug_Throws(string? value)
    {
        Should.Throw<ArgumentException>(() => new ProjectSlug(value!));
    }

    [Fact]
    public void TryParse_ValidSlug_ReturnsTrue()
    {
        ProjectSlug.TryParse("my-project", out var slug).ShouldBeTrue();
        slug!.Value.ShouldBe("my-project");
    }

    [Fact]
    public void TryParse_InvalidSlug_ReturnsFalse()
    {
        ProjectSlug.TryParse(null, out _).ShouldBeFalse();
    }
}
