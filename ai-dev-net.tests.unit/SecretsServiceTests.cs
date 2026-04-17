using AiDev.Features.Secrets;

namespace AiDevNet.Tests.Unit;

public class SecretsServiceTests
{
    [Fact]
    public void ListSecrets_WhenSecretsFileDoesNotExist_ReturnsEmptyList()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        var result = service.ListSecrets(projectSlug);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ListSecrets_WhenSecretsExist_ReturnsSecretNames()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        service.SetSecret(projectSlug, "api-key", "secret-value");
        service.SetSecret(projectSlug, "db-password", "another-secret");

        var result = service.ListSecrets(projectSlug);

        result.ShouldContain("api-key");
        result.ShouldContain("db-password");
        result.Count().ShouldBe(2);
    }

    [Fact]
    public void SetSecret_WithValidNameAndValue_StoresSecret()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        service.SetSecret(projectSlug, "api-key", "secret-value");

        var secrets = service.ListSecrets(projectSlug);
        secrets.ShouldContain("api-key");
    }

    [Fact]
    public void SetSecret_WhenNameIsEmpty_ThrowsArgumentException()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        Should.Throw<ArgumentException>(() => service.SetSecret(projectSlug, "", "value"));
    }

    [Fact]
    public void SetSecret_WhenNameIsWhitespace_ThrowsArgumentException()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        Should.Throw<ArgumentException>(() => service.SetSecret(projectSlug, "   ", "value"));
    }

    [Fact]
    public void SetSecret_WhenNameAlreadyExists_Replaces()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        service.SetSecret(projectSlug, "api-key", "original");
        service.SetSecret(projectSlug, "api-key", "updated");

        var secrets = service.ListSecrets(projectSlug);
        secrets.Count(s => s == "api-key").ShouldBe(1);
    }

    [Fact]
    public void DeleteSecret_WhenSecretExists_Removes()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        service.SetSecret(projectSlug, "api-key", "secret");
        service.DeleteSecret(projectSlug, "api-key");

        var secrets = service.ListSecrets(projectSlug);
        secrets.ShouldBeEmpty();
    }

    [Fact]
    public void DeleteSecret_WhenSecretDoesNotExist_IsNoOp()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        // Should not throw
        service.DeleteSecret(projectSlug, "nonexistent");
    }

    [Fact]
    public void LoadDecryptedSecrets_WhenNoSecretsExist_ReturnsEmptyDictionary()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        var result = service.LoadDecryptedSecrets(projectSlug);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void LoadDecryptedSecrets_WhenSecretsExist_ReturnsDecryptedValues()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        service.SetSecret(projectSlug, "api-key", "secret-value");
        service.SetSecret(projectSlug, "token", "token-value");

        var result = service.LoadDecryptedSecrets(projectSlug);

        result.Count.ShouldBe(2);
        result["api-key"].ShouldBe("secret-value");
        result["token"].ShouldBe("token-value");
    }

    [Fact]
    public void LoadDecryptedSecrets_WithUnicodeValues_DecryptsCorrectly()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");
        var unicodeValue = "café-password-éàü";

        service.SetSecret(projectSlug, "unicode-key", unicodeValue);

        var result = service.LoadDecryptedSecrets(projectSlug);

        result["unicode-key"].ShouldBe(unicodeValue);
    }

    [Fact]
    public void LoadDecryptedSecrets_WithSpecialCharacterValues_DecryptsCorrectly()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");
        var specialChars = "!@#$%^&*()_+-=[]{}|;:',.<>?/`~\\";

        service.SetSecret(projectSlug, "special", specialChars);

        var result = service.LoadDecryptedSecrets(projectSlug);

        result["special"].ShouldBe(specialChars);
    }

    [Fact]
    public void LoadDecryptedSecrets_WithLargeValue_DecryptsCorrectly()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");
        var largeValue = string.Join("", Enumerable.Range(0, 10000).Select(i => $"part{i}"));

        service.SetSecret(projectSlug, "large", largeValue);

        var result = service.LoadDecryptedSecrets(projectSlug);

        result["large"].ShouldBe(largeValue);
    }

    [Fact]
    public void SetAndLoadSecret_RoundTrip_PreservesValue()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");
        var originalValue = "This is my secret value with stuff: !@#$%^&*()";

        service.SetSecret(projectSlug, "my-secret", originalValue);

        var decrypted = service.LoadDecryptedSecrets(projectSlug);

        decrypted["my-secret"].ShouldBe(originalValue);
    }

    [Fact]
    public void ListSecrets_AfterDeleteSecret_NoLongerIncludes()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        service.SetSecret(projectSlug, "secret1", "value1");
        service.SetSecret(projectSlug, "secret2", "value2");
        service.DeleteSecret(projectSlug, "secret1");

        var result = service.ListSecrets(projectSlug);

        result.ShouldContain("secret2");
        result.ShouldNotContain("secret1");
        result.Count().ShouldBe(1);
    }

    [Fact]
    public void Secrets_AreSeparatePerProject()
    {
        var service = CreateService(out _);
        var project1 = new ProjectSlug("project-1");
        var project2 = new ProjectSlug("project-2");

        service.SetSecret(project1, "key", "value1");
        service.SetSecret(project2, "key", "value2");

        var secrets1 = service.LoadDecryptedSecrets(project1);
        var secrets2 = service.LoadDecryptedSecrets(project2);

        secrets1["key"].ShouldBe("value1");
        secrets2["key"].ShouldBe("value2");
    }

    [Fact]
    public void SetSecret_WithEmptyStringValue_IsAllowed()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        service.SetSecret(projectSlug, "empty-secret", "");

        var result = service.LoadDecryptedSecrets(projectSlug);
        result["empty-secret"].ShouldBe("");
    }

    private static SecretsService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new SecretsService(paths, new AtomicFileWriter());
    }
}
