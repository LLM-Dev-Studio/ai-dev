namespace AiDev.Features.Decision;

public class DecisionsService(
    WorkspacePaths paths,
    IDomainEventDispatcher dispatcher,
    AtomicFileWriter fileWriter,
    ProjectMutationCoordinator coordinator,
    ILogger<DecisionsService> logger)
{
    private const string ResponseSeparator = "\n\n---\n\n## Human Response\n\n";
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(10);

    public Result<Unit> CreateDecision(ProjectSlug projectSlug, string from, string subject,
        string priority, string? blocks, string body)
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
                    ["priority"] = priority,
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

    public List<DecisionItem> ListDecisions(ProjectSlug projectSlug, string status = "pending")
    {
        string[] dirs = status == "resolved"
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

    public DecisionItem? GetDecision(ProjectSlug projectSlug, string id)
    {
        var filename = $"{id}.md";
        var pendingPath = Path.Combine(paths.DecisionsPendingDir(projectSlug), filename);
        if (File.Exists(pendingPath)) return ParseDecisionFile(pendingPath);
        var resolvedPath = Path.Combine(paths.DecisionsResolvedDir(projectSlug), filename);
        if (File.Exists(resolvedPath)) return ParseDecisionFile(resolvedPath);
        return null;
    }

    public Task<Result<Unit>> ResolveDecisionAsync(ProjectSlug projectSlug, string id, string response)
        => ResolveDecisionAsync(projectSlug, id, response, CancellationToken.None);

    public Task<Result<Unit>> ResolveDecisionAsync(ProjectSlug projectSlug, string id, string response, CancellationToken cancellationToken)
        => coordinator.ExecuteAsync(projectSlug, async () =>
        {
            using var activity = AiDevTelemetry.ActivitySource.StartActivity("Decision.Resolve", ActivityKind.Internal);
            activity?.SetTag("project.slug", projectSlug.Value);
            activity?.SetTag("decision.id", id);
            return await GetPendingDecision(projectSlug, id)
                .Then(decision => PersistResolvedDecisionAsync(projectSlug, decision, response, cancellationToken)).ConfigureAwait(false);
        }, cancellationToken);

    private Result<DecisionItem> GetPendingDecision(ProjectSlug projectSlug, string id)
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
            var id = Path.GetFileNameWithoutExtension(path);

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
