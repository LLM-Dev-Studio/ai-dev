namespace AiDevNet.Tests.Unit;

public class SystemPromptLoaderTests
{
    private readonly string _workingDir =
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public SystemPromptLoaderTests() => Directory.CreateDirectory(_workingDir);

    private void WriteFull(string content) =>
        File.WriteAllText(Path.Combine(_workingDir, "CLAUDE.md"), content);

    private void WriteCompact(string content) =>
        File.WriteAllText(Path.Combine(_workingDir, "CLAUDE.compact.md"), content);

    [Fact]
    public void Load_ContextWindowBelowThreshold_CompactExists_ReturnsCompact()
    {
        WriteFull("Full prompt.");
        WriteCompact("Compact prompt.");

        var result = SystemPromptLoader.Load(_workingDir, contextWindow: 8192, threshold: 16384);

        result.ShouldBe("Compact prompt.");
    }

    [Fact]
    public void Load_ContextWindowAboveThreshold_ReturnsFullEvenIfCompactExists()
    {
        WriteFull("Full prompt.");
        WriteCompact("Compact prompt.");

        var result = SystemPromptLoader.Load(_workingDir, contextWindow: 32768, threshold: 16384);

        result.ShouldBe("Full prompt.");
    }

    [Fact]
    public void Load_ContextWindowBelowThreshold_NoCompactFile_ReturnsFull()
    {
        WriteFull("Full prompt.");

        var result = SystemPromptLoader.Load(_workingDir, contextWindow: 4096, threshold: 16384);

        result.ShouldBe("Full prompt.");
    }

    [Fact]
    public void Load_UnknownContextWindow_ReturnsFull()
    {
        WriteFull("Full prompt.");
        WriteCompact("Compact prompt.");

        var result = SystemPromptLoader.Load(_workingDir, contextWindow: 0, threshold: 16384);

        result.ShouldBe("Full prompt.");
    }

    [Fact]
    public void Load_NeitherFileExists_ReturnsFallback()
    {
        var result = SystemPromptLoader.Load(_workingDir, contextWindow: 4096, threshold: 16384);

        result.ShouldBe(SystemPromptLoader.Fallback);
    }

    [Fact]
    public void BuildRefusalMessage_ContainsModelIdAndContextWindow()
    {
        var msg = SystemPromptLoader.BuildRefusalMessage(
            modelId: "gemma-3-4b", contextWindow: 2048, minRequired: 4096);

        msg.ShouldContain("gemma-3-4b");
        msg.ShouldContain("2048");
        msg.ShouldContain("4096");
        msg.ShouldContain("[SESSION REFUSED]");
    }
}
