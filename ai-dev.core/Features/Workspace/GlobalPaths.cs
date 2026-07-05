namespace AiDev.Features.Workspace;

/// <summary>
/// Provides paths to the global (per-user, cross-project) AI Dev Studio data directory.
/// These locations are independent of any codebase root.
/// </summary>
public static class GlobalPaths
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AiDevStudio");

    /// <summary>Path to the global managed-projects file.</summary>
    public static string ManagedProjectsFile => Path.Combine(AppDataDir, "managed-projects.json");

    /// <summary>Path to the global studio settings file.</summary>
    public static string StudioSettingsFile => Path.Combine(AppDataDir, "studio-settings.json");

    /// <summary>Path to the global agent templates directory.</summary>
    public static string AgentTemplatesDir => Path.Combine(AppDataDir, "agent-templates");

    /// <summary>Ensures the global app data directory exists.</summary>
    public static void EnsureCreated() => Directory.CreateDirectory(AppDataDir);
}
