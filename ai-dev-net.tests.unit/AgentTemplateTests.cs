namespace AiDevNet.Tests.Unit;

public class AgentTemplateTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var t = new AgentTemplate();

        t.Slug.ShouldBeNull();
        t.Name.ShouldBe(string.Empty);
        t.Role.ShouldBe(string.Empty);
        t.Model.ShouldBe("sonnet");
        t.Description.ShouldBe(string.Empty);
        t.Content.ShouldBe(string.Empty);
    }

    [Fact]
    public void Properties_RetainAssignedValues()
    {
        var t = new AgentTemplate
        {
            Slug        = "backend-dev",
            Name        = "Backend Developer",
            Role        = "You are a backend developer.",
            Model       = "opus",
            Description = "Handles API work.",
            Content     = "# Backend Developer\n\nYou write APIs.",
        };

        t.Slug.Value.ShouldBe("backend-dev");
        t.Name.ShouldBe("Backend Developer");
        t.Role.ShouldBe("You are a backend developer.");
        t.Model.ShouldBe("opus");
        t.Description.ShouldBe("Handles API work.");
        t.Content.ShouldBe("# Backend Developer\n\nYou write APIs.");
    }
}
