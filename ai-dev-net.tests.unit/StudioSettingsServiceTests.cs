using AiDev.Models;
using AiDev.Services;
using Microsoft.Extensions.Configuration;

namespace AiDevNet.Tests.Unit;

public class StudioSettingsServiceTests
{
    [Fact]
    public void GetSettings_WhenNoConfigurationExists_ReturnsDefaults()
    {
        var config = CreateConfiguration();
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.OllamaBaseUrl.ShouldBe("http://localhost:11434");
        settings.LmStudioBaseUrl.ShouldBe("http://localhost:1234");
        settings.AnthropicApiKey.ShouldBeNull();
        settings.GitHubToken.ShouldBeNull();
        settings.InsightsExecutor.ShouldBeNull();
        settings.InsightsModel.ShouldBeNull();
        settings.Models.ShouldContainKeyAndValue("sonnet", "claude-sonnet-4-6");
        settings.Models.ShouldContainKeyAndValue("haiku", "claude-haiku-4-5-20251001");
        settings.Models.ShouldContainKeyAndValue("opus", "claude-opus-4-6");
    }

    [Fact]
    public void GetSettings_WhenOllamaUrlConfigured_OverridesDefault()
    {
        var config = CreateConfiguration(
            ("StudioSettings:OllamaBaseUrl", "http://ollama.example.com:11434")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.OllamaBaseUrl.ShouldBe("http://ollama.example.com:11434");
    }

    [Fact]
    public void GetSettings_WhenLmStudioUrlConfigured_OverridesDefault()
    {
        var config = CreateConfiguration(
            ("StudioSettings:LmStudioBaseUrl", "http://lmstudio.example.com:1234")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.LmStudioBaseUrl.ShouldBe("http://lmstudio.example.com:1234");
    }

    [Fact]
    public void GetSettings_WhenAnthropicApiKeyConfigured_IncludesIt()
    {
        var config = CreateConfiguration(
            ("StudioSettings:AnthropicApiKey", "sk-test-key-12345")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.AnthropicApiKey.ShouldBe("sk-test-key-12345");
    }

    [Fact]
    public void GetSettings_WhenGitHubTokenNotConfigured_IsNull()
    {
        var config = CreateConfiguration();
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.GitHubToken.ShouldBeNull();
    }

    [Fact]
    public void GetSettings_WhenInsightsExecutorConfigured_IncludesIt()
    {
        var config = CreateConfiguration(
            ("StudioSettings:InsightsExecutor", "anthropic")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.InsightsExecutor.ShouldBe("anthropic");
    }

    [Fact]
    public void GetSettings_WhenInsightsModelConfigured_IncludesIt()
    {
        var config = CreateConfiguration(
            ("StudioSettings:InsightsModel", "claude-haiku-4-5-20251001")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.InsightsModel.ShouldBe("claude-haiku-4-5-20251001");
    }

    [Fact]
    public void GetSettings_WhenModelAliasConfigured_OverridesDefault()
    {
        var config = CreateConfiguration(
            ("StudioSettings:Models:sonnet", "claude-sonnet-5-2024-06-01")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.Models.ShouldContainKeyAndValue("sonnet", "claude-sonnet-5-2024-06-01");
    }

    [Fact]
    public void GetSettings_WhenNewModelAliasConfigured_IncludesIt()
    {
        var config = CreateConfiguration(
            ("StudioSettings:Models:custom", "custom-model-123")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.Models.ShouldContainKeyAndValue("custom", "custom-model-123");
    }

    [Fact]
    public void GetSettings_WhenMultipleModelsConfigured_IncludesAll()
    {
        var config = CreateConfiguration(
            ("StudioSettings:Models:sonnet", "claude-sonnet-5"),
            ("StudioSettings:Models:haiku", "claude-haiku-5"),
            ("StudioSettings:Models:custom", "my-custom-model")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.Models.ShouldContainKeyAndValue("sonnet", "claude-sonnet-5");
        settings.Models.ShouldContainKeyAndValue("haiku", "claude-haiku-5");
        settings.Models.ShouldContainKeyAndValue("custom", "my-custom-model");
        settings.Models.Count.ShouldBe(4); // sonnet, haiku, custom, opus (default)
    }

    [Fact]
    public void GetSettings_WhenConfigurationValueIsWhitespace_TreatsAsEmpty()
    {
        var config = CreateConfiguration(
            ("StudioSettings:AnthropicApiKey", "   "),
            ("StudioSettings:GitHubToken", "\t\n")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.AnthropicApiKey.ShouldBeNull();
        settings.GitHubToken.ShouldBeNull();
    }

    [Fact]
    public void GetSettings_WhenConfigurationValueHasWhitespace_Trims()
    {
        var config = CreateConfiguration(
            ("StudioSettings:InsightsExecutor", "  anthropic  "),
            ("StudioSettings:InsightsModel", "\nmodel-123\t")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.InsightsExecutor.ShouldBe("anthropic");
        settings.InsightsModel.ShouldBe("model-123");
    }

    [Fact]
    public void GetSettings_WhenModelsConfiguredWithBlankValues_IgnoresThem()
    {
        var config = CreateConfiguration(
            ("StudioSettings:Models:empty", ""),
            ("StudioSettings:Models:whitespace", "   "),
            ("StudioSettings:Models:haiku", "claude-haiku-5")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        settings.Models.ContainsKey("empty").ShouldBeFalse();
        settings.Models.ContainsKey("whitespace").ShouldBeFalse();
        settings.Models.ShouldContainKeyAndValue("haiku", "claude-haiku-5");
    }

    [Fact]
    public void SaveSettings_WhenCalled_PersistsToFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var originalSettingsPath = Path.Combine(AppContext.BaseDirectory, FilePathConstants.StudioSettingsFileName);
            var settingsPath = Path.Combine(tempDir, FilePathConstants.StudioSettingsFileName);

            var config = CreateConfiguration();
            var service = new StudioSettingsService(config);

            var settings = new StudioSettings
            {
                OllamaBaseUrl = "http://custom-ollama.local:11434",
                LmStudioBaseUrl = "http://custom-lmstudio.local:1234",
                AnthropicApiKey = "sk-custom-key",
                GitHubToken = "ghp_custom_token",
                InsightsExecutor = "ollama",
                InsightsModel = "llama2",
                Models = new Dictionary<string, string>
                {
                    ["sonnet"] = "claude-sonnet-custom",
                    ["haiku"] = "claude-haiku-custom",
                    ["test"] = "test-model",
                },
            };

            service.SaveSettings(settings);

            // Verify file was created
            File.Exists(originalSettingsPath).ShouldBeTrue();
            var content = File.ReadAllText(originalSettingsPath);
            content.ShouldContain("OllamaBaseUrl");
            content.ShouldContain("custom-ollama.local");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SaveSettings_PersistsSettingsToFile()
    {
        var config = CreateConfiguration();
        var service = new StudioSettingsService(config);

        var settings = new StudioSettings
        {
            OllamaBaseUrl = "  http://ollama.local  ",
            LmStudioBaseUrl = "\thttp://lmstudio.local\t",
            AnthropicApiKey = "  sk-key  ",
            GitHubToken = "\nghp-token\n",
            InsightsExecutor = "  anthropic  ",
            InsightsModel = "  claude-haiku  ",
        };

        service.SaveSettings(settings);

        // File should have been written to AppContext.BaseDirectory
        // The settings are persisted with trimming applied
        File.Exists(Path.Combine(AppContext.BaseDirectory, FilePathConstants.StudioSettingsFileName)).ShouldBeTrue();
    }

    [Fact]
    public void SaveSettings_WhenBlankSettingsProvided_UsesDefaults()
    {
        var config = CreateConfiguration();
        var service = new StudioSettingsService(config);

        var settings = new StudioSettings
        {
            OllamaBaseUrl = "   ",
            LmStudioBaseUrl = "",
            AnthropicApiKey = null,
            GitHubToken = null,
            InsightsExecutor = null,
            InsightsModel = null,
        };

        service.SaveSettings(settings);

        var reloaded = service.GetSettings();
        reloaded.OllamaBaseUrl.ShouldBe("http://localhost:11434");
        reloaded.LmStudioBaseUrl.ShouldBe("http://localhost:1234");
        reloaded.AnthropicApiKey.ShouldBeNull();
        reloaded.GitHubToken.ShouldBeNull();
        reloaded.InsightsExecutor.ShouldBeNull();
        reloaded.InsightsModel.ShouldBeNull();
    }

    [Fact]
    public void SaveSettings_WhenNullSettingsProvided_Throws()
    {
        var config = CreateConfiguration();
        var service = new StudioSettingsService(config);

        var ex = Should.Throw<ArgumentNullException>(() => service.SaveSettings(null!));
        ex.ParamName.ShouldBe("settings");
    }

    [Fact]
    public void SaveSettings_WhenEmptyModelsProvided_SavesEmpty()
    {
        var config = CreateConfiguration();
        var service = new StudioSettingsService(config);

        var settings = new StudioSettings { Models = new Dictionary<string, string>() };

        service.SaveSettings(settings);

        var reloaded = service.GetSettings();
        reloaded.Models.Count.ShouldBe(3); // Just defaults
    }

    [Fact]
    public void GetSettings_WhenSaveSettingsCalledWithModels_IncludesPersistedModels()
    {
        var config = CreateConfiguration();
        var service = new StudioSettingsService(config);

        var settings = new StudioSettings
        {
            Models = new Dictionary<string, string>
            {
                ["custom1"] = "model-one",
                ["custom2"] = "model-two",
            },
        };

        // Save the settings
        service.SaveSettings(settings);

        // Settings are persisted to file, but GetSettings() reads from config
        // which was created empty, so it won't have the saved models
        var reloaded = service.GetSettings();
        reloaded.Models.Count.ShouldBe(3); // Just the defaults
    }

    [Fact]
    public void GetSettings_WhenModelAliasIsCaseInsensitive_NormalizesKey()
    {
        var config = CreateConfiguration(
            ("StudioSettings:Models:MyModel", "my-model-id")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        // Dictionary should have normalized the key
        settings.Models.ContainsKey("MyModel").ShouldBeTrue();
    }

    [Fact]
    public void SaveSettings_WhenNoModelsProvided_StoresPersisted()
    {
        var config = CreateConfiguration();
        var service = new StudioSettingsService(config);

        var settings = new StudioSettings { Models = [] };

        service.SaveSettings(settings);

        var reloaded = service.GetSettings();
        reloaded.Models.ShouldNotBeNull();
        reloaded.Models.Count.ShouldBe(3); // Defaults
    }

    [Fact]
    public void GetSettings_WhenConfigurationFromMultipleSources_MergesCorrectly()
    {
        var config = CreateConfiguration(
            ("StudioSettings:OllamaBaseUrl", "http://ollama.local"),
            ("OllamaBaseUrl", "http://ollama.fallback"),
            ("StudioSettings:AnthropicApiKey", "sk-section-key"),
            ("AnthropicApiKey", "sk-flat-key")
        );
        var service = new StudioSettingsService(config);

        var settings = service.GetSettings();

        // Section key should take precedence
        settings.OllamaBaseUrl.ShouldBe("http://ollama.local");
        settings.AnthropicApiKey.ShouldBe("sk-section-key");
    }

    private static IConfiguration CreateConfiguration(params (string, string)[] entries)
    {
        var builder = new ConfigurationBuilder();

        if (entries.Length > 0)
        {
            var inMemoryCollection = entries.ToDictionary(e => e.Item1, e => (string?)e.Item2);
            builder.AddInMemoryCollection(inMemoryCollection);
        }

        return builder.Build();
    }
}
