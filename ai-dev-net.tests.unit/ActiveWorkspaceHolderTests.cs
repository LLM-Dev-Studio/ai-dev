namespace AiDevNet.Tests.Unit;

public class ActiveWorkspaceHolderTests
{
    [Fact]
    public void HasActiveProject_WhenNotActivated_ReturnsFalse()
    {
        var holder = new ActiveWorkspaceHolder();

        holder.HasActiveProject.ShouldBeFalse();
        holder.ActiveCodebasePath.ShouldBeNull();
    }

    [Fact]
    public void Paths_WhenNotActivated_Throws()
    {
        var holder = new ActiveWorkspaceHolder();

        Should.Throw<InvalidOperationException>(() => _ = holder.Paths);
    }

    [Fact]
    public void Activate_SetsPathsAndCodebasePath()
    {
        var holder = new ActiveWorkspaceHolder();
        var codebasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        holder.Activate(codebasePath);

        holder.HasActiveProject.ShouldBeTrue();
        holder.ActiveCodebasePath.ShouldBe(Path.GetFullPath(codebasePath));
        holder.Paths.Root.Value.ShouldBe(Path.Combine(Path.GetFullPath(codebasePath), ".ai-dev"));
    }

    [Fact]
    public void Deactivate_ClearsState()
    {
        var holder = new ActiveWorkspaceHolder();
        holder.Activate(Path.GetTempPath());

        holder.Deactivate();

        holder.HasActiveProject.ShouldBeFalse();
        holder.ActiveCodebasePath.ShouldBeNull();
    }

    [Fact]
    public void Activate_NormalizesRelativePath()
    {
        var holder = new ActiveWorkspaceHolder();
        var dir = Path.GetTempPath();

        holder.Activate(dir);

        holder.ActiveCodebasePath.ShouldBe(Path.GetFullPath(dir));
    }
}
