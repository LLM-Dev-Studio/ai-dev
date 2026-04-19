using AiDev.Features.Planning;

namespace AiDevNet.Tests.Unit;

/// <summary>
/// Tests for EC-6: Phase 1 implementation-detail keyword filter.
/// Validates that the filter correctly blocks technology keywords and allows business terms.
/// </summary>
public class Phase1KeywordFilterTests
{
    // The filter is implemented as an internal class inside AnthropicPlanningChatService.
    // We test it through the PlanningKeywordBlocklist static class which wraps the same
    // logic and is accessible from the test project.

    private static bool ContainsBlocked(string text) =>
        PlanningKeywordBlocklist.ContainsBlockedKeyword(text);

    // -------------------------------------------------------------------------
    // Should block — technology keywords
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("We could use Python for the backend")]
    [InlineData("I suggest PostgreSQL as the database")]
    [InlineData("Build it with React for the frontend")]
    [InlineData("Deploy on AWS EC2 instances")]
    [InlineData("Use Docker containers")]
    [InlineData("Implement CQRS pattern")]
    [InlineData("Store events using Event Sourcing")]
    [InlineData("Use GraphQL for the API")]
    [InlineData("Microservices would suit this architecture")]
    [InlineData("Use OAuth for authentication")]
    [InlineData("Deploy on Azure")]
    [InlineData("Use Redis for caching")]
    [InlineData("Use Entity Framework for data access")]
    [InlineData("Implement the Repository Pattern")]
    [InlineData("Use JWT tokens for auth")]
    public void BlockedKeyword_IsDetected(string text)
    {
        ContainsBlocked(text).ShouldBeTrue($"Expected '{text}' to be blocked");
    }

    // -------------------------------------------------------------------------
    // Should allow — business terminology
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("The user submits an invoice")]
    [InlineData("Managers can approve leave requests")]
    [InlineData("Customers receive notifications when their order ships")]
    [InlineData("The system must support multiple approval workflows")]
    [InlineData("HR processes employee onboarding requests")]
    [InlineData("Sales representatives track their pipeline")]
    [InlineData("The portal should handle leave requests")]
    [InlineData("Stakeholders need visibility into project status")]
    [InlineData("The product catalogue must be searchable")]
    [InlineData("Audit trail is required for all financial transactions")]
    public void BusinessTerminology_IsAllowed(string text)
    {
        ContainsBlocked(text).ShouldBeFalse($"Expected '{text}' to be allowed");
    }

    // -------------------------------------------------------------------------
    // Case insensitivity
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("We should use python")]
    [InlineData("PYTHON would work")]
    [InlineData("Consider POSTGRESQL")]
    [InlineData("deploy with docker")]
    public void BlockedKeyword_CaseInsensitive(string text)
    {
        ContainsBlocked(text).ShouldBeTrue($"Expected '{text}' to be blocked regardless of case");
    }

    // -------------------------------------------------------------------------
    // Word boundary: should NOT block common words that contain blocked substrings
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("The system will send email notifications")]   // "Go" not as standalone
    [InlineData("Employees can request days off")]             // no tech keyword
    [InlineData("The portal allows managers to review")]       // no tech keyword
    public void CommonWords_NotBlockedBySubstringMatch(string text)
    {
        // This test ensures word-boundary matching avoids false positives.
        ContainsBlocked(text).ShouldBeFalse($"Expected '{text}' not to be blocked");
    }

    // -------------------------------------------------------------------------
    // FindFirstMatch returns the matched term
    // -------------------------------------------------------------------------

    [Fact]
    public void FindFirstMatch_ReturnsTerm_WhenBlocked()
    {
        var match = PlanningKeywordBlocklist.FindFirstMatch("We should use Python for this");
        match.ShouldNotBeNull();
    }

    [Fact]
    public void FindFirstMatch_ReturnsNull_WhenClean()
    {
        var match = PlanningKeywordBlocklist.FindFirstMatch("The user submits an invoice");
        match.ShouldBeNull();
    }

    [Fact]
    public void EmptyString_ReturnsNull()
    {
        PlanningKeywordBlocklist.FindFirstMatch("").ShouldBeNull();
        PlanningKeywordBlocklist.FindFirstMatch("   ").ShouldBeNull();
    }
}
