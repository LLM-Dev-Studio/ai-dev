using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

using AiDev.Models;

namespace AiDev.Features.Planning;

/// <summary>
/// File-system-backed planning session service.
///
/// Sessions are stored at:
///   {projectDir}/sessions/planning/{sessionId}/
///     metadata.json          — session state and token counts
///     conversation.jsonl     — one JSON object per turn (append-only)
///     Business.dsl           — locked Phase 1 output (immutable after lock)
///     Solution.dsl           — locked Phase 2 output (immutable after lock)
///     Plan.dsl               — finalised Phase 3 output
///     drafts/
///       Business.dsl
///       Solution.dsl
///       Plan.dsl
/// </summary>
public sealed class PlanningSessionService(
    WorkspacePaths paths,
    AtomicFileWriter fileWriter,
    ILogger<PlanningSessionService> logger) : IPlanningSessionService
{
    private static readonly DomainError PhaseAlreadyLockedError =
        new("PLANNING_PHASE_LOCKED", "This phase is already locked and cannot be modified.");

    private static readonly DomainError InvalidPhaseTransitionError =
        new("PLANNING_INVALID_TRANSITION", "Phase transition conditions are not met.");

    private static readonly DomainError SolutionDslInvalidError =
        new("PLANNING_SOLUTION_DSL_INVALID", "Solution.dsl failed VSA stack validation. See validation errors for details.");

    // -------------------------------------------------------------------------
    // Session lifecycle
    // -------------------------------------------------------------------------

    public async Task<PlanningSessionMetadata> CreateSessionAsync(
        ProjectSlug projectSlug, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var metadata = new PlanningSessionMetadata
        {
            Id           = sessionId,
            CreatedAt    = now,
            CurrentPhase = SessionPhase.Phase1BusinessDiscovery,
            State        = PlanningSessionState.Active,
        };

        var sessionDir = paths.PlanningSessionDir(projectSlug, sessionId);
        sessionDir.Create();
        paths.PlanningSessionDraftsDir(projectSlug, sessionId).Create();

        // AD-08: Apply user-only ACL to session directory (Windows; gracefully degrades elsewhere).
        ApplySessionDirectoryPermissions(sessionDir.Value);

        await SaveMetadataAsync(projectSlug, sessionId, metadata, ct).ConfigureAwait(false);

        logger.LogInformation("[planning] Created session {SessionId} for project {ProjectSlug}",
            sessionId, projectSlug.Value);

        return metadata;
    }

    public PlanningSessionMetadata? GetActiveSession(ProjectSlug projectSlug)
    {
        var sessions = ListSessions(projectSlug);
        return sessions.FirstOrDefault(s => s.State != PlanningSessionState.Completed);
    }

    public PlanningSessionMetadata? GetSession(ProjectSlug projectSlug, string sessionId)
    {
        var metadataPath = paths.PlanningSessionMetadataPath(projectSlug, sessionId);
        if (!metadataPath.Exists()) return null;
        return ReadMetadata(metadataPath.Value);
    }

    public IReadOnlyList<PlanningSessionMetadata> ListSessions(ProjectSlug projectSlug)
    {
        var sessionsDir = paths.PlanningSessionsDir(projectSlug);
        if (!sessionsDir.Exists()) return [];

        var result = new List<PlanningSessionMetadata>();
        foreach (var dir in Directory.EnumerateDirectories(sessionsDir.Value))
        {
            var metadataPath = Path.Combine(dir, FilePathConstants.PlanningMetadataFileName);
            if (!File.Exists(metadataPath)) continue;

            var metadata = ReadMetadata(metadataPath);
            if (metadata != null) result.Add(metadata);
        }

        return [.. result.OrderByDescending(s => s.CreatedAt)];
    }

    // -------------------------------------------------------------------------
    // Conversation
    // -------------------------------------------------------------------------

    public IReadOnlyList<ConversationTurn> GetConversation(ProjectSlug projectSlug, string sessionId)
    {
        var path = paths.PlanningSessionConversationPath(projectSlug, sessionId);
        if (!path.Exists()) return [];
        return ReadConversation(path.Value);
    }

    public IReadOnlyList<ConversationTurn> GetConversationForPhase(
        ProjectSlug projectSlug, string sessionId, SessionPhase phase)
        => GetConversation(projectSlug, sessionId)
            .Where(t => t.Phase == phase)
            .ToList();

    public async Task AppendTurnAsync(
        ProjectSlug projectSlug, string sessionId, ConversationTurn turn, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = paths.PlanningSessionConversationPath(projectSlug, sessionId);
        var dto  = ConversationTurnToDto(turn);
        var line = JsonSerializer.Serialize(dto, JsonDefaults.WriteCompact) + "\n";

        // Append to .jsonl — use streaming append for safety.
        await using var stream = new FileStream(
            path.Value, FileMode.Append, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(line), ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // DSL files
    // -------------------------------------------------------------------------

    public async Task SaveDraftDslAsync(
        ProjectSlug projectSlug, string sessionId, SessionPhase phase,
        string yamlContent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var dslFileName = DslFileName(phase);
        var draftPath   = paths.PlanningSessionDraftDslPath(projectSlug, sessionId, dslFileName);

        paths.PlanningSessionDraftsDir(projectSlug, sessionId).Create();
        await WriteYamlAsync(draftPath.Value, yamlContent, ct).ConfigureAwait(false);

        logger.LogDebug("[planning] Saved draft {Phase} DSL for session {SessionId}", phase, sessionId);
    }

    public async Task<Result<Unit>> LockPhaseAsync(
        ProjectSlug projectSlug, string sessionId, SessionPhase phase,
        string yamlContent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var metadata = GetSession(projectSlug, sessionId);
        if (metadata == null)
            return new Err<Unit>(new DomainError("PLANNING_SESSION_NOT_FOUND", $"Session {sessionId} not found."));

        // Guard: already locked?
        if (IsLocked(metadata, phase))
            return new Err<Unit>(PhaseAlreadyLockedError);

        // Guard: phase transition order
        if (!CanLockPhase(metadata, phase))
            return new Err<Unit>(InvalidPhaseTransitionError);

        // AD-10: Validate Solution.dsl before locking Phase 2.
        if (phase == SessionPhase.Phase2SolutionShaping)
        {
            var validationResult = SolutionDslValidator.Validate(yamlContent);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("[planning] Solution.dsl validation failed for session {SessionId}: {Errors}",
                    sessionId, string.Join("; ", validationResult.Errors.Select(e => e.Message)));
                return new Err<Unit>(SolutionDslInvalidError with
                {
                    Message = string.Join(" | ", validationResult.Errors.Select(e => e.Message)),
                });
            }
        }

        // Write locked DSL (immutable after this point)
        var dslFileName  = DslFileName(phase);
        var lockedPath   = paths.PlanningSessionLockedDslPath(projectSlug, sessionId, dslFileName);

        if (lockedPath.Exists())
        {
            // Locked file already exists — reject silently to preserve immutability.
            logger.LogWarning("[planning] Attempt to overwrite locked {Phase} DSL for session {SessionId} — rejected",
                phase, sessionId);
            return new Err<Unit>(PhaseAlreadyLockedError);
        }

        await WriteYamlAsync(lockedPath.Value, yamlContent, ct).ConfigureAwait(false);

        // AD-07 control 3: mark locked DSL file read-only as defence-in-depth.
        SetFileReadOnly(lockedPath.Value);

        // Update metadata state
        var now = DateTimeOffset.UtcNow;
        switch (phase)
        {
            case SessionPhase.Phase1BusinessDiscovery:
                metadata.State        = PlanningSessionState.Phase1Locked;
                metadata.Phase1LockedAt = now;
                metadata.CurrentPhase = SessionPhase.Phase2SolutionShaping;
                break;

            case SessionPhase.Phase2SolutionShaping:
                metadata.State        = PlanningSessionState.Phase2Locked;
                metadata.Phase2LockedAt = now;
                metadata.CurrentPhase = SessionPhase.Phase3PlanningDecomposition;
                break;

            case SessionPhase.Phase3PlanningDecomposition:
                metadata.State      = PlanningSessionState.Completed;
                metadata.CompletedAt = now;
                break;
        }

        await SaveMetadataAsync(projectSlug, sessionId, metadata, ct).ConfigureAwait(false);

        logger.LogInformation("[planning] Locked {Phase} for session {SessionId}", phase, sessionId);
        return new Ok<Unit>(Unit.Value);
    }

    public async Task UpdateTokenCountAsync(
        ProjectSlug projectSlug, string sessionId, SessionPhase phase,
        int inputTokens, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var metadata = GetSession(projectSlug, sessionId);
        if (metadata == null) return;

        metadata.SetPhaseTokens(phase, inputTokens);
        await SaveMetadataAsync(projectSlug, sessionId, metadata, ct).ConfigureAwait(false);
    }

    public string? GetLockedDsl(ProjectSlug projectSlug, string sessionId, SessionPhase phase)
    {
        var path = paths.PlanningSessionLockedDslPath(projectSlug, sessionId, DslFileName(phase));
        if (!path.Exists()) return null;
        try { return File.ReadAllText(path.Value, Encoding.UTF8); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[planning] Failed to read locked {Phase} DSL for session {SessionId}", phase, sessionId);
            return null;
        }
    }

    public string? GetDraftDsl(ProjectSlug projectSlug, string sessionId, SessionPhase phase)
    {
        var path = paths.PlanningSessionDraftDslPath(projectSlug, sessionId, DslFileName(phase));
        if (!path.Exists()) return null;
        try { return File.ReadAllText(path.Value, Encoding.UTF8); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[planning] Failed to read draft {Phase} DSL for session {SessionId}", phase, sessionId);
            return null;
        }
    }

    public async Task<string> CreateEc4EscalationAsync(
        ProjectSlug projectSlug,
        string sessionId,
        string unsupportedRequirement,
        string closestAlternative,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var decisionsDir = paths.DecisionsPendingDir(projectSlug);
        decisionsDir.Create();

        var now           = DateTimeOffset.UtcNow;
        var timestamp     = now.ToString("yyyyMMdd-HHmmss");
        var subjectSlug   = "unsupported-vsa-requirement";
        var fileName      = $"{timestamp}-{subjectSlug}.md";
        var filePath      = Path.Combine(decisionsDir.Value, fileName);

        var content = $"""
            ---
            from: planning-ui
            date: {now:yyyy-MM-ddTHH:mm:ssZ}
            priority: high
            subject: Unsupported VSA requirement — {TruncateForSlug(unsupportedRequirement, 60)}
            status: pending
            blocks: Phase 2 lock and Plan.dsl generation cannot proceed until this is resolved
            session-id: {sessionId}
            ---

            ## Unsupported Requirement

            ```
            {unsupportedRequirement}
            ```

            ## Why It's Unsupported

            The stated requirement cannot be fulfilled using the supported VSA stack for ai-dev-studio
            projects. The VSA stack supports: API, Infrastructure, SharedContractsSDK, MauiHybridUI,
            and Worker project types with Auth, CQRS, EFCore, Observability, Validation, Caching,
            and Messaging cross-cutting modules.

            ## Closest Supported Alternative

            ```
            {closestAlternative}
            ```

            ## User Decision

            The user has declined the alternative. They require architect or human decision to either:
            1. Accept the supported alternative and proceed.
            2. Approve a non-standard solution (requires explicit override of VSA stack constraints).
            3. Pause planning and defer to a later phase.
            """;

        await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct)
            .ConfigureAwait(false);

        logger.LogInformation("[planning] Created EC-4 escalation decision file: {FilePath}", filePath);
        return filePath;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task SaveMetadataAsync(
        ProjectSlug projectSlug, string sessionId,
        PlanningSessionMetadata metadata, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = paths.PlanningSessionMetadataPath(projectSlug, sessionId);
        var json = JsonSerializer.Serialize(MetadataToDto(metadata), JsonDefaults.Write);
        fileWriter.WriteAllText(path, json);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private PlanningSessionMetadata? ReadMetadata(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var dto  = JsonSerializer.Deserialize<PlanningSessionMetadataDto>(json, JsonDefaults.Read);
            return dto == null ? null : DtoToMetadata(dto);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[planning] Failed to read session metadata from {Path}", filePath);
            return null;
        }
    }

    private static IReadOnlyList<ConversationTurn> ReadConversation(string filePath)
    {
        var turns = new List<ConversationTurn>();
        try
        {
            foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var dto = JsonSerializer.Deserialize<ConversationTurnDto>(line, JsonDefaults.Read);
                if (dto != null) turns.Add(DtoToTurn(dto));
            }
        }
        catch { /* return partial results on read failure */ }
        return turns;
    }

    private static async Task WriteYamlAsync(string filePath, string yamlContent, CancellationToken ct)
    {
        // Normalise to UTF-8 LF (as required by AD-03).
        var normalised = yamlContent.Replace("\r\n", "\n").Replace("\r", "\n");
        await File.WriteAllTextAsync(filePath, normalised, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// AD-08: Restrict session directory to the current Windows user only.
    /// Gracefully degrades on non-Windows platforms and network drives.
    /// </summary>
    private void ApplySessionDirectoryPermissions(string directoryPath)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var dirInfo     = new DirectoryInfo(directoryPath);
            var dirSecurity = dirInfo.GetAccessControl();

            // Remove inherited permissions and start fresh.
            dirSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // Grant full control to the current user only.
            var currentUser = WindowsIdentity.GetCurrent().User!;
            dirSecurity.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(dirSecurity);
            logger.LogInformation("[planning] Session directory created with user-only permissions: {Path}", directoryPath);
        }
        catch (Exception ex)
        {
            // Non-critical: log warning and continue (e.g. network drives do not support ACLs).
            logger.LogWarning(ex, "[planning] Could not set user-only ACL on session directory {Path} — proceeding without ACL", directoryPath);
        }
    }

    /// <summary>
    /// AD-07 control 3: Mark a locked DSL file as read-only to prevent direct disk edits.
    /// Silently degrades on file systems that do not support the attribute.
    /// </summary>
    private static void SetFileReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
        }
        catch
        {
            // Non-critical hardening; silently degrade.
        }
    }

    private static string TruncateForSlug(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength].TrimEnd() + "...";
    }

    private static string DslFileName(SessionPhase phase) => phase switch
    {
        SessionPhase.Phase1BusinessDiscovery    => FilePathConstants.BusinessDslFileName,
        SessionPhase.Phase2SolutionShaping      => FilePathConstants.SolutionDslFileName,
        SessionPhase.Phase3PlanningDecomposition => FilePathConstants.PlanDslFileName,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    private static bool IsLocked(PlanningSessionMetadata metadata, SessionPhase phase) => phase switch
    {
        SessionPhase.Phase1BusinessDiscovery    => metadata.Phase1LockedAt.HasValue,
        SessionPhase.Phase2SolutionShaping      => metadata.Phase2LockedAt.HasValue,
        SessionPhase.Phase3PlanningDecomposition => metadata.State == PlanningSessionState.Completed,
        _ => false,
    };

    private static bool CanLockPhase(PlanningSessionMetadata metadata, SessionPhase phase) => phase switch
    {
        SessionPhase.Phase1BusinessDiscovery     => true,
        SessionPhase.Phase2SolutionShaping       => metadata.Phase1LockedAt.HasValue,
        SessionPhase.Phase3PlanningDecomposition => metadata.Phase2LockedAt.HasValue,
        _ => false,
    };

    // -------------------------------------------------------------------------
    // DTO mapping
    // -------------------------------------------------------------------------

    private sealed record PlanningSessionMetadataDto(
        string Id, DateTimeOffset CreatedAt, string CurrentPhase, string State,
        DateTimeOffset? Phase1LockedAt, DateTimeOffset? Phase2LockedAt, DateTimeOffset? CompletedAt,
        int Phase1InputTokens, int Phase2InputTokens, int Phase3InputTokens);

    private sealed record ConversationTurnDto(
        string Role, string Content, DateTimeOffset Timestamp, string Phase,
        int InputTokens, int OutputTokens);

    private static PlanningSessionMetadataDto MetadataToDto(PlanningSessionMetadata m) =>
        new(m.Id, m.CreatedAt, m.CurrentPhase.ToString(), m.State.ToString(),
            m.Phase1LockedAt, m.Phase2LockedAt, m.CompletedAt,
            m.Phase1InputTokens, m.Phase2InputTokens, m.Phase3InputTokens);

    private static PlanningSessionMetadata DtoToMetadata(PlanningSessionMetadataDto dto)
    {
        Enum.TryParse<SessionPhase>(dto.CurrentPhase, out var phase);
        Enum.TryParse<PlanningSessionState>(dto.State, out var state);
        return new PlanningSessionMetadata
        {
            Id                  = dto.Id,
            CreatedAt           = dto.CreatedAt,
            CurrentPhase        = phase,
            State               = state,
            Phase1LockedAt      = dto.Phase1LockedAt,
            Phase2LockedAt      = dto.Phase2LockedAt,
            CompletedAt         = dto.CompletedAt,
            Phase1InputTokens   = dto.Phase1InputTokens,
            Phase2InputTokens   = dto.Phase2InputTokens,
            Phase3InputTokens   = dto.Phase3InputTokens,
        };
    }

    private static ConversationTurnDto ConversationTurnToDto(ConversationTurn t) =>
        new(t.Role.ToString(), t.Content, t.Timestamp, t.Phase.ToString(), t.InputTokens, t.OutputTokens);

    private static ConversationTurn DtoToTurn(ConversationTurnDto dto)
    {
        Enum.TryParse<ConversationRole>(dto.Role, out var role);
        Enum.TryParse<SessionPhase>(dto.Phase, out var phase);
        return new ConversationTurn(role, dto.Content, dto.Timestamp, phase, dto.InputTokens, dto.OutputTokens);
    }
}
