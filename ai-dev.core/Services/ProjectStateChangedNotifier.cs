namespace AiDev.Services;

/// <summary>
/// Publishes project-level state changes from core services so UI layers can react consistently.
/// </summary>
public class ProjectStateChangedNotifier
{
    public event Action<ProjectStateChangedEvent>? Changed;

    public void Notify(ProjectSlug projectSlug, ProjectStateChangeKind kind)
    {
        if (kind == ProjectStateChangeKind.None)
            return;

        Changed?.Invoke(new ProjectStateChangedEvent(projectSlug, kind, DateTime.UtcNow));
    }
}
