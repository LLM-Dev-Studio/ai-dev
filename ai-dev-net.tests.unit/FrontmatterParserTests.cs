namespace AiDevNet.Tests.Unit;

public class FrontmatterParserTests
{
    // -------------------------------------------------------------------------
    // Parse — happy paths
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_WellFormedFrontmatter_ExtractsFieldsAndBody()
    {
        var content = "---\nfrom: alice\nre: hello\n---\nThis is the body.";

        var (fields, body) = FrontmatterParser.Parse(content);

        fields["from"].ShouldBe("alice");
        fields["re"].ShouldBe("hello");
        body.ShouldBe("This is the body.");
    }

    [Fact]
    public void Parse_FieldsAreCaseInsensitive()
    {
        var content = "---\nFrom: alice\n---\nbody";

        var (fields, _) = FrontmatterParser.Parse(content);

        fields["FROM"].ShouldBe("alice");
        fields["from"].ShouldBe("alice");
    }

    [Fact]
    public void Parse_NoFrontmatter_ReturnsEmptyFieldsAndFullContent()
    {
        var content = "Just a plain body with no frontmatter.";

        var (fields, body) = FrontmatterParser.Parse(content);

        fields.ShouldBeEmpty();
        body.ShouldBe(content);
    }

    [Fact]
    public void Parse_FrontmatterWithNoBody_ReturnsEmptyBody()
    {
        var content = "---\nkey: value\n---";

        var (fields, body) = FrontmatterParser.Parse(content);

        fields["key"].ShouldBe("value");
        body.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_ColonInValue_IsPreserved()
    {
        var content = "---\ndate: 2024-01-01T12:00:00Z\n---\nbody";

        var (fields, _) = FrontmatterParser.Parse(content);

        fields["date"].ShouldBe("2024-01-01T12:00:00Z");
    }

    [Fact]
    public void Parse_WindowsLineEndings_NormalisedToUnix()
    {
        var content = "---\r\nfrom: bob\r\n---\r\nHello";

        var (fields, body) = FrontmatterParser.Parse(content);

        fields["from"].ShouldBe("bob");
        body.ShouldBe("Hello");
    }

    [Fact]
    public void Parse_LeadingNewlinesInBodyStripped()
    {
        var content = "---\nkey: v\n---\n\n\nActual body";

        var (_, body) = FrontmatterParser.Parse(content);

        body.ShouldBe("Actual body");
    }

    // -------------------------------------------------------------------------
    // Stringify
    // -------------------------------------------------------------------------

    [Fact]
    public void Stringify_ProducesValidFrontmatter()
    {
        var fields = new Dictionary<string, string> { ["from"] = "alice", ["re"] = "test" };
        var body = "Hello world";

        var result = FrontmatterParser.Stringify(fields, body);

        result.ShouldStartWith("---");
        result.ShouldContain("from: alice");
        result.ShouldContain("re: test");
        result.ShouldContain("Hello world");
    }

    [Fact]
    public void Stringify_EmptyBody_NoBodySection()
    {
        var fields = new Dictionary<string, string> { ["key"] = "val" };

        var result = FrontmatterParser.Stringify(fields, string.Empty);

        // After the closing ---, there should be no extra content
        var afterClose = result[(result.LastIndexOf("---", StringComparison.Ordinal) + 3)..].Trim();
        afterClose.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void RoundTrip_StringifyThenParse_RestoresData()
    {
        var original = new Dictionary<string, string>
        {
            ["from"] = "system",
            ["re"] = "task update",
            ["priority"] = "high",
            ["task-id"] = "task-123-abcde",
        };
        var originalBody = "Please review this task.";

        var serialised = FrontmatterParser.Stringify(original, originalBody);
        var (fields, body) = FrontmatterParser.Parse(serialised);

        fields["from"].ShouldBe("system");
        fields["re"].ShouldBe("task update");
        fields["priority"].ShouldBe("high");
        fields["task-id"].ShouldBe("task-123-abcde");
        body.ShouldBe(originalBody);
    }
}
