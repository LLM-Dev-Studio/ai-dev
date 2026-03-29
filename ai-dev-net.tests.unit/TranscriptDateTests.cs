namespace AiDevNet.Tests.Unit;

public class TranscriptDateTests
{
    // -------------------------------------------------------------------------
    // Construction & validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("2024-01-01")]
    [InlineData("2000-12-31")]
    [InlineData("2025-06-15")]
    public void Constructor_ValidFormat_Succeeds(string value)
    {
        var d = new TranscriptDate(value);
        d.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("01-01-2024")]    // wrong order
    [InlineData("2024/01/01")]    // wrong separator
    [InlineData("2024-1-1")]      // no zero-padding
    [InlineData("not-a-date")]
    public void Constructor_InvalidFormat_Throws(string? value)
    {
        Should.Throw<ArgumentException>(() => new TranscriptDate(value!));
    }

    // -------------------------------------------------------------------------
    // TryParse
    // -------------------------------------------------------------------------

    [Fact]
    public void TryParse_ValidValue_ReturnsTrueAndDate()
    {
        var result = TranscriptDate.TryParse("2024-06-15", out var date);
        result.ShouldBeTrue();
        date!.Value.ShouldBe("2024-06-15");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bad")]
    [InlineData("2024-6-15")]
    public void TryParse_InvalidValue_ReturnsFalse(string? value)
    {
        TranscriptDate.TryParse(value, out var date).ShouldBeFalse();
        date.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // From(DateTime)
    // -------------------------------------------------------------------------

    [Fact]
    public void From_DateTime_ProducesCorrectDateString()
    {
        var dt = new DateTime(2025, 3, 7, 14, 30, 0, DateTimeKind.Utc);
        var d = TranscriptDate.From(dt);
        d.Value.ShouldBe("2025-03-07");
    }

    [Fact]
    public void From_MidnightUtc_ProducesCorrectDate()
    {
        var dt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        TranscriptDate.From(dt).Value.ShouldBe("2025-01-01");
    }

    // -------------------------------------------------------------------------
    // Today
    // -------------------------------------------------------------------------

    [Fact]
    public void Today_MatchesUtcNowDate()
    {
        var expected = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        TranscriptDate.Today.Value.ShouldBe(expected);
    }

    // -------------------------------------------------------------------------
    // IsToday
    // -------------------------------------------------------------------------

    [Fact]
    public void IsToday_WhenToday_ReturnsTrue()
    {
        TranscriptDate.Today.IsToday.ShouldBeTrue();
    }

    [Fact]
    public void IsToday_WhenPastDate_ReturnsFalse()
    {
        new TranscriptDate("2000-01-01").IsToday.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // ToDateOnly
    // -------------------------------------------------------------------------

    [Fact]
    public void ToDateOnly_ReturnsCorrectValue()
    {
        var d = new TranscriptDate("2024-06-15");
        d.ToDateOnly().ShouldBe(new DateOnly(2024, 6, 15));
    }

    // -------------------------------------------------------------------------
    // CompareTo
    // -------------------------------------------------------------------------

    [Fact]
    public void CompareTo_Earlier_ReturnsNegative()
    {
        var earlier = new TranscriptDate("2024-01-01");
        var later = new TranscriptDate("2024-12-31");
        earlier.CompareTo(later).ShouldBeLessThan(0);
    }

    [Fact]
    public void CompareTo_Later_ReturnsPositive()
    {
        var earlier = new TranscriptDate("2024-01-01");
        var later = new TranscriptDate("2024-12-31");
        later.CompareTo(earlier).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_Same_ReturnsZero()
    {
        var a = new TranscriptDate("2024-06-15");
        var b = new TranscriptDate("2024-06-15");
        a.CompareTo(b).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_Null_ReturnsPositive()
    {
        new TranscriptDate("2024-01-01").CompareTo(null).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void SortOrder_IsChronological()
    {
        var dates = new[]
        {
            new TranscriptDate("2024-12-31"),
            new TranscriptDate("2024-01-01"),
            new TranscriptDate("2024-06-15"),
        };

        var sorted = dates.Order().ToList();
        sorted[0].Value.ShouldBe("2024-01-01");
        sorted[1].Value.ShouldBe("2024-06-15");
        sorted[2].Value.ShouldBe("2024-12-31");
    }

    // -------------------------------------------------------------------------
    // Equality (record semantics)
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        new TranscriptDate("2024-06-15").ShouldBe(new TranscriptDate("2024-06-15"));
    }

    [Fact]
    public void Equality_DifferentValue_NotEqual()
    {
        new TranscriptDate("2024-06-15").ShouldNotBe(new TranscriptDate("2024-06-16"));
    }

    // -------------------------------------------------------------------------
    // Conversions
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsValue()
    {
        new TranscriptDate("2024-06-15").ToString().ShouldBe("2024-06-15");
    }

    [Fact]
    public void ImplicitToString_ReturnsValue()
    {
        TranscriptDate d = new("2024-06-15");
        string s = d;
        s.ShouldBe("2024-06-15");
    }

    [Fact]
    public void ImplicitFromString_CreatesDate()
    {
        TranscriptDate d = "2024-06-15";
        d.Value.ShouldBe("2024-06-15");
    }
}
