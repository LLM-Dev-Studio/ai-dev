using System.Collections.ObjectModel;

using AiDev.Features.Planning;
using AiDev.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiDev.WinUI.ViewModels;

/// <summary>An item in the recent-sessions list shown in the session menu.</summary>
public sealed partial class PlanningSessionListItem : ObservableObject
{
    public string SessionId { get; init; } = "";
    public string DisplayId  { get; init; } = "";  // truncated UUID
    public string CreatedAt  { get; init; } = "";
    public string Phase      { get; init; } = "";
}

/// <summary>A single message in the conversation panel.</summary>
public sealed partial class PlanningMessageViewModel : ObservableObject
{
    [ObservableProperty] public partial string Content { get; set; } = "";
    [ObservableProperty] public partial bool IsUser { get; set; }
    [ObservableProperty] public partial bool IsStreaming { get; set; }
    [ObservableProperty] public partial bool WasFiltered { get; set; }
    public DateTimeOffset Timestamp { get; init; }
    public string TimeDisplay => Timestamp.LocalDateTime.ToString("HH:mm");
}

/// <summary>
/// ViewModel for the Planning Tasks Screen.
/// Manages three-phase planning conversation: Business Discovery, Solution Shaping,
/// and Planning &amp; Decomposition.
/// </summary>
public sealed partial class PlanningTasksViewModel : ObservableObject, IDisposable
{
    private readonly IPlanningSessionService _sessionService;
    private readonly IPlanningChatService    _chatService;
    private readonly MainViewModel           _mainViewModel;

    private CancellationTokenSource? _sendCts;

    // ── Session state ────────────────────────────────────────────────────────
    [ObservableProperty] public partial bool IsLoading            { get; set; }
    [ObservableProperty] public partial bool HasSession           { get; set; }
    [ObservableProperty] public partial string SessionDisplayId   { get; set; } = "";
    [ObservableProperty] public partial string? ActiveSessionId   { get; set; }

    // ── Phase state ──────────────────────────────────────────────────────────
    [ObservableProperty] public partial SessionPhase CurrentPhase { get; set; } = SessionPhase.Phase1BusinessDiscovery;
    [ObservableProperty] public partial bool Phase1Locked         { get; set; }
    [ObservableProperty] public partial bool Phase2Locked         { get; set; }

    // ── Conversation ─────────────────────────────────────────────────────────
    [ObservableProperty] public partial bool IsLlmResponding      { get; set; }
    [ObservableProperty] public partial string MessageInput       { get; set; } = "";
    [ObservableProperty] public partial string? ErrorMessage      { get; set; }

    // Turn counts per phase
    [ObservableProperty] public partial int Phase1TurnCount       { get; set; }
    [ObservableProperty] public partial int Phase2TurnCount       { get; set; }
    [ObservableProperty] public partial int Phase3TurnCount       { get; set; }

    // Turn limit banners
    [ObservableProperty] public partial bool ShowSoftWarning      { get; set; }
    [ObservableProperty] public partial bool ShowHardLimit        { get; set; }
    [ObservableProperty] public partial int  SoftLimitLastWarned  { get; set; }

    // ── DSL panel ────────────────────────────────────────────────────────────
    [ObservableProperty] public partial bool   IsDslPanelOpen   { get; set; }
    [ObservableProperty] public partial string DslContent       { get; set; } = "";
    [ObservableProperty] public partial bool   HasDraftDsl      { get; set; }
    [ObservableProperty] public partial string DslPanelTitle    { get; set; } = "DSL Output";

    // ── Phase review (read-only modal) ───────────────────────────────────────
    [ObservableProperty] public partial bool   IsPhaseReviewOpen      { get; set; }
    [ObservableProperty] public partial string ReviewPhaseTitle       { get; set; } = "";
    [ObservableProperty] public partial string ReviewConversation     { get; set; } = "";
    [ObservableProperty] public partial string ReviewDslContent       { get; set; } = "";

