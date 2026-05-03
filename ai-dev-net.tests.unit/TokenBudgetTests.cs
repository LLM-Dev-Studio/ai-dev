namespace AiDevNet.Tests.Unit;

public class TokenBudgetTests
{
    [Fact]
    public void EstimateTokens_EmptyOrNull_ReturnsZero()
    {
        TokenBudget.EstimateTokens(null).ShouldBe(0);
        TokenBudget.EstimateTokens(string.Empty).ShouldBe(0);
    }

    [Fact]
    public void EstimateTokens_RoundsUp()
    {
        // 5 chars / 4 = 1.25 => 2 tokens (ceiling division)
        TokenBudget.EstimateTokens("hello").ShouldBe(2);
        // 4 chars => exactly 1 token
        TokenBudget.EstimateTokens("test").ShouldBe(1);
        // 1 char => still 1 token
        TokenBudget.EstimateTokens("x").ShouldBe(1);
    }

    [Fact]
    public void EstimateMessagesTokens_IncludesPerMessageOverhead()
    {
        var messages = new[] { "hello", "world" };
        // 2+2 = 4 tokens of content + 2*8 = 16 overhead = 20
        TokenBudget.EstimateMessagesTokens(messages).ShouldBe(20);
    }

    [Fact]
    public void Preflight_WithinBudget_Fits()
    {
        var result = TokenBudget.Preflight(
            contextWindow:   8192,
            maxOutputTokens: 1024,
            messageContents: new[] { "hello world" },
            toolsJson:       null,
            modelId:         "test-model",
            executorName:    "Test");

        result.Fits.ShouldBeTrue();
        result.Error.ShouldBeNull();
        result.Required.ShouldBeLessThan(8192);
    }

    [Fact]
    public void Preflight_OverflowsBudget_FailsWithErrorMessage()
    {
        // 4096 context with 4096 output reservation alone already exceeds the window.
        var result = TokenBudget.Preflight(
            contextWindow:   4096,
            maxOutputTokens: 4096,
            messageContents: new[] { new string('a', 4000) },  // ~1000 tokens
            toolsJson:       new string('t', 8000),            // ~2000 tokens
            modelId:         "qwen/qwen3.5-9b",
            executorName:    "LM Studio");

        result.Fits.ShouldBeFalse();
        result.Required.ShouldBeGreaterThan(4096);
        result.Error.ShouldNotBeNullOrEmpty();
        result.Error!.ShouldContain("qwen/qwen3.5-9b");
        result.Error.ShouldContain("LM Studio");
        result.Error.ShouldContain("context_length=4096");
    }

    [Fact]
    public void Preflight_UnknownContextWindow_Fits()
    {
        // contextWindow == 0 means "unknown" — skip the check.
        var result = TokenBudget.Preflight(
            contextWindow:   0,
            maxOutputTokens: 4096,
            messageContents: new[] { new string('a', 1_000_000) },
            toolsJson:       null,
            modelId:         "unknown-model",
            executorName:    "Test");

        result.Fits.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Theory]
    [InlineData(100,    2048)]    // small requests round up to the 2048 floor
    [InlineData(4000,   8192)]    // ~5000 with 25% headroom -> next power-of-2 is 8192
    [InlineData(30000,  65536)]   // ~37500 with headroom -> 65536
    [InlineData(150000, 131072)]  // clamped at the 131072 ceiling
    public void SuggestContextWindow_RoundsUpToPowerOfTwo(int required, int expected)
    {
        TokenBudget.SuggestContextWindow(required).ShouldBe(expected);
    }

    [Fact]
    public void RecommendMaxOutputTokens_UnknownContext_ReturnsCeiling()
    {
        TokenBudget.RecommendMaxOutputTokens(0, floor: 512, ceiling: 4096).ShouldBe(4096);
    }

    [Fact]
    public void RecommendMaxOutputTokens_ClampsToFloorAndCeiling()
    {
        // 2048 / 4 = 512 (hits floor)
        TokenBudget.RecommendMaxOutputTokens(2048).ShouldBe(512);
        // 32768 / 4 = 8192, clamped to default ceiling 4096
        TokenBudget.RecommendMaxOutputTokens(32768).ShouldBe(4096);
        // 8192 / 4 = 2048, within bounds
        TokenBudget.RecommendMaxOutputTokens(8192).ShouldBe(2048);
    }

    [Fact]
    public void SelectSystemPrompt_ContextWindowBelowThreshold_ReturnsCompact()
    {
        var result = TokenBudget.SelectSystemPrompt(
            contextWindow: 8192, fullPrompt: "full", compactPrompt: "compact", threshold: 16384);

        result.ShouldBe("compact");
    }

    [Fact]
    public void SelectSystemPrompt_ContextWindowAtThreshold_ReturnsFull()
    {
        var result = TokenBudget.SelectSystemPrompt(
            contextWindow: 16384, fullPrompt: "full", compactPrompt: "compact", threshold: 16384);

        result.ShouldBe("full");
    }

    [Fact]
    public void SelectSystemPrompt_ContextWindowAboveThreshold_ReturnsFull()
    {
        var result = TokenBudget.SelectSystemPrompt(
            contextWindow: 32768, fullPrompt: "full", compactPrompt: "compact", threshold: 16384);

        result.ShouldBe("full");
    }

    [Fact]
    public void SelectSystemPrompt_UnknownContextWindow_ReturnsFull()
    {
        var result = TokenBudget.SelectSystemPrompt(
            contextWindow: 0, fullPrompt: "full", compactPrompt: "compact", threshold: 16384);

        result.ShouldBe("full");
    }

    [Fact]
    public void CanFitCompact_PromptAndOutputFitInWindow_ReturnsTrue()
    {
        // compact prompt = 400 chars = 100 tokens, output = 512, margin = 64 => 676 < 8192
        var compact = new string('x', 400);

        TokenBudget.CanFitCompact(8192, compact, maxOutputTokens: 512).ShouldBeTrue();
    }

    [Fact]
    public void CanFitCompact_PromptAndOutputExceedWindow_ReturnsFalse()
    {
        // compact = 16000 chars = 4000 tokens, output = 4096, margin = 64 => 8160 > 4096
        var compact = new string('x', 16_000);

        TokenBudget.CanFitCompact(4096, compact, maxOutputTokens: 4096).ShouldBeFalse();
    }

    [Fact]
    public void CanFitCompact_UnknownContextWindow_ReturnsTrue()
    {
        var compact = new string('x', 100_000);

        TokenBudget.CanFitCompact(0, compact, maxOutputTokens: 4096).ShouldBeTrue();
    }
}
