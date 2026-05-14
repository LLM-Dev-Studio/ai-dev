namespace AiDev.Features.Workspace;

/// <summary>
/// Reads .ai-dev/project.json from a directory and resolves WorkspacePaths from it.
/// </summary>
public static class ProjectConfigReader
{
    private const string AiDevDirName = ".ai-dev";
    private const string ConfigFileName = "project.json";

    /// <summary>
    /// Tries to read .ai-dev/project.json from the given base directory.
    /// Returns null if the file is absent, malformed, or missing required fields.
    /// </summary>
    public static ProjectConfig? TryRead(string baseDirectory)
    {
        var configPath = Path.Combine(baseDirectory, AiDevDirName, ConfigFileName);
        if (!File.Exists(configPath)) return null;

        try
        {
            var json = File.ReadAllText(configPath);
            var dto = JsonSerializer.Deserialize<ProjectConfigDto>(json, JsonDefaults.Read);
            if (dto?.ProjectSlug is null or "") return null;
            return new ProjectConfig(dto.ProjectSlug, dto.ApiPort);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates WorkspacePaths by walking up from <paramref name="baseDirectory"/> until a directory
    /// containing .ai-dev/project.json is found, otherwise falls back to the provided RootDir.
    /// </summary>
    public static WorkspacePaths CreateWorkspacePaths(string baseDirectory, RootDir? fallback)
    {
        var dir = Path.GetFullPath(baseDirectory);
        while (dir is not null)
        {
            var config = TryRead(dir);
            if (config is not null)
                return new WorkspacePaths(new RootDir(Path.Combine(dir, AiDevDirName)));
            dir = Path.GetDirectoryName(dir);
        }

        if (fallback is null)
            throw new InvalidOperationException(
                $"No .ai-dev/project.json found walking up from '{baseDirectory}' and no fallback RootDir was provided.");

        return new WorkspacePaths(fallback);
    }

    private sealed class ProjectConfigDto
    {
        public string? ProjectSlug { get; set; }
        public int ApiPort { get; set; }
    }
}
