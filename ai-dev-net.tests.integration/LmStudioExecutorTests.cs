using AiDev;
using AiDev.Executors;
using AiDev.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using System.Net;
using System.Text;
using System.Threading.Channels;

using Xunit.Sdk;

namespace AiDevNet.Tests.Integration;

// ---------------------------------------------------------------------------
// LM Studio SSE stream builder
// ---------------------------------------------------------------------------

/// <summary>
/// Builds OpenAI-compatible SSE streaming bodies matching the
/// /v1/chat/completions format with stream:true.
/// </summary>
internal static class LmStudioStream
{
    /// <param name="chunks">Each tuple: (content token, is this the final chunk).</param>
    public static string Build(params (string content, bool done)[] chunks)
    {
        var sb = new StringBuilder();
        foreach (var (content, done) in chunks)
        {
            var escaped = content.Replace("\\", "\\\\").Replace("\"", "\\\"");
            if (!done)
            {
                sb.AppendLine(
                    $"data: {{\"id\":\"chatcmpl-test\",\"object\":\"chat.completion.chunk\"," +
                    $"\"choices\":[{{\"index\":0,\"delta\":{{\"content\":\"{escaped}\"}},\"finish_reason\":null}}]}}");
                sb.AppendLine();
            }
            else
            {
                // Final chunk with finish_reason and usage
                sb.AppendLine(
                    $"data: {{\"id\":\"chatcmpl-test\",\"object\":\"chat.completion.chunk\"," +
                    $"\"choices\":[{{\"index\":0,\"delta\":{{}},\"finish_reason\":\"stop\"}}]," +
                    $"\"usage\":{{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15}}}}");
                sb.AppendLine();
                sb.AppendLine("data: [DONE]");
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    public static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
        };

    public static HttpResponseMessage Error(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/plain") };
}

// ---------------------------------------------------------------------------
// Test fixture
// ---------------------------------------------------------------------------

internal sealed class LmStudioFixture : IDisposable
{
    private readonly string _dir;

    public StudioSettingsService Settings { get; }
    public WorkspacePaths Paths { get; }

    public LmStudioFixture(string lmStudioBaseUrl = "http://fake-lmstudio:1234")
    {
        _dir = Path.Combine(Path.GetTempPath(), $"lmstudio-t-{Guid.NewGuid():N}"[..24]);
        Directory.CreateDirectory(_dir);
        Paths = new WorkspacePaths(new RootDir(_dir));
        Settings = new StudioSettingsService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LmStudioBaseUrl"] = lmStudioBaseUrl,
            })
            .Build());
    }

    /// <summary>Builds an executor backed by fake HTTP handlers.</summary>
    public LmStudioAgentExecutor Build(
        HttpMessageHandler lmStudioHandler,
        HttpMessageHandler? healthHandler = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lmstudio").Returns(new HttpClient(lmStudioHandler));
        factory.CreateClient("lmstudio-health")
               .Returns(new HttpClient(healthHandler ?? new FakeHttpMessageHandler()));
        return new LmStudioAgentExecutor(
            factory, Settings, NullLogger<LmStudioAgentExecutor>.Instance);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}

// ---------------------------------------------------------------------------
// Unit tests — no live LM Studio required
// ---------------------------------------------------------------------------

public sealed class LmStudioAgentExecutorUnitTests : IDisposable
{
    private readonly LmStudioFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ExecutorContext Ctx(string? workingDir = null) =>
        new(WorkspaceRoot: Path.GetTempPath(),
            ProjectSlug: "demo-project",
            WorkingDir: workingDir ?? Path.GetTempPath(),
            ModelId: "test-model",
            Prompt: "Hello",
            EnabledSkills: [],
            ReportPid: null,
            CancellationToken: CancellationToken.None);

    private static async Task<(ExecutorResult result, List<string> output)> RunAsync(
        LmStudioAgentExecutor executor, ExecutorContext? ctx = null)
    {
        var channel = Channel.CreateUnbounded<string>(new() { SingleReader = true });
        var result = await executor.RunAsync(ctx ?? Ctx(), channel.Writer);
        channel.Writer.TryComplete();
        var lines = new List<string>();
        await foreach (var line in channel.Reader.ReadAllAsync())
            lines.Add(line);
        return (result, lines);
    }

    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_MultipleChunks_AccumulatesAndOutputsFullResponse()
    {
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(LmStudioStream.Build(
            ("Hello", false), (" world", false), ("!", false), ("", true))));

        var (result, output) = await RunAsync(_fx.Build(h));

        result.ExitCode.ShouldBe(0);
        result.ErrorMessage.ShouldBeNull();
        string.Join("\n", output).ShouldContain("Hello");
        string.Join("\n", output).ShouldContain("world");
    }

