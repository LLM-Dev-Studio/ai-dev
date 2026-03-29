using AiDevNet.Models.Types;

namespace AiDevNet;

public abstract record FilePathBase(string Value)
{
    public string FullPath => Path.GetFullPath(Value);

    public static implicit operator string(FilePathBase path) => path.Value;
}

public abstract record DirPath(string Value) : FilePathBase(Value)
{
    public bool Exists() => Directory.Exists(Value);
    public void Create() => Directory.CreateDirectory(Value);
}

public abstract record FilePath(string Value) : FilePathBase(Value)
{
    public bool Exists() => File.Exists(Value);
}


public record RootDir(string Value) : DirPath(Value);
public record RegistryFile(string Value) : FilePath(Value);
public record StudioSettingFile(string Value) : FilePath(Value);
public record AgentTemplatesFile(string Value) : FilePath(Value);

public record ProjectDir(string Value) : DirPath(Value);
public record ProjectJsonFile(string Value) : FilePath(Value);
public record AgentsDir(string Value) : DirPath(Value);
public record BoardFile(string Value) : FilePath(Value);
public record DecisionsPendingDir(string Value) : DirPath(Value);
public record DecisionsResolvedDir(string Value) : DirPath(Value);
public record KbDir(string Value) : DirPath(Value);

public record AgentDir(string Value) : DirPath(Value);
public record AgentJsonFile(string Value) : FilePath(Value);
public record AgentClaudeMdFile(string Value) : FilePath(Value);
public record AgentInboxDir(string Value) : DirPath(Value);
public record AgentInboxProcessedDir(string Value) : DirPath(Value);
public record AgentOutboxDir(string Value) : DirPath(Value);
public record AgentJournalDir(string Value) : DirPath(Value);
public record AgentTranscriptsDir(string Value) : DirPath(Value);

public record TranscriptFile(string Value) : FilePath(Value);
public record KbArticleFile(string Value) : FilePath(Value);
public record TemplateFile(string Value) : FilePath(Value);

/// <summary>
/// Resolved once at startup; provides every known file-system location within the workspace.
/// Register as a singleton so the workspace root is calculated only once.
/// </summary>
public class WorkspacePaths
{
    /// <summary>Absolute path to the workspace root directory.</summary>
    public RootDir Root { get; }

    /// <summary>Path to workspaces.json (the project registry).</summary>
    public RegistryFile RegistryPath { get; }

    /// <summary>Path to studio-settings.json.</summary>
    public StudioSettingFile StudioSettingsPath { get; }

    /// <summary>Directory containing agent template files.</summary>
    public AgentTemplatesFile AgentTemplatesDir { get; }

    public WorkspacePaths(IWebHostEnvironment env)
    {
        Root = env.RootDir();
        RegistryPath = Root.RegistryFile();
        StudioSettingsPath = Root.StudioSettingFile();
        AgentTemplatesDir = Root.AgentTemplatesFile();
    }

    public ProjectDir ProjectDir(ProjectSlug p) => Root.ProjectDir(p);
    public ProjectJsonFile ProjectJsonPath(ProjectSlug p) => ProjectDir(p).ProjectJsonFile();
    public AgentsDir AgentsDir(ProjectSlug p) => ProjectDir(p).AgentsDir();
    public BoardFile BoardPath(ProjectSlug p) => ProjectDir(p).BoardFile();
    public DecisionsPendingDir DecisionsPendingDir(ProjectSlug p) => ProjectDir(p).DecisionsPendingDir();
    public DecisionsResolvedDir DecisionsResolvedDir(ProjectSlug p) => ProjectDir(p).DecisionsResolvedDir();
    public KbDir KbDir(ProjectSlug p) => ProjectDir(p).KbDir();

    public AgentDir AgentDir(ProjectSlug p, Models.AgentSlug a) => AgentsDir(p).AgentDir(a);
    public AgentJsonFile AgentJsonPath(ProjectSlug p, Models.AgentSlug a) => AgentDir(p, a).AgentJsonFile();
    public AgentClaudeMdFile AgentClaudeMdPath(ProjectSlug p, Models.AgentSlug a) => AgentDir(p, a).AgentClaudeMdFile();
    public AgentInboxDir AgentInboxDir(ProjectSlug p, Models.AgentSlug a) => AgentDir(p, a).AgentInboxDir();
    public AgentInboxProcessedDir AgentInboxProcessedDir(ProjectSlug p, Models.AgentSlug a) => AgentInboxDir(p, a).AgentInboxProcessedDir();
    public AgentOutboxDir AgentOutboxDir(ProjectSlug p, Models.AgentSlug a) => AgentDir(p, a).AgentOutboxDir();
    public AgentJournalDir AgentJournalDir(ProjectSlug p, Models.AgentSlug a) => AgentDir(p, a).AgentJournalDir();
    public AgentTranscriptsDir AgentTranscriptsDir(ProjectSlug p, Models.AgentSlug a) => AgentDir(p, a).AgentTranscriptsDir();

    public TranscriptFile TranscriptPath(ProjectSlug p, Models.AgentSlug a, TranscriptDate date) => AgentTranscriptsDir(p, a).TranscriptFile(date);
    public KbArticleFile? SafeKbArticlePath(ProjectSlug p, string slug) => KbDir(p).SafeKbArticleFile(slug);
    public TemplateFile? SafeTemplatePath(string slug, string extension) => AgentTemplatesDir.SafeTemplateFile(slug, extension);
}
