using AiDev;
using AiDev.Api.Hubs;
using AiDev.Models;
using AiDev.Models.Types;
using AiDev.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AiDevNet.Tests.Integration;

public class ProjectStateHubTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private string _workspaceRoot = null!;

    public ValueTask InitializeAsync()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("WORKSPACE_ROOT", _workspaceRoot));

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        try { Directory.Delete(_workspaceRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task StateChanged_WhenNotifierFires_ClientReceivesMessage()
    {
        var projectSlug = "test-project";
        var received = new TaskCompletionSource<(string slug, string[] kinds)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var hubUrl = new UriBuilder(_factory.Server.BaseAddress) { Path = "/hubs/project" }.Uri;
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        connection.On<StateChangedMessage>("StateChanged", msg =>
            received.TrySetResult((msg.ProjectSlug, msg.Kinds)));

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await connection.InvokeAsync("JoinProject", projectSlug, TestContext.Current.CancellationToken);

        // Fire the notifier directly via DI
        var notifier = _factory.Services.GetRequiredService<ProjectStateChangedNotifier>();
        notifier.Notify(new ProjectSlug(projectSlug), ProjectStateChangeKind.Agents | ProjectStateChangeKind.Messages);

        var timeout = Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var completed = await Task.WhenAny(received.Task, timeout);
        completed.ShouldBe(received.Task, "Timed out waiting for StateChanged message");

        var (slug, kinds) = await received.Task;
        slug.ShouldBe(projectSlug);
        kinds.ShouldContain("Agents");
        kinds.ShouldContain("Messages");

        await connection.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StateChanged_WhenClientNotInGroup_DoesNotReceiveMessage()
    {
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var hubUrl = new UriBuilder(_factory.Server.BaseAddress) { Path = "/hubs/project" }.Uri;
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        connection.On<StateChangedMessage>("StateChanged", _ => received.TrySetResult(true));

        await connection.StartAsync(TestContext.Current.CancellationToken);
        // Deliberately do NOT join any project group

        var notifier = _factory.Services.GetRequiredService<ProjectStateChangedNotifier>();
        notifier.Notify(new ProjectSlug("other-project"), ProjectStateChangeKind.Board);

        var timeout = Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        var completed = await Task.WhenAny(received.Task, timeout);
        completed.ShouldBe(timeout, "Client not in group should not receive message");

        await connection.StopAsync(TestContext.Current.CancellationToken);
    }
}
