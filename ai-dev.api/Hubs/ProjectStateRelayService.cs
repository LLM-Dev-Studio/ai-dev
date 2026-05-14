using AiDev.Models;
using AiDev.Services;
using Microsoft.AspNetCore.SignalR;

namespace AiDev.Api.Hubs;

/// <summary>
/// Bridges ProjectStateChangedNotifier events to SignalR by forwarding each notification
/// to the project's group on ProjectStateHub. Registered as a hosted service so it
/// subscribes on startup and unsubscribes on shutdown.
/// </summary>
public sealed class ProjectStateRelayService(
    ProjectStateChangedNotifier notifier,
    IHubContext<ProjectStateHub> hubContext) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        notifier.Changed += OnChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        notifier.Changed -= OnChanged;
        return Task.CompletedTask;
    }

    private void OnChanged(ProjectStateChangedEvent e)
    {
        var kinds = GetKindNames(e.Kind);
        var message = new StateChangedMessage(e.ProjectSlug.Value, kinds);
        _ = hubContext.Clients
            .Group(e.ProjectSlug.Value)
            .SendAsync("StateChanged", message);
    }

    private static string[] GetKindNames(ProjectStateChangeKind kind)
    {
        var names = new List<string>(4);
        if (kind.HasFlag(ProjectStateChangeKind.Agents))    names.Add("Agents");
        if (kind.HasFlag(ProjectStateChangeKind.Messages))  names.Add("Messages");
        if (kind.HasFlag(ProjectStateChangeKind.Decisions)) names.Add("Decisions");
        if (kind.HasFlag(ProjectStateChangeKind.Board))     names.Add("Board");
        return [.. names];
    }
}
