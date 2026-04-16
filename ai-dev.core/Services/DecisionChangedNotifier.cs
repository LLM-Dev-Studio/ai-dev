namespace AiDev.Services;

/// <summary>
/// Singleton pub/sub bridge that lets background services (DispatcherService)
/// signal UI pages that decisions have changed for a given project, without taking any dependency on Blazor.
/// </summary>
public class DecisionChangedNotifier
{
    public event Action<ProjectSlug>? Changed;

    public void Notify(ProjectSlug projectSlug) => Changed?.Invoke(projectSlug);
}
