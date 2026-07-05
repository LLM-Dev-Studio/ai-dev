namespace AiDev.Features.Workspace;

/// <summary>
/// Reads <c>.ai-dev/project.json</c> from a codebase directory.
/// </summary>
public static class ProjectConfigReader
{
    private const string AiDevDirName = ".ai-dev";
    private const string ConfigFileName = "project.json";

    /// <summary>
    /// Reads <c>.ai-dev/project.json</c> from <paramref name="baseDirectory"/>.
    /// Returns <see langword="null"/> if the file is absent, malformed, or missing
    /// <c>projectSlug</c>.
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
        catch { return null; }
    }

    /// <summary>
    /// Reads <c>.ai-dev/project.json</c> from <paramref name="baseDirectory"/>, including the
    /// optional display fields written by AI Dev Studio (<c>name</c>, <c>description</c>,
    /// <c>createdAt</c>).
    /// Returns <see langword="null"/> if the file is absent, malformed, or missing
    /// <c>projectSlug</c>.
    /// </summary>
    public static ProjectConfig? TryReadFull(string baseDirectory)
    {
        var configPath = Path.Combine(baseDirectory, AiDevDirName, ConfigFileName);
        if (!File.Exists(configPath)) return null;

        try
        {
            var json = File.ReadAllText(configPath);
            var dto = JsonSerializer.Deserialize<ProjectConfigDto>(json, JsonDefaults.Read);
            if (dto?.ProjectSlug is null or "") return null;

            DateTime? createdAt = DateTime.TryParse(dto.CreatedAt, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;

            return new ProjectConfig(dto.ProjectSlug, dto.ApiPort,
                Name: dto.Name, Description: dto.Description, CreatedAt: createdAt);
        }
        catch { return null; }
    }

    /// <summary>
    /// Creates <see cref="WorkspacePaths"/> by walking up from <paramref name="baseDirectory"/>
    /// until a directory containing <c>.ai-dev/project.json</c> is found, otherwise falls back
    /// to <paramref name="fallback"/>.
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
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? CreatedAt { get; set; }
    }
}
