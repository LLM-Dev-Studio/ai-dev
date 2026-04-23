using AiDev.Features.Planning;

namespace AiDevNet.Tests.Unit;

/// <summary>
/// Tests for token-based session limits (32k soft / 40k hard per phase).
/// </summary>
public class TokenStatusTests
{
    [Fact]
    public void BelowSoftThreshold_NoWarning()
    {
        var status = TokenStatus.From(10_000);
        status.WarningLevel.ShouldBe(TokenWarningLevel.None);
        status.IsWarning.ShouldBeFalse();
        status.IsAtHardLimit.ShouldBeFalse();
    }

    [Fact]
    public void AtSoftThreshold_SoftWarning()
    {
        var status = TokenStatus.From(TokenStatus.SoftWarningThreshold);
        status.WarningLevel.ShouldBe(TokenWarningLevel.Soft);
        status.IsWarning.ShouldBeTrue();
        status.IsAtHardLimit.ShouldBeFalse();
    }

    [Fact]
    public void AboveSoftBelowHard_SoftWarning()
    {
        var status = TokenStatus.From(35_000);
        status.WarningLevel.ShouldBe(TokenWarningLevel.Soft);
    }

    [Fact]
    public void AtHardThreshold_HardLimit()
    {
        var status = TokenStatus.From(TokenStatus.HardLimitThreshold);
        status.WarningLevel.ShouldBe(TokenWarningLevel.Hard);
        status.IsAtHardLimit.ShouldBeTrue();
        status.IsWarning.ShouldBeTrue();
    }

    [Fact]
    public void AboveHardThreshold_HardLimit()
    {
        var status = TokenStatus.From(50_000);
        status.WarningLevel.ShouldBe(TokenWarningLevel.Hard);
        status.IsAtHardLimit.ShouldBeTrue();
    }

    [Fact]
    public void TotalTokens_IsPreserved()
    {
        var status = TokenStatus.From(25_000);
        status.TotalTokens.ShouldBe(25_000);
    }
}
