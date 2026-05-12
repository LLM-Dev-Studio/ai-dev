using AiDev.Extensions;

namespace AiDev;

/// <summary>
/// Represents a strongly typed file-system path value.
/// </summary>
/// <param name="Value">The underlying path value.</param>
public abstract record FilePathBase(string Value)
{
    /// <summary>
    /// Gets the absolute normalized path.
    /// </summary>
    public string FullPath => Path.GetFullPath(Value);

    /// <summary>
    /// Converts the typed path to its underlying string value.
    /// </summary>
    /// <param name="path">The typed path value.</param>
    public static implicit operator string(FilePathBase path) => path.Value;
}

/// <summary>
/// Represents a strongly typed directory path.
/// </summary>
/// <param name="Value">The underlying directory path value.</param>
public abstract record DirPath(string Value) : FilePathBase(Value)
{
    /// <summary>
    /// Determines whether the directory exists.
    /// </summary>
    /// <returns><see langword="true"/> when the directory exists; otherwise, <see langword="false"/>.</returns>
    public bool Exists() => Directory.Exists(Value);

    /// <summary>
    /// Creates the directory if it does not already exist.
    /// </summary>
    public void Create() => Directory.CreateDirectory(Value);
}

/// <summary>
/// Represents a strongly typed file path.
/// </summary>
/// <param name="Value">The underlying file path value.</param>
public abstract record FilePath(string Value) : FilePathBase(Value)
{
    /// <summary>
    /// Determines whether the file exists.
    /// </summary>
    /// <returns><see langword="true"/> when the file exists; otherwise, <see langword="false"/>.</returns>
    public bool Exists() => File.Exists(Value);
}


/// <summary>Represents the workspace root directory.</summary>
public record RootDir(string Value) : DirPath(Value);
/// <summary>Represents the workspace registry file path.</summary>
public record RegistryFile(string Value) : FilePath(Value);
/// <summary>Represents the studio settings file path.</summary>
public record StudioSettingFile(string Value) : FilePath(Value);
/// <summary>Represents the agent templates directory path.</summary>
public record AgentTemplatesFile(string Value) : FilePath(Value);

/// <summary>Represents a project root directory.</summary>
public record ProjectDir(string Value) : DirPath(Value);
/// <summary>Represents a project metadata file path.</summary>
public record ProjectJsonFile(string Value) : FilePath(Value);
/// <summary>Represents a project agents directory.</summary>
public record AgentsDir(string Value) : DirPath(Value);
/// <summary>Represents a project board file path.</summary>
public record BoardFile(string Value) : FilePath(Value);
/// <summary>Represents a project's pending decisions directory.</summary>
public record DecisionsPendingDir(string Value) : DirPath(Value);
/// <summary>Represents a project's resolved decisions directory.</summary>
public record DecisionsResolvedDir(string Value) : DirPath(Value);
/// <summary>Represents a project's decision chats directory.</summary>
public record DecisionChatsDir(string Value) : DirPath(Value);
/// <summary>Represents a project's knowledge base directory.</summary>
public record KbDir(string Value) : DirPath(Value);
/// <summary>Represents a project's playbooks directory.</summary>
public record PlaybooksDir(string Value) : DirPath(Value);
/// <summary>Represents a playbook file path.</summary>
public record PlaybookFile(string Value) : FilePath(Value);

/// <summary>Represents an agent directory.</summary>
public record AgentDir(string Value) : DirPath(Value);
/// <summary>Represents an agent metadata file path.</summary>
public record AgentJsonFile(string Value) : FilePath(Value);
/// <summary>Represents an agent CLAUDE.md file path.</summary>
public record AgentClaudeMdFile(string Value) : FilePath(Value);
/// <summary>Represents an agent inbox directory.</summary>
public record AgentInboxDir(string Value) : DirPath(Value);
/// <summary>Represents an agent processed inbox directory.</summary>
public record AgentInboxProcessedDir(string Value) : DirPath(Value);
/// <summary>Represents an agent outbox directory.</summary>
public record AgentOutboxDir(string Value) : DirPath(Value);
/// <summary>Represents an agent journal directory.</summary>
public record AgentJournalDir(string Value) : DirPath(Value);
/// <summary>Represents an agent transcripts directory.</summary>
public record AgentTranscriptsDir(string Value) : DirPath(Value);

