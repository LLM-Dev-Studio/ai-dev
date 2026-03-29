namespace AiDev.Services;

/// <summary>
/// Singleton pub/sub bridge that lets background services (DispatcherService,
/// AgentRunnerService) signal UI pages that inbox messages have changed for a
/// given project, without taking any dependency on Blazor.
/// </summary>
public class MessageChangedNotifier
{
    public event Action<ProjectSlug>? Changed;

    public void Notify(ProjectSlug projectSlug) => Changed?.Invoke(projectSlug);
}
