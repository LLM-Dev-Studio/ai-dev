namespace AiDevNet.Tests.Unit;

public class WorkspacePathTests
{
    // -------------------------------------------------------------------------
    // FilePathBase — Value & implicit conversion
    // -------------------------------------------------------------------------

    [Fact]
    public void FilePath_Value_ReturnsConstructedValue()
    {
        var path = new RegistryFile("/workspace/workspaces.json");
        path.Value.ShouldBe("/workspace/workspaces.json");
    }

    [Fact]
    public void FilePath_ImplicitToString_ReturnsValue()
    {
        var path = new RegistryFile("/workspace/workspaces.json");
        string s = path;
        s.ShouldBe("/workspace/workspaces.json");
    }

    [Fact]
    public void DirPath_Value_ReturnsConstructedValue()
    {
        var path = new RootDir("/workspace");
        path.Value.ShouldBe("/workspace");
    }

    [Fact]
    public void DirPath_ImplicitToString_ReturnsValue()
    {
        var path = new RootDir("/workspace");
        string s = path;
        s.ShouldBe("/workspace");
    }

    // -------------------------------------------------------------------------
    // FullPath — delegates to Path.GetFullPath
    // -------------------------------------------------------------------------

    [Fact]
    public void FullPath_AbsolutePath_ReturnsSameNormalized()
    {
        var absolute = Path.GetFullPath("/workspace/file.json");
        var path = new RegistryFile(absolute);
        path.FullPath.ShouldBe(absolute);
    }

    // -------------------------------------------------------------------------
    // Exists() — false for paths that don't exist
    // -------------------------------------------------------------------------

    [Fact]
    public void FilePath_Exists_ReturnsFalse_ForNonExistentPath()
    {
        var path = new RegistryFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        path.Exists().ShouldBeFalse();
    }

    [Fact]
    public void DirPath_Exists_ReturnsFalse_ForNonExistentPath()
    {
        var path = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        path.Exists().ShouldBeFalse();
    }

    [Fact]
    public void DirPath_Exists_ReturnsTrue_ForExistingDirectory()
    {
        var path = new RootDir(Path.GetTempPath());
        path.Exists().ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Record equality
    // -------------------------------------------------------------------------

    [Fact]
    public void SameType_SameValue_AreEqual()
    {
        new RootDir("/workspace").ShouldBe(new RootDir("/workspace"));
        new RegistryFile("/workspace/file.json").ShouldBe(new RegistryFile("/workspace/file.json"));
    }

    [Fact]
    public void SameType_DifferentValue_NotEqual()
    {
        new RootDir("/workspace/a").ShouldNotBe(new RootDir("/workspace/b"));
    }

    // -------------------------------------------------------------------------
    // Concrete path record types — spot checks
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("/root/agents")]
    [InlineData("relative/path")]
    public void AgentDir_PreservesValue(string value)
    {
        new AgentDir(value).Value.ShouldBe(value);
    }

    [Fact]
    public void TranscriptFile_ImplicitToString()
    {
        var path = new TranscriptFile("/agents/bob/transcripts/2024-06-15.md");
        string s = path;
        s.ShouldBe("/agents/bob/transcripts/2024-06-15.md");
    }

    [Fact]
    public void AgentInboxDir_Exists_ReturnsFalse_ForNonExistentPath()
    {
        var path = new AgentInboxDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "inbox"));
        path.Exists().ShouldBeFalse();
    }
}
