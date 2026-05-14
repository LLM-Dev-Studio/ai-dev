using AiDev.Models;
using Microsoft.AspNetCore.SignalR;

namespace AiDev.Api.Hubs;

/// <summary>
/// Broadcasts project state changes to connected VS Code extension and web clients.
/// Clients join a project group by sending JoinProject(projectSlug) on connect.
/// </summary>
public class ProjectStateHub : Hub
{
    public async Task JoinProject(string projectSlug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, projectSlug);
    }

    public async Task LeaveProject(string projectSlug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, projectSlug);
    }
}

/// <summary>
/// Payload sent to clients when project state changes.
/// </summary>
public sealed record StateChangedMessage(string ProjectSlug, string[] Kinds);
