namespace AiDevNet.Tests.Unit;

public class ProjectConfigReaderTests
{
    [Fact]
    public void TryRead_WhenConfigFileExists_ReturnsProjectConfig()
    {
        var dir = TempDir();
        WriteConfig(dir, projectSlug: "my-app", apiPort: 5100);

        var result = ProjectConfigReader.TryRead(dir);

        result.ShouldNotBeNull();
        result!.ProjectSlug.ShouldBe("my-app");
        result.ApiPort.ShouldBe(5100);
    }

    [Fact]
    public void TryRead_WhenConfigFileAbsent_ReturnsNull()
    {
        var dir = TempDir();

        var result = ProjectConfigReader.TryRead(dir);

        result.ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenConfigFileMalformed_ReturnsNull()
    {
        var dir = TempDir();
        WriteRawConfig(dir, "{ not valid json }");

        var result = ProjectConfigReader.TryRead(dir);

        result.ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenConfigFileMissingRequiredFields_ReturnsNull()
    {
        var dir = TempDir();
        WriteRawConfig(dir, "{}");

        var result = ProjectConfigReader.TryRead(dir);

        result.ShouldBeNull();
    }

    [Fact]
    public void WorkspacePaths_WhenConfigPresent_UsesAiDevSubdirectoryAsRoot()
    {
        var dir = TempDir();
        WriteConfig(dir, projectSlug: "my-app", apiPort: 5100);

        var paths = ProjectConfigReader.CreateWorkspacePaths(dir, fallback: null);

        var expectedRoot = Path.Combine(dir, ".ai-dev");
        paths.Root.Value.ShouldBe(expectedRoot);
    }

    [Fact]
    public void WorkspacePaths_WhenConfigAbsent_UsesFallbackRoot()
    {
        var dir = TempDir();
        var fallback = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var paths = ProjectConfigReader.CreateWorkspacePaths(dir, fallback);

        paths.Root.ShouldBe(fallback);
    }

    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    private static void WriteConfig(string dir, string projectSlug, int apiPort)
    {
        var aiDevDir = Path.Combine(dir, ".ai-dev");
        Directory.CreateDirectory(aiDevDir);
        File.WriteAllText(
            Path.Combine(aiDevDir, "project.json"),
            $$"""{"projectSlug":"{{projectSlug}}","apiPort":{{apiPort}}}""");
    }

    private static void WriteRawConfig(string dir, string content)
    {
        var aiDevDir = Path.Combine(dir, ".ai-dev");
        Directory.CreateDirectory(aiDevDir);
        File.WriteAllText(Path.Combine(aiDevDir, "project.json"), content);
    }
}
