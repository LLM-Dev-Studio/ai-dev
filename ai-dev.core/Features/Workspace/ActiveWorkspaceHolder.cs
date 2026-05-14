namespace AiDev.Features.Workspace;

/// <summary>
/// Holds the currently active project's <see cref="WorkspacePaths"/>.
/// Registered as a singleton; call <see cref="Activate"/> before any service resolves
/// <see cref="WorkspacePaths"/> from DI.
/// </summary>
public sealed class ActiveWorkspaceHolder
{
    private WorkspacePaths? _paths;

    /// <summary>
    /// Gets the <see cref="WorkspacePaths"/> for the active project.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no project has been activated.</exception>
    public WorkspacePaths Paths => _paths ?? throw new InvalidOperationException(
        "No active project. Call ActiveWorkspaceHolder.Activate(codebasePath) before accessing workspace paths.");

    /// <summary>Gets a value indicating whether a project is currently active.</summary>
    public bool HasActiveProject => _paths is not null;

    /// <summary>Gets the absolute path to the active codebase root, or <see langword="null"/> when no project is active.</summary>
    public string? ActiveCodebasePath { get; private set; }

    /// <summary>
    /// Activates the workspace rooted at <paramref name="codebasePath"/>/.ai-dev/.
    /// </summary>
    /// <param name="codebasePath">Absolute path to the codebase directory.</param>
    public void Activate(string codebasePath)
    {
        ActiveCodebasePath = Path.GetFullPath(codebasePath);
        _paths = new WorkspacePaths(new RootDir(Path.Combine(ActiveCodebasePath, ".ai-dev")));
    }

    /// <summary>Clears the active project.</summary>
    public void Deactivate()
    {
        _paths = null;
        ActiveCodebasePath = null;
    }
}
