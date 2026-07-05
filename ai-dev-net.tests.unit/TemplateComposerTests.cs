namespace AiDevNet.Tests.Unit;

public class TemplateComposerTests
{
    private readonly TemplateComposer _composer = new();

    [Fact]
    public void Compose_SingleInclude_ReplacesWithPartialContent()
    {
        var template = "Hello\n{{> shared/tools}}\nWorld";
        var partials = new Dictionary<string, string>
        {
            ["shared/tools"] = "## Tools\nUse MCP tools."
        };

        var result = _composer.Compose(template, partials);

        result.ShouldBe("Hello\n## Tools\nUse MCP tools.\nWorld");
    }

    [Fact]
    public void Compose_MultipleIncludes_ReplacesAll()
    {
        var template = "{{> shared/tools}}\n{{> shared/session-protocol}}";
        var partials = new Dictionary<string, string>
        {
            ["shared/tools"] = "## Tools",
            ["shared/session-protocol"] = "## Session Protocol"
        };

        var result = _composer.Compose(template, partials);

        result.ShouldBe("## Tools\n## Session Protocol");
    }

    [Fact]
    public void Compose_NestedIncludes_ResolvesTransitively()
    {
        var template = "{{> shared/outer}}";
        var partials = new Dictionary<string, string>
        {
            ["shared/outer"] = "Before\n{{> shared/inner}}\nAfter",
            ["shared/inner"] = "INNER"
        };

        var result = _composer.Compose(template, partials);

        result.ShouldBe("Before\nINNER\nAfter");
    }

    [Fact]
    public void Compose_MissingPartial_ThrowsDescriptiveException()
    {
        var template = "{{> shared/missing}}";

        var ex = Should.Throw<InvalidOperationException>(() =>
            _composer.Compose(template, new Dictionary<string, string>()));

        ex.Message.ShouldContain("shared/missing");
    }

    [Fact]
    public void Compose_NonIncludePlaceholders_AreUntouched()
    {
        var template = "You are {{name}}, a developer.";

        var result = _composer.Compose(template, new Dictionary<string, string>());

        result.ShouldBe("You are {{name}}, a developer.");
    }

    [Fact]
    public void Compose_NoIncludes_ReturnsInputUnchanged()
    {
        var template = "# Agent\n\nYou are a helpful agent.";

        var result = _composer.Compose(template, new Dictionary<string, string>());

        result.ShouldBe(template);
    }
}