    [Fact]
    public async Task RunAsync_SingleDoneChunk_OutputsContent()
    {
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(LmStudioStream.Build(("Hi there", false), ("", true))));

        var (result, output) = await RunAsync(_fx.Build(h));

        result.ExitCode.ShouldBe(0);
        string.Join("", output).ShouldContain("Hi there");
    }

    [Fact]
    public async Task RunAsync_ReportsTokenUsageFromFinalChunk()
    {
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(LmStudioStream.Build(("ok", false), ("", true))));

        var (result, output) = await RunAsync(_fx.Build(h));

        result.ExitCode.ShouldBe(0);
        result.Usage.ShouldNotBeNull();
        result.Usage!.InputTokens.ShouldBe(10);
        result.Usage!.OutputTokens.ShouldBe(5);
    }

    // -------------------------------------------------------------------------
    // Request shape
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_PostsToChatCompletionsEndpointWithCorrectPayload()
    {
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(LmStudioStream.Build(("ok", false), ("", true))));

        await RunAsync(_fx.Build(h), Ctx());

        h.Requests.ShouldHaveSingleItem();
        var req = h.Requests[0];
        req.Method.ShouldBe(HttpMethod.Post);
        req.RequestUri!.PathAndQuery.ShouldBe("/v1/chat/completions");

        var body = h.RequestBodies[0];
        body.ShouldContain("\"model\":\"test-model\"");
        body.ShouldContain("\"stream\":true");
    }

    [Fact]
    public async Task RunAsync_IncludesSystemPromptFromClaudeMd()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "CLAUDE.md"), "You are a test agent.", TestContext.Current.CancellationToken);

        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(LmStudioStream.Build(("ok", false), ("", true))));

        await RunAsync(_fx.Build(h), Ctx(dir));

        h.RequestBodies[0].ShouldContain("You are a test agent.");

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_NoClaudeMd_FallsBackToDefaultSystemPrompt()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(LmStudioStream.Build(("ok", false), ("", true))));

        await RunAsync(_fx.Build(h), Ctx(dir));

        h.RequestBodies[0].ShouldContain("helpful AI agent");

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_IncludesToolsWhenWorkspaceSkillEnabled()
    {
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(LmStudioStream.Build(("ok", false), ("", true))));

        await RunAsync(_fx.Build(h), Ctx() with { EnabledSkills = ["mcp-workspace"] });

        h.RequestBodies[0].ShouldContain("\"tools\":");
        h.RequestBodies[0].ShouldContain("read_file");
    }

    [Fact]
    public async Task RunAsync_OmitsToolsWhenSkillExplicitlyDisabled()
    {
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(LmStudioStream.Build(("ok", false), ("", true))));

        // An explicit list that does NOT include "mcp-workspace" disables tools.
        await RunAsync(_fx.Build(h), Ctx() with { EnabledSkills = ["some-other-skill"] });

        h.RequestBodies[0].ShouldNotContain("\"tools\":");
    }

    // -------------------------------------------------------------------------
    // Error conditions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_LmStudioReturns500_ReturnsNonZeroExitCode()
    {
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Error(HttpStatusCode.InternalServerError, "model load failed"));

        var (result, output) = await RunAsync(_fx.Build(h));

        result.ExitCode.ShouldBe(1);
        result.ErrorMessage!.ShouldContain("500");
        output.ShouldContain(l => l.Contains("[error]"));
    }

    [Fact]
    public async Task RunAsync_LmStudioReturns404_IncludesStatusInError()
    {
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Error(HttpStatusCode.NotFound, "model not found"));

        var (result, _) = await RunAsync(_fx.Build(h));

        result.ExitCode.ShouldBe(1);
        result.ErrorMessage!.ShouldContain("404");
    }

    [Fact]
    public async Task RunAsync_NetworkFailure_ReturnsNonZeroExitCodeWithMessage()
    {
        var executor = _fx.Build(
            new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused")));

        var (result, output) = await RunAsync(executor);

        result.ExitCode.ShouldBe(1);
        result.ErrorMessage!.ShouldContain("Connection refused");
        output.ShouldContain(l => l.Contains("[error]"));
    }

    // -------------------------------------------------------------------------
    // Edge cases — SSE format
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_DoneWithoutContentChunks_ReturnsSuccessNoError()
    {
        // Only a done chunk, no content
        var body = "data: {\"id\":\"chatcmpl-test\",\"object\":\"chat.completion.chunk\"," +
                   "\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
                   "data: [DONE]\n\n";
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(body));

        var (result, output) = await RunAsync(_fx.Build(h));

        result.ExitCode.ShouldBe(0);
        output.ShouldNotContain(l => l.Contains("[error]"));
    }

    [Fact]
    public async Task RunAsync_MalformedSseLine_SkipsItAndContinues()
    {
        var body = "data: NOT-JSON\n\n" +
                   LmStudioStream.Build(("hello", false), ("", true));
        var h = new FakeHttpMessageHandler();
        h.Enqueue(LmStudioStream.Ok(body));

        var (result, output) = await RunAsync(_fx.Build(h));

        result.ExitCode.ShouldBe(0);
        string.Join("", output).ShouldContain("hello");
    }

    // -------------------------------------------------------------------------
    // CheckHealthAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_LmStudioAvailableWithModels_ReturnsHealthyWithModelDetails()
    {
        const string json = """
            {"models":[
                {"type":"llm","key":"google/gemma-4-26b","display_name":"Gemma 4 26B","capabilities":{"vision":true,"trained_for_tool_use":true}},
                {"type":"llm","key":"qwen/qwen2.5-7b","display_name":"Qwen 2.5 7B","capabilities":{"trained_for_tool_use":false}}
            ]}
            """;
        var healthH = new FakeHttpMessageHandler();
        healthH.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        var result = await _fx.Build(new FakeHttpMessageHandler(), healthH).CheckHealthAsync(TestContext.Current.CancellationToken);

        result.IsHealthy.ShouldBeTrue();
        result.Models!.Select(m => m.Id).ShouldContain("google/gemma-4-26b");
        result.Models!.Select(m => m.Id).ShouldContain("qwen/qwen2.5-7b");
        // Display names should be parsed from the response.
        result.Models!.First(m => m.Id == "google/gemma-4-26b").DisplayName.ShouldBe("Gemma 4 26B");
    }

    [Fact]
    public async Task CheckHealthAsync_FiltersOutEmbeddingModels()
    {
        const string json = """
            {"models":[
                {"type":"llm","key":"google/gemma-4-26b","display_name":"Gemma 4 26B","capabilities":{"trained_for_tool_use":true}},
                {"type":"embedding","key":"nomic-ai/text-embed","display_name":"Nomic Embed"}
            ]}
            """;
        var healthH = new FakeHttpMessageHandler();
        healthH.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        var result = await _fx.Build(new FakeHttpMessageHandler(), healthH).CheckHealthAsync(TestContext.Current.CancellationToken);

        result.IsHealthy.ShouldBeTrue();
        result.Models!.Count.ShouldBe(1);
        result.Models![0].Id.ShouldBe("google/gemma-4-26b");
    }

    [Fact]
    public async Task CheckHealthAsync_ParsesCapabilitiesFromResponse()
    {
        const string json = """
            {"models":[
                {"type":"llm","key":"tool-model","display_name":"Tool Model","capabilities":{"vision":true,"trained_for_tool_use":true}},
                {"type":"llm","key":"basic-model","display_name":"Basic Model","capabilities":{"vision":false,"trained_for_tool_use":false}}
            ]}
            """;
        var healthH = new FakeHttpMessageHandler();
        healthH.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        var result = await _fx.Build(new FakeHttpMessageHandler(), healthH).CheckHealthAsync(TestContext.Current.CancellationToken);

        var toolModel = result.Models!.First(m => m.Id == "tool-model");
        toolModel.Capabilities.HasFlag(AiDev.Executors.ModelCapabilities.ToolCalling).ShouldBeTrue();
        toolModel.Capabilities.HasFlag(AiDev.Executors.ModelCapabilities.Vision).ShouldBeTrue();

        var basicModel = result.Models!.First(m => m.Id == "basic-model");
        basicModel.Capabilities.HasFlag(AiDev.Executors.ModelCapabilities.ToolCalling).ShouldBeFalse();
        basicModel.Capabilities.HasFlag(AiDev.Executors.ModelCapabilities.Vision).ShouldBeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_NoModelsLoaded_ReturnsHealthyWithEmptyDetails()
    {
        const string json = """{"models":[]}""";
        var healthH = new FakeHttpMessageHandler();
        healthH.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        var result = await _fx.Build(new FakeHttpMessageHandler(), healthH).CheckHealthAsync(TestContext.Current.CancellationToken);

        result.IsHealthy.ShouldBeTrue();
        (result.Models == null || result.Models.Count == 0).ShouldBeTrue("Expected no model details");
    }

    [Fact]
    public async Task CheckHealthAsync_LmStudioReturnsError_ReturnsUnhealthy()
    {
        var healthH = new FakeHttpMessageHandler();
        healthH.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await _fx.Build(new FakeHttpMessageHandler(), healthH).CheckHealthAsync(TestContext.Current.CancellationToken);

        result.IsHealthy.ShouldBeFalse();
        result.Message.ShouldContain("503");
    }

    [Fact]
    public async Task CheckHealthAsync_NetworkFailure_ReturnsUnhealthy()
    {
        var result = await _fx.Build(
                new FakeHttpMessageHandler(),
                new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused")))
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        result.IsHealthy.ShouldBeFalse();
        result.Message.ShouldContain("Connection refused");
    }

    // -------------------------------------------------------------------------
    // Executor identity
    // -------------------------------------------------------------------------

    [Fact]
    public void Name_ReturnsLmStudio()
    {
        var h = new FakeHttpMessageHandler();
        var executor = _fx.Build(h);

        executor.Name.ShouldBe("lmstudio");
        executor.DisplayName.ShouldBe("LM Studio");
    }

    [Fact]
    public void KnownModels_IsEmpty()
    {
        var h = new FakeHttpMessageHandler();
        var executor = _fx.Build(h);

        executor.KnownModels.ShouldBeEmpty();
    }

    [Fact]
    public void AvailableSkills_ContainsMcpWorkspace()
    {
        var h = new FakeHttpMessageHandler();
        var executor = _fx.Build(h);

        executor.AvailableSkills.ShouldContain(s => s.Key == "mcp-workspace");
    }
}

// ---------------------------------------------------------------------------
// Integration tests — require a live LM Studio at localhost:1234
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
public sealed class LmStudioAgentExecutorIntegrationTests : IDisposable
{
    private const string LmStudioBaseUrl = "http://localhost:1234";

    private readonly LmStudioFixture _fx = new(LmStudioBaseUrl);

    public void Dispose() => _fx.Dispose();

    /// <summary>
    /// Skips the test if LM Studio is unreachable or has no models loaded.
    /// Returns the model to use: LMSTUDIO_TEST_MODEL env var if set, otherwise the first available model.
    /// </summary>
    private static async Task<string> SkipIfUnavailableAsync()
    {
        var envModel = Environment.GetEnvironmentVariable("LMSTUDIO_TEST_MODEL");

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var r = await http.GetAsync($"{LmStudioBaseUrl}/api/v1/models");
            if (!r.IsSuccessStatusCode)
                throw SkipException.ForSkip($"LM Studio returned HTTP {(int)r.StatusCode}");

            if (envModel is { Length: > 0 })
                return envModel;

            var body = await r.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonDocument.Parse(body);

            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                models = data.EnumerateArray()
                    .Select(m => m.TryGetProperty("id", out var idProp) ? idProp.GetString() : null)
                    .Where(n => n is { Length: > 0 })
                    .Select(n => n!)
                    .ToList();
            }

            if (models.Count == 0)
                throw SkipException.ForSkip("LM Studio is running but no models are loaded.");

            return models[0];
        }
        catch (SkipException) { throw; }
        catch (Exception ex)
        {
            throw SkipException.ForSkip($"LM Studio not reachable: {ex.Message}");
        }
    }

    private LmStudioAgentExecutor BuildRealExecutor()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lmstudio")
               .Returns(new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
        factory.CreateClient("lmstudio-health")
               .Returns(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
        return new LmStudioAgentExecutor(
            factory, _fx.Settings, NullLogger<LmStudioAgentExecutor>.Instance);
    }

    [Fact]
    public async Task CheckHealthAsync_RealLmStudio_ReturnsHealthy()
    {
        await SkipIfUnavailableAsync();

        var result = await BuildRealExecutor().CheckHealthAsync(TestContext.Current.CancellationToken);

        result.IsHealthy.ShouldBeTrue(result.Message);
    }

    [Fact]
    public async Task RunAsync_RealLmStudio_SimplePrompt_ReturnsNonEmptyResponse()
    {
        var model = await SkipIfUnavailableAsync();

        var channel = Channel.CreateUnbounded<string>(new() { SingleReader = true });
        var ctx = new ExecutorContext(
            WorkspaceRoot: Path.GetTempPath(),
            ProjectSlug: "demo-project",
            WorkingDir: Path.GetTempPath(),
            ModelId: model,
            Prompt: "Reply with exactly the text: LMSTUDIO_OK",
            EnabledSkills: [],
            ReportPid: null,
            CancellationToken: CancellationToken.None);

        var result = await BuildRealExecutor().RunAsync(ctx, channel.Writer);
        channel.Writer.TryComplete();

        var lines = new List<string>();
        await foreach (var line in channel.Reader.ReadAllAsync(TestContext.Current.CancellationToken))
            lines.Add(line);

        result.ExitCode.ShouldBe(0, $"Executor failed: {result.ErrorMessage}");
        string.Join("\n", lines).ShouldNotBeNullOrWhiteSpace("Expected model output");
    }
}