    public ObservableCollection<PlanningMessageViewModel>  Messages      { get; } = [];
    public ObservableCollection<PlanningSessionListItem>   RecentSessions { get; } = [];

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    public PlanningTasksViewModel(
        IPlanningSessionService sessionService,
        IPlanningChatService chatService,
        MainViewModel mainViewModel)
    {
        _sessionService = sessionService;
        _chatService    = chatService;
        _mainViewModel  = mainViewModel;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    public void Load()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            RefreshRecentSessions();
            var active = _sessionService.GetActiveSession(CurrentSlug);
            if (active != null)
            {
                LoadSession(active);
            }
            else
            {
                HasSession = false;
                ClearConversation();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load sessions: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadSession(PlanningSessionMetadata session)
    {
        ActiveSessionId  = session.Id;
        HasSession       = true;
        SessionDisplayId = TruncateId(session.Id);
        CurrentPhase     = session.CurrentPhase;
        Phase1Locked     = session.Phase1LockedAt.HasValue;
        Phase2Locked     = session.Phase2LockedAt.HasValue;

        // Load conversation for current phase
        var turns = _sessionService.GetConversationForPhase(CurrentSlug!, session.Id, session.CurrentPhase);
        Messages.Clear();
        foreach (var t in turns)
        {
            Messages.Add(new PlanningMessageViewModel
            {
                Content   = t.Content,
                IsUser    = t.Role == ConversationRole.User,
                Timestamp = t.Timestamp,
            });
        }

        // Load turn counts for all phases
        var allTurns = _sessionService.GetConversation(CurrentSlug!, session.Id);
        Phase1TurnCount = allTurns.Count(t => t.Phase == SessionPhase.Phase1BusinessDiscovery && t.Role == ConversationRole.User);
        Phase2TurnCount = allTurns.Count(t => t.Phase == SessionPhase.Phase2SolutionShaping   && t.Role == ConversationRole.User);
        Phase3TurnCount = allTurns.Count(t => t.Phase == SessionPhase.Phase3PlanningDecomposition && t.Role == ConversationRole.User);

        // Load draft DSL if available
        var draftDsl = _sessionService.GetDraftDsl(CurrentSlug!, session.Id, session.CurrentPhase);
        if (!string.IsNullOrEmpty(draftDsl))
        {
            DslContent  = draftDsl;
            HasDraftDsl = true;
        }

        UpdateDslPanelTitle();
        UpdateTurnLimitState();
    }

    private void RefreshRecentSessions()
    {
        RecentSessions.Clear();
        if (CurrentSlug is null) return;
        var sessions = _sessionService.ListSessions(CurrentSlug);
        foreach (var s in sessions.Take(10))
        {
            RecentSessions.Add(new PlanningSessionListItem
            {
                SessionId  = s.Id,
                DisplayId  = TruncateId(s.Id),
                CreatedAt  = s.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                Phase      = s.CurrentPhase.ToString().Replace("Phase", "Ph").Replace("BusinessDiscovery", "1").Replace("SolutionShaping", "2").Replace("PlanningDecomposition", "3"),
            });
        }
    }

    // ── Session commands ──────────────────────────────────────────────────────

    [RelayCommand]
    public async Task NewSessionAsync()
    {
        if (CurrentSlug is null) return;
        try
        {
            IsLoading = true;
            var session = await _sessionService.CreateSessionAsync(CurrentSlug);
            LoadSession(session);
            RefreshRecentSessions();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create session: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void LoadExistingSession(string sessionId)
    {
        if (CurrentSlug is null) return;
        var session = _sessionService.GetSession(CurrentSlug, sessionId);
        if (session != null) LoadSession(session);
    }

    // ── Conversation commands ─────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    public async Task SendMessageAsync()
    {
        if (!CanSendMessage() || CurrentSlug is null || ActiveSessionId is null) return;

        var userText = MessageInput.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        MessageInput     = "";
        IsLlmResponding  = true;
        ErrorMessage     = null;

        // Add user message to UI immediately
        var userTurn = new PlanningMessageViewModel
        {
            Content   = userText,
            IsUser    = true,
            Timestamp = DateTimeOffset.UtcNow,
        };
        Messages.Add(userTurn);

        // Persist user turn
        await _sessionService.AppendTurnAsync(CurrentSlug, ActiveSessionId,
            new ConversationTurn(ConversationRole.User, userText, DateTimeOffset.UtcNow, CurrentPhase, 0, 0));

        // Increment turn count
        IncrementTurnCount();

        // Placeholder for streaming AI response
        var aiPlaceholder = new PlanningMessageViewModel
        {
            Content     = "Generating response...",
            IsUser      = false,
            IsStreaming = true,
            Timestamp   = DateTimeOffset.UtcNow,
        };
        Messages.Add(aiPlaceholder);

        _sendCts?.Cancel();
        _sendCts = new CancellationTokenSource();
        var ct = _sendCts.Token;

        try
        {
            var history    = GetCurrentPhaseHistory(excludeLast: 1);
            PlanningChatResponse response;

            switch (CurrentPhase)
            {
                case SessionPhase.Phase1BusinessDiscovery:
                    response = await _chatService.SendPhase1MessageAsync(CurrentSlug!, history, userText, ct);
                    break;

                case SessionPhase.Phase2SolutionShaping:
                    var businessDsl2 = _sessionService.GetLockedDsl(CurrentSlug, ActiveSessionId, SessionPhase.Phase1BusinessDiscovery) ?? "";
                    response = await _chatService.SendPhase2MessageAsync(CurrentSlug!, history, businessDsl2, userText, ct);
                    break;

                case SessionPhase.Phase3PlanningDecomposition:
                    var businessDsl3 = _sessionService.GetLockedDsl(CurrentSlug, ActiveSessionId, SessionPhase.Phase1BusinessDiscovery) ?? "";
                    var solutionDsl3 = _sessionService.GetLockedDsl(CurrentSlug, ActiveSessionId, SessionPhase.Phase2SolutionShaping) ?? "";
                    response = await _chatService.SendPhase3MessageAsync(CurrentSlug!, history, businessDsl3, solutionDsl3, userText, ct);
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected phase: {CurrentPhase}");
            }

            // Update AI placeholder with real response
            aiPlaceholder.Content     = response.Content;
            aiPlaceholder.IsStreaming = false;
            aiPlaceholder.WasFiltered = response.WasFiltered;

            // Persist AI turn
            await _sessionService.AppendTurnAsync(CurrentSlug, ActiveSessionId,
                new ConversationTurn(ConversationRole.Assistant, response.Content, DateTimeOffset.UtcNow, CurrentPhase,
                    response.InputTokens, response.OutputTokens));

            // Update token count in metadata
            await _sessionService.UpdateTokenCountAsync(CurrentSlug, ActiveSessionId, CurrentPhase, response.InputTokens);

            // Check turn limits AFTER adding the pair
            UpdateTurnLimitState();
        }
        catch (OperationCanceledException)
        {
            aiPlaceholder.Content     = "(Response cancelled)";
            aiPlaceholder.IsStreaming = false;
        }
        catch (Exception ex)
        {
            aiPlaceholder.Content     = $"Error: {ex.Message}";
            aiPlaceholder.IsStreaming = false;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLlmResponding = false;
            SendMessageCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSendMessage()
        => HasSession && !IsLlmResponding && !ShowHardLimit && !string.IsNullOrWhiteSpace(MessageInput);

    [RelayCommand]
    public void CancelResponse()
    {
        _sendCts?.Cancel();
    }

    // ── DSL commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task GenerateDslAsync()
    {
        if (CurrentSlug is null || ActiveSessionId is null) return;
        IsLlmResponding = true;
        ErrorMessage    = null;
        try
        {
            string yaml;
            var history = GetCurrentPhaseHistory(excludeLast: 0);

            switch (CurrentPhase)
            {
                case SessionPhase.Phase1BusinessDiscovery:
                    yaml = await _chatService.GenerateBusinessDslAsync(CurrentSlug!, history);
                    break;

                case SessionPhase.Phase2SolutionShaping:
                    var businessDsl = _sessionService.GetLockedDsl(CurrentSlug, ActiveSessionId, SessionPhase.Phase1BusinessDiscovery) ?? "";
                    yaml = await _chatService.GenerateSolutionDslAsync(CurrentSlug!, history, businessDsl);
                    // Validate before displaying
                    var validation = SolutionDslValidator.Validate(yaml);
                    if (!validation.IsValid)
                    {
                        ErrorMessage = "Generated Solution.dsl has validation errors: " +
                                       string.Join("; ", validation.Errors.Select(e => e.Message));
                        // Still show it so user can see the issues
                    }
                    break;

                case SessionPhase.Phase3PlanningDecomposition:
                    var bDsl = _sessionService.GetLockedDsl(CurrentSlug, ActiveSessionId, SessionPhase.Phase1BusinessDiscovery) ?? "";
                    var sDsl = _sessionService.GetLockedDsl(CurrentSlug, ActiveSessionId, SessionPhase.Phase2SolutionShaping) ?? "";
                    yaml = await _chatService.GeneratePlanDslAsync(CurrentSlug!, history, bDsl, sDsl);
                    break;

                default:
                    return;
            }

            DslContent  = yaml;
            HasDraftDsl = true;
            IsDslPanelOpen = true;

            await _sessionService.SaveDraftDslAsync(CurrentSlug, ActiveSessionId, CurrentPhase, yaml);
            UpdateDslPanelTitle();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to generate DSL: {ex.Message}";
        }
        finally
        {
            IsLlmResponding = false;
        }
    }

    [RelayCommand]
    public async Task DiscardDraftAsync()
    {
        DslContent  = "";
        HasDraftDsl = false;
        UpdateDslPanelTitle();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Locks the current phase DSL. Returns a validation/error message if lock fails, null on success.
    /// </summary>
    public async Task<string?> LockPhaseAsync()
    {
        if (CurrentSlug is null || ActiveSessionId is null || string.IsNullOrWhiteSpace(DslContent))
            return "No DSL content to lock.";

        var result = await _sessionService.LockPhaseAsync(CurrentSlug, ActiveSessionId, CurrentPhase, DslContent);
        return result switch
        {
            Ok<Unit> => null,
            Err<Unit> err => err.Error.Message,
            _ => "Unexpected error during lock.",
        };
    }

    /// <summary>
    /// Called after a successful phase lock to advance the UI state.
    /// </summary>
    public void AdvancePhase()
    {
        switch (CurrentPhase)
        {
            case SessionPhase.Phase1BusinessDiscovery:
                Phase1Locked = true;
                CurrentPhase = SessionPhase.Phase2SolutionShaping;
                break;
            case SessionPhase.Phase2SolutionShaping:
                Phase2Locked = true;
                CurrentPhase = SessionPhase.Phase3PlanningDecomposition;
                break;
        }

        DslContent  = "";
        HasDraftDsl = false;
        ClearConversationMessages();
        UpdateDslPanelTitle();
        ShowSoftWarning = false;
        ShowHardLimit   = false;
    }

    // ── EC-4 escalation ───────────────────────────────────────────────────────

    public async Task<string> EscalateEc4Async(string requirement, string alternative)
    {
        if (CurrentSlug is null || ActiveSessionId is null) return "";
        return await _sessionService.CreateEc4EscalationAsync(CurrentSlug, ActiveSessionId, requirement, alternative);
    }

    // ── Phase review ──────────────────────────────────────────────────────────

    public void OpenPhaseReview(SessionPhase phase)
    {
        if (CurrentSlug is null || ActiveSessionId is null) return;
        if (phase == CurrentPhase) return; // Can only review locked phases

        var turns = _sessionService.GetConversationForPhase(CurrentSlug, ActiveSessionId, phase);
        var sb    = new System.Text.StringBuilder();
        foreach (var t in turns)
        {
            var role = t.Role == ConversationRole.User ? "You" : "AI";
            sb.AppendLine($"[{role}] {t.Content}");
            sb.AppendLine();
        }

        ReviewConversation = sb.ToString().Trim();
        ReviewDslContent   = _sessionService.GetLockedDsl(CurrentSlug, ActiveSessionId, phase) ?? "(No DSL locked)";
        ReviewPhaseTitle   = phase switch
        {
            SessionPhase.Phase1BusinessDiscovery     => "Phase 1 — Business Discovery (Read-Only)",
            SessionPhase.Phase2SolutionShaping       => "Phase 2 — Solution Shaping (Read-Only)",
            _ => "Phase Review (Read-Only)",
        };
        IsPhaseReviewOpen = true;
    }

    public void ClosePhaseReview() => IsPhaseReviewOpen = false;

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ClearConversation()
    {
        Messages.Clear();
        DslContent     = "";
        HasDraftDsl    = false;
        ShowSoftWarning = false;
        ShowHardLimit   = false;
        Phase1TurnCount = 0;
        Phase2TurnCount = 0;
        Phase3TurnCount = 0;
        UpdateDslPanelTitle();
    }

    private void ClearConversationMessages()
    {
        Messages.Clear();
        DslContent  = "";
        HasDraftDsl = false;
    }

    private void IncrementTurnCount()
    {
        switch (CurrentPhase)
        {
            case SessionPhase.Phase1BusinessDiscovery:     Phase1TurnCount++; break;
            case SessionPhase.Phase2SolutionShaping:       Phase2TurnCount++; break;
            case SessionPhase.Phase3PlanningDecomposition: Phase3TurnCount++; break;
        }
    }

    private int CurrentTurnCount => CurrentPhase switch
    {
        SessionPhase.Phase1BusinessDiscovery     => Phase1TurnCount,
        SessionPhase.Phase2SolutionShaping       => Phase2TurnCount,
        SessionPhase.Phase3PlanningDecomposition => Phase3TurnCount,
        _ => 0,
    };

    private void UpdateTurnLimitState()
    {
        var turns = CurrentTurnCount;
        ShowHardLimit = turns >= 40;

        // Soft warning: show at 25+ and remind every 5 turns after that
        ShowSoftWarning = !ShowHardLimit && turns >= 25 && turns != SoftLimitLastWarned;
        if (ShowSoftWarning) SoftLimitLastWarned = turns;
    }

    private void UpdateDslPanelTitle()
    {
        var phaseName = CurrentPhase switch
        {
            SessionPhase.Phase1BusinessDiscovery     => "Phase 1: Business Discovery",
            SessionPhase.Phase2SolutionShaping       => "Phase 2: Solution Shaping",
            SessionPhase.Phase3PlanningDecomposition => "Phase 3: Planning & Decomposition",
            _ => "",
        };
        var status = HasDraftDsl ? "Draft" : "Empty";
        DslPanelTitle = $"DSL Output — {phaseName} ({status})";
    }

    private IReadOnlyList<ConversationTurn> GetCurrentPhaseHistory(int excludeLast)
    {
        if (CurrentSlug is null || ActiveSessionId is null) return [];
        var turns = _sessionService.GetConversationForPhase(CurrentSlug, ActiveSessionId, CurrentPhase);
        if (excludeLast > 0 && turns.Count >= excludeLast)
            return turns.Take(turns.Count - excludeLast).ToList();
        return turns;
    }

    private static string TruncateId(string id)
    {
        if (id.Length <= 8) return id;
        return id[..4] + "..." + id[^4..];
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _sendCts?.Cancel();
        _sendCts?.Dispose();
    }
}
