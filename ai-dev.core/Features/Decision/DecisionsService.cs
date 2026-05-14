namespace AiDev.Features.Decision;

/// <summary>
/// Provides creation, lookup, and resolution operations for project decisions.
/// </summary>
public class DecisionsService(
    WorkspacePaths paths,
    IDomainEventDispatcher dispatcher,
    AtomicFileWriter fileWriter,
    ProjectMutationCoordinator coordinator,
    ILogger<DecisionsService> logger) : IDecisionsService
{
    private const string ResponseSeparator = "\n\n---\n\n## Human Response\n\n";
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates a new decision request.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decision.</param>
    /// <param name="from">The decision source.</param>
    /// <param name="subject">The decision subject.</param>
    /// <param name="priority">The decision priority.</param>
    /// <param name="blocks">The optional blocker reference.</param>
    /// <param name="body">The decision body content.</param>
    /// <returns>The result of the create operation.</returns>
    public Result<Unit> CreateDecision(ProjectSlug projectSlug, string from, string subject,
        Priority priority, string? blocks, string body)
    {
        return coordinator.Execute(projectSlug, () =>
        {
            using var activity = AiDevTelemetry.ActivitySource.StartActivity("Decision.Create", ActivityKind.Internal);
            activity?.SetTag("project.slug", projectSlug.Value);
            activity?.SetTag("decision.subject", subject);
            try
            {
                var now = DateTime.UtcNow;
                var slug = System.Text.RegularExpressions.Regex.Replace(
                    subject.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
                slug = slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
                var filename = $"{now:yyyyMMdd-HHmmss}-{slug}.md";

                var fields = new Dictionary<string, string>
                {
                    ["from"] = from,
                    ["date"] = now.ToString("o"),
                    ["priority"] = priority.Value,
                    ["subject"] = subject,
                    ["status"] = "pending",
                };
                if (!string.IsNullOrEmpty(blocks)) fields["blocks"] = blocks;

                var pendingDir = paths.DecisionsPendingDir(projectSlug);
                Directory.CreateDirectory(pendingDir);

                if (Directory.GetFiles(pendingDir, $"*-{slug}.md").Length > 0)
                    return (Result<Unit>)new Ok<Unit>(Unit.Value);

                fileWriter.WriteAllText(Path.Combine(pendingDir, filename),
                    FrontmatterParser.Stringify(fields, body));
                return (Result<Unit>)new Ok<Unit>(Unit.Value);
            }
            catch (IOException ex) { return (Result<Unit>)new Err<Unit>(new DomainError("DECISION_IO_ERROR", ex.Message)); }
            catch (UnauthorizedAccessException ex) { return (Result<Unit>)new Err<Unit>(new DomainError("DECISION_IO_ERROR", ex.Message)); }
        });
    }

    /// <summary>
    /// Lists decisions for a project filtered by status.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decisions.</param>
    /// <param name="status">The optional status filter.</param>
    /// <returns>The matching decisions.</returns>
    public List<DecisionItem> ListDecisions(ProjectSlug projectSlug, DecisionStatus? status = null)
    {
        var effectiveStatus = status ?? DecisionStatus.Pending;
        string[] dirs = effectiveStatus == DecisionStatus.Resolved
            ? [paths.DecisionsResolvedDir(projectSlug)]
            : [paths.DecisionsPendingDir(projectSlug)];

        var results = new List<DecisionItem>();
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.md").OrderByDescending(f => f))
            {
                var item = ParseDecisionFile(file);
                if (item != null) results.Add(item);
            }
        }
        return results;
    }

    /// <summary>
    /// Gets a decision by identifier.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decision.</param>
    /// <param name="id">The decision identifier.</param>
    /// <returns>The decision item, or <see langword="null"/> when not found.</returns>
    public DecisionItem? GetDecision(ProjectSlug projectSlug, DecisionId id)
    {
        var filename = $"{id.Value}.md";
        var pendingPath = Path.Combine(paths.DecisionsPendingDir(projectSlug), filename);
        if (File.Exists(pendingPath)) return ParseDecisionFile(pendingPath);
        var resolvedPath = Path.Combine(paths.DecisionsResolvedDir(projectSlug), filename);
        if (File.Exists(resolvedPath)) return ParseDecisionFile(resolvedPath);
        return null;
    }

    /// <summary>
    /// Resolves a decision with a human response.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decision.</param>
    /// <param name="id">The decision identifier.</param>
    /// <param name="response">The response used to resolve the decision.</param>
    /// <returns>The result of the resolve operation.</returns>
    public Task<Result<Unit>> ResolveDecisionAsync(ProjectSlug projectSlug, DecisionId id, string response)
        => ResolveDecisionAsync(projectSlug, id, response, CancellationToken.None);

    /// <summary>
    /// Resolves a decision with a human response.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decision.</param>
    /// <param name="id">The decision identifier.</param>
    /// <param name="response">The response used to resolve the decision.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The result of the resolve operation.</returns>
    public Task<Result<Unit>> ResolveDecisionAsync(ProjectSlug projectSlug, DecisionId id, string response, CancellationToken cancellationToken)
        => coordinator.ExecuteAsync(projectSlug, async () =>
        {
            using var activity = AiDevTelemetry.ActivitySource.StartActivity("Decision.Resolve", ActivityKind.Internal);
            activity?.SetTag("project.slug", projectSlug.Value);
            activity?.SetTag("decision.id", id.Value);
            return await GetPendingDecision(projectSlug, id)
                .Then(decision => PersistResolvedDecisionAsync(projectSlug, decision, response, cancellationToken)).ConfigureAwait(false);
        }, cancellationToken);

    private Result<DecisionItem> GetPendingDecision(ProjectSlug projectSlug, DecisionId id)
    {
        var decision = GetDecision(projectSlug, id);
        if (decision == null) return new Err<DecisionItem>(new DomainError("DECISION_NOT_FOUND", "Decision not found."));
        if (!decision.Status.IsPending) return new Err<DecisionItem>(new DomainError("DECISION_ALREADY_RESOLVED", "Decision is already resolved."));

        return new Ok<DecisionItem>(decision);
    }

    private async Task<Result<Unit>> PersistResolvedDecisionAsync(ProjectSlug projectSlug, DecisionItem decision, string response, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolvedAt = DateTime.UtcNow;
            decision.Resolve("human", response, resolvedAt);
            var updatedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["from"] = decision.From,
                ["date"] = decision.Date?.ToString("o") ?? string.Empty,
                ["priority"] = decision.Priority.Value,
                ["subject"] = decision.Subject,
                ["status"] = decision.Status.Value,
                ["resolvedAt"] = decision.ResolvedAt?.ToString("o") ?? string.Empty,
                ["resolvedBy"] = decision.ResolvedBy ?? string.Empty,
            };
            if (!string.IsNullOrEmpty(decision.Blocks)) updatedFields["blocks"] = decision.Blocks;

            var mainContent = FrontmatterParser.Stringify(updatedFields, decision.Body);
            var fullContent = mainContent + ResponseSeparator + decision.Response;

            var resolvedDir = paths.DecisionsResolvedDir(projectSlug);
            var destPath = Path.Combine(resolvedDir, decision.Filename);
            fileWriter.WriteAllText(destPath, fullContent);

            // Remove from pending
            var pendingPath = Path.Combine(paths.DecisionsPendingDir(projectSlug), decision.Filename);
            fileWriter.DeleteFile(pendingPath);

            var dispatchResult = await DispatchDecisionEventsAsync(decision.DequeueDomainEvents(), cancellationToken).ConfigureAwait(false);
            if (dispatchResult is Err<Unit> err)
                return err;

            return new Ok<Unit>(Unit.Value);
        }
        catch (ArgumentException ex) { return new Err<Unit>(new DomainError("DECISION_INVALID_RESPONSE", ex.Message)); }
        catch (IOException ex) { return new Err<Unit>(new DomainError("DECISION_IO_ERROR", ex.Message)); }
        catch (UnauthorizedAccessException ex) { return new Err<Unit>(new DomainError("DECISION_IO_ERROR", ex.Message)); }
    }

    private async Task<Result<Unit>> DispatchDecisionEventsAsync(IReadOnlyList<DomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        if (domainEvents.Count == 0)
            return new Ok<Unit>(Unit.Value);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DispatchTimeout);
        var dispatchResult = await dispatcher.Dispatch(domainEvents, timeoutCts.Token).ConfigureAwait(false);
        if (dispatchResult is Err<Unit> err)
        {
            logger.LogError("[decisions] Event dispatch failed: {Message}", err.Error.Message);
            return err;
        }

        return new Ok<Unit>(Unit.Value);
    }

    private static DecisionItem? ParseDecisionFile(string path)
    {
        try
        {
            var content = File.ReadAllText(path);
            string? response = null;
            string mainContent = content;

            var sepIdx = content.IndexOf(ResponseSeparator, StringComparison.Ordinal);
            if (sepIdx >= 0)
            {
                mainContent = content[..sepIdx];
                response = content[(sepIdx + ResponseSeparator.Length)..].Trim();
            }

            var (fields, body) = FrontmatterParser.Parse(mainContent);
            var filename = Path.GetFileName(path);
            var id = new DecisionId(Path.GetFileNameWithoutExtension(path));

            var dateStr = fields.GetValueOrDefault("date");
            var resolvedAtStr = fields.GetValueOrDefault("resolvedAt");
            return new(
                filename: filename,
                id: id,
                from: fields.GetValueOrDefault("from", string.Empty),
                subject: fields.GetValueOrDefault("subject", filename),
                body: body.Trim(),
                date: DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null,
                priority: Priority.From(fields.GetValueOrDefault("priority", Priority.Normal.Value)),
                status: DecisionStatus.From(fields.GetValueOrDefault("status", DecisionStatus.Pending.Value)),
                blocks: fields.TryGetValue("blocks", out var blocks) ? blocks : null,
                resolvedAt: DateTime.TryParse(resolvedAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var rat) ? rat : null,
                resolvedBy: fields.TryGetValue("resolvedBy", out var resolvedBy) ? resolvedBy : null,
                response: response);
        }
        catch { return null; }
    }
}