/// <summary>Represents a planning sessions directory.</summary>
public record PlanningSessionsDir(string Value) : DirPath(Value);
/// <summary>Represents a specific planning session directory.</summary>
public record PlanningSessionDir(string Value) : DirPath(Value);
/// <summary>Represents a planning session drafts directory.</summary>
public record PlanningSessionDraftsDir(string Value) : DirPath(Value);
/// <summary>Represents a planning session metadata file path.</summary>
public record PlanningSessionMetadataFile(string Value) : FilePath(Value);
/// <summary>Represents a planning session conversation file path.</summary>
public record PlanningSessionConversationFile(string Value) : FilePath(Value);
/// <summary>Represents a planning session DSL file path.</summary>
public record PlanningSessionDslFile(string Value) : FilePath(Value);

/// <summary>Represents a transcript file path.</summary>
public record TranscriptFile(string Value) : FilePath(Value);
/// <summary>Represents an insight file path.</summary>
public record InsightFile(string Value) : FilePath(Value);
/// <summary>Represents a secrets file path.</summary>
public record SecretsFile(string Value) : FilePath(Value);
/// <summary>Represents a knowledge base article file path.</summary>
public record KbArticleFile(string Value) : FilePath(Value);
/// <summary>Represents a playbook article file path.</summary>
public record PlaybookArticleFile(string Value) : FilePath(Value);
/// <summary>Represents a template file path.</summary>
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

    /// <summary>
    /// Initializes resolved workspace paths from the workspace root.
    /// </summary>
    /// <param name="root">The workspace root directory.</param>
    public WorkspacePaths(RootDir root)
    {
        Root = root;
        RegistryPath = Root.RegistryFile();
        StudioSettingsPath = Root.StudioSettingFile();
        AgentTemplatesDir = Root.AgentTemplatesFile();
    }

    /// <summary>Gets the project directory path.</summary>
    public ProjectDir ProjectDir(ProjectSlug p) => Root.ProjectDir(p);
    /// <summary>Gets the project metadata file path.</summary>
    public ProjectJsonFile ProjectJsonPath(ProjectSlug p) => ProjectDir(p).ProjectJsonFile();
    /// <summary>Gets the agents directory path.</summary>
    public AgentsDir AgentsDir(ProjectSlug p) => ProjectDir(p).AgentsDir();
    /// <summary>Gets the board file path.</summary>
    public BoardFile BoardPath(ProjectSlug p) => ProjectDir(p).BoardFile();
    /// <summary>Gets the pending decisions directory path.</summary>
    public DecisionsPendingDir DecisionsPendingDir(ProjectSlug p) => ProjectDir(p).DecisionsPendingDir();
    /// <summary>Gets the resolved decisions directory path.</summary>
    public DecisionsResolvedDir DecisionsResolvedDir(ProjectSlug p) => ProjectDir(p).DecisionsResolvedDir();
    /// <summary>Gets the decision chats directory path.</summary>
    public DecisionChatsDir DecisionChatsDir(ProjectSlug p) => ProjectDir(p).DecisionChatsDir();
    /// <summary>Gets the knowledge base directory path.</summary>
    public KbDir KbDir(ProjectSlug p) => ProjectDir(p).KbDir();
    /// <summary>Gets the secrets file path.</summary>
    public SecretsFile SecretsPath(ProjectSlug p) => ProjectDir(p).SecretsFile();
    /// <summary>Gets the playbooks directory path.</summary>
    public PlaybooksDir PlaybooksDir(ProjectSlug p) => ProjectDir(p).PlaybooksDir();

    /// <summary>Gets the agent directory path.</summary>
    public AgentDir AgentDir(ProjectSlug p, AgentSlug a) => AgentsDir(p).AgentDir(a);
    /// <summary>Gets the agent metadata file path.</summary>
    public AgentJsonFile AgentJsonPath(ProjectSlug p, AgentSlug a) => AgentDir(p, a).AgentJsonFile();
    /// <summary>Gets the agent CLAUDE.md file path.</summary>
    public AgentClaudeMdFile AgentClaudeMdPath(ProjectSlug p, AgentSlug a) => AgentDir(p, a).AgentClaudeMdFile();
    /// <summary>Gets the agent inbox directory path.</summary>
    public AgentInboxDir AgentInboxDir(ProjectSlug p, AgentSlug a) => AgentDir(p, a).AgentInboxDir();
    /// <summary>Gets the processed agent inbox directory path.</summary>
    public AgentInboxProcessedDir AgentInboxProcessedDir(ProjectSlug p, AgentSlug a) => AgentInboxDir(p, a).AgentInboxProcessedDir();
    /// <summary>Gets the agent outbox directory path.</summary>
    public AgentOutboxDir AgentOutboxDir(ProjectSlug p, AgentSlug a) => AgentDir(p, a).AgentOutboxDir();
    /// <summary>Gets the agent journal directory path.</summary>
    public AgentJournalDir AgentJournalDir(ProjectSlug p, AgentSlug a) => AgentDir(p, a).AgentJournalDir();
    /// <summary>Gets the agent transcripts directory path.</summary>
    public AgentTranscriptsDir AgentTranscriptsDir(ProjectSlug p, AgentSlug a) => AgentDir(p, a).AgentTranscriptsDir();

    /// <summary>Gets the planning sessions directory path.</summary>
    public PlanningSessionsDir PlanningSessionsDir(ProjectSlug p) =>
        new(Path.Combine(ProjectDir(p).Value, FilePathConstants.SessionsDirName, FilePathConstants.PlanningDirName));

    /// <summary>Gets a planning session directory path.</summary>
    public PlanningSessionDir PlanningSessionDir(ProjectSlug p, SessionId sessionId) =>
        new(Path.Combine(PlanningSessionsDir(p).Value, sessionId.Value));

    /// <summary>Gets a planning session drafts directory path.</summary>
    public PlanningSessionDraftsDir PlanningSessionDraftsDir(ProjectSlug p, SessionId sessionId) =>
        new(Path.Combine(PlanningSessionDir(p, sessionId).Value, FilePathConstants.DraftsDirName));

    /// <summary>Gets a planning session metadata file path.</summary>
    public PlanningSessionMetadataFile PlanningSessionMetadataPath(ProjectSlug p, SessionId sessionId) =>
        new(Path.Combine(PlanningSessionDir(p, sessionId).Value, FilePathConstants.PlanningMetadataFileName));

    /// <summary>Gets a planning session conversation file path.</summary>
    public PlanningSessionConversationFile PlanningSessionConversationPath(ProjectSlug p, SessionId sessionId) =>
        new(Path.Combine(PlanningSessionDir(p, sessionId).Value, FilePathConstants.PlanningConversationFileName));

    /// <summary>Gets a locked planning DSL file path.</summary>
    public PlanningSessionDslFile PlanningSessionLockedDslPath(ProjectSlug p, SessionId sessionId, string dslFileName) =>
        new(Path.Combine(PlanningSessionDir(p, sessionId).Value, dslFileName));

    /// <summary>Gets a draft planning DSL file path.</summary>
    public PlanningSessionDslFile PlanningSessionDraftDslPath(ProjectSlug p, SessionId sessionId, string dslFileName) =>
        new(Path.Combine(PlanningSessionDraftsDir(p, sessionId).Value, dslFileName));

    /// <summary>Gets a transcript file path.</summary>
    public TranscriptFile TranscriptPath(ProjectSlug p, AgentSlug a, TranscriptDate date) => AgentTranscriptsDir(p, a).TranscriptFile(date);
    /// <summary>Gets an insight file path.</summary>
    public InsightFile InsightPath(ProjectSlug p, AgentSlug a, TranscriptDate date) => AgentTranscriptsDir(p, a).InsightFile(date);
    /// <summary>Gets a safe knowledge base article file path when the slug is valid.</summary>
    public KbArticleFile? SafeKbArticlePath(ProjectSlug p, string slug) => KbDir(p).SafeKbArticleFile(slug);
    /// <summary>Gets a safe playbook file path when the slug is valid.</summary>
    public PlaybookArticleFile? SafePlaybookPath(ProjectSlug p, string slug) => PlaybooksDir(p).SafePlaybookFile(slug);
    /// <summary>Gets a safe template file path when the slug and extension are valid.</summary>
    public TemplateFile? SafeTemplatePath(string slug, string extension) => AgentTemplatesDir.SafeTemplateFile(slug, extension);
}
