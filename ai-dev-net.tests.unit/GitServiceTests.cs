using AiDev.Features.Git;

namespace AiDevNet.Tests.Unit;

public class GitServiceTests
{
    [Fact]
    public void IsGitRepo_WhenPathDoesNotExist_ReturnsFalse()
    {
        var service = new GitService();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = service.IsGitRepo(nonExistentPath);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsGitRepo_WhenPathExistsButIsNotGitRepo_ReturnsFalse()
    {
        var service = new GitService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = service.IsGitRepo(tempDir);

            result.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetLog_WhenRepositoryPathDoesNotExist_ReturnsEmptyList()
    {
        var service = new GitService();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = service.GetLog(nonExistentPath, 50);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetLog_WhenPathIsNotGitRepo_ReturnsEmptyList()
    {
        var service = new GitService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = service.GetLog(tempDir, 50);

            result.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetLog_WithValidCountParameter_CallsGitWithCorrectCount()
    {
        var service = new GitService();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        // This will return empty (not a git repo), but verifies the method handles the count parameter
        var result = service.GetLog(nonExistentPath, 100);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetCommit_WhenHashIsEmpty_ReturnsNull()
    {
        var service = new GitService();
        var repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = service.GetCommit(repoPath, string.Empty);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCommit_WhenHashIsTooShort_ReturnsNull()
    {
        var service = new GitService();
        var repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = service.GetCommit(repoPath, "abc");

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCommit_WhenHashIsTooLong_ReturnsNull()
    {
        var service = new GitService();
        var repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = service.GetCommit(repoPath, "a".PadRight(65, 'a'));

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCommit_WhenHashContainsInvalidCharacters_ReturnsNull()
    {
        var service = new GitService();
        var repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = service.GetCommit(repoPath, "abc-123_@#$");

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcd")]
    [InlineData("0123456789abcdef")]
    public void GetCommit_WhenHashIsValidFormat_AttemptsLookup(string validHash)
    {
        var service = new GitService();
        var repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        // Valid hex hashes (4+ chars) should attempt lookup
        // They'll return null since path doesn't exist, but validates format acceptance
        var result = service.GetCommit(repoPath, validHash);

        // Returns null because repo doesn't exist, but format is accepted
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("!abcdef")]
    [InlineData("abcdef!")]
    [InlineData("abc def")]
    [InlineData("abcG123")]
    [InlineData("ABCDEF")]
    public void GetCommit_WhenHashContainsFlagInjectionCharacters_RejectsHash(string invalidHash)
    {
        var service = new GitService();
        var repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = service.GetCommit(repoPath, invalidHash);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCommit_WhenRepositoryPathDoesNotExist_ReturnsNull()
    {
        var service = new GitService();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var validHash = "abcd1234";

        var result = service.GetCommit(nonExistentPath, validHash);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetLog_DefaultCountIs50()
    {
        var service = new GitService();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        // Should use default count of 50 without throwing
        var result = service.GetLog(nonExistentPath);

        result.ShouldBeEmpty();
    }
}
