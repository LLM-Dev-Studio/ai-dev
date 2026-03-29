using AiDevNet.Models.Types;

namespace AiDevNet.Extensions;

public static class WorkspacePathsExtensions
{
    extension(RootDir dir)
    {
        public RegistryFile RegistryFile() => new(Path.Combine(dir.Value, FilePathConstants.RegistryFileName));
        public StudioSettingFile StudioSettingFile() => new(Path.Combine(dir.Value, FilePathConstants.StudioSettingsFileName));
        public AgentTemplatesFile AgentTemplatesFile() => new(Path.Combine(dir.Value, FilePathConstants.AgentTemplatesDirName));
        public ProjectDir ProjectDir(ProjectSlug slug) => new(Path.Combine(dir.Value, slug.Value));
    }

    extension(IWebHostEnvironment env)
    {
        public RootDir RootDir()
        {
            var envVar = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
            if (string.IsNullOrEmpty(envVar))
                return new(Path.GetFullPath(
                    Path.Combine(env.ContentRootPath, "..", FilePathConstants.WorkspacesDirName)));

            // Reject relative paths and UNC paths (\\server\share) — must be an absolute local path.
            if (!Path.IsPathFullyQualified(envVar) || envVar.StartsWith(@"\\"))
                throw new InvalidOperationException(
                    $"WORKSPACE_ROOT must be an absolute local path, got: '{envVar}'");

            return new(envVar);
        }
    }

    extension(ProjectDir dir)
    {

        public ProjectJsonFile ProjectJsonFile() =>
            new(Path.Combine(dir.Value, FilePathConstants.ProjectJsonFileName));

        public AgentsDir AgentsDir() =>
            new(Path.Combine(dir.Value, FilePathConstants.AgentsDirName));

        public BoardFile BoardFile() => new(Path.Combine(dir.Value,
            FilePathConstants.BoardDirName, FilePathConstants.BoardJsonFileName));

        public DecisionsPendingDir DecisionsPendingDir() => new(Path.Combine(dir.Value,
            FilePathConstants.DecisionsDirName, FilePathConstants.PendingDirName));

        public DecisionsResolvedDir DecisionsResolvedDir() => new(Path.Combine(dir.Value,
            FilePathConstants.DecisionsDirName, FilePathConstants.ResolvedDirName));

        public KbDir KbDir() => new(Path.Combine(dir.Value, FilePathConstants.KbDirName));
    }

    extension(AgentsDir dir)
    {
        public AgentDir AgentDir(Models.AgentSlug slug) => new(Path.Combine(dir.Value, slug.Value));
    }

    extension(AgentDir dir)
    {
        public AgentJsonFile AgentJsonFile() =>
            new(Path.Combine(dir.Value, FilePathConstants.AgentJsonFileName));

        public AgentClaudeMdFile AgentClaudeMdFile() =>
            new(Path.Combine(dir.Value, FilePathConstants.AgentClaudeMdFileName));

        public AgentInboxDir AgentInboxDir() =>
            new(Path.Combine(dir.Value, FilePathConstants.AgentInboxDirName));

        public AgentOutboxDir AgentOutboxDir() =>
            new(Path.Combine(dir.Value, FilePathConstants.OutboxDirName));

        public AgentJournalDir AgentJournalDir() =>
            new(Path.Combine(dir.Value, FilePathConstants.JournalDirName));

        public AgentTranscriptsDir AgentTranscriptsDir() =>
            new(Path.Combine(dir.Value, FilePathConstants.TranscriptsDirName));
    }

    extension(AgentInboxDir dir)
    {
        public AgentInboxProcessedDir AgentInboxProcessedDir() =>
            new(Path.Combine(dir.Value, FilePathConstants.ProcessedDirName));
    }

    /// <summary>Returns the transcript .md path for a validated date. Cannot escape the directory.</summary>
    public static TranscriptFile TranscriptFile(this AgentTranscriptsDir dir, TranscriptDate date) =>
    new(Path.Combine(dir.Value, $"{date}.md"));

    /// <summary>Returns the KB article .md path for a user-supplied slug, or null if it escapes the directory.</summary>
    public static KbArticleFile? SafeKbArticleFile(this KbDir dir, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var fullDir = dir.FullPath;
        var resolved = Path.Combine(fullDir, $"{slug}.md");
        return resolved.StartsWith(fullDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? new(resolved) : null;
    }

    /// <summary>Returns the template path for a user-supplied slug and extension, or null if it escapes the directory.</summary>
    public static TemplateFile? SafeTemplateFile(this AgentTemplatesFile dir, string slug, string extension)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var fullDir = dir.FullPath;
        var resolved = Path.GetFullPath(Path.Combine(fullDir, $"{slug}{extension}"));
        return resolved.StartsWith(fullDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? new(resolved) : null;
    }
}
