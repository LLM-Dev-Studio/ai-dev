namespace AiDevNet.Services;

public class DecisionItem
{
    public string Filename { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Blocks { get; set; }
    public string? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Response { get; set; }
}

public class DecisionsService(WorkspacePaths paths)
{
    private const string ResponseSeparator = "\n\n---\n\n## Human Response\n\n";

    public string? CreateDecision(ProjectSlug projectSlug, string from, string subject,
        string priority, string? blocks, string body)
    {
        try
        {
            var now = DateTime.UtcNow;
            var slug = System.Text.RegularExpressions.Regex.Replace(
                subject.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            slug = slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
            var filename = $"{now:yyyyMMdd-HHmmss}-{slug}.md";

            var fields = new Dictionary<string, string>
            {
                ["from"]     = from,
                ["date"]     = now.ToString("o"),
                ["priority"] = priority,
                ["subject"]  = subject,
                ["status"]   = "pending",
            };
            if (!string.IsNullOrEmpty(blocks)) fields["blocks"] = blocks;

            var pendingDir = paths.DecisionsPendingDir(projectSlug);
            Directory.CreateDirectory(pendingDir);
            File.WriteAllText(Path.Combine(pendingDir, filename),
                FrontmatterParser.Stringify(fields, body));
            return null;
        }
        catch (Exception ex) { return ex.Message; }
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

    public string? ResolveDecision(ProjectSlug projectSlug, string id, string response)
    {
        var decision = GetDecision(projectSlug, id);
        if (decision == null) return "Decision not found.";
        if (decision.Status != "pending") return "Decision is already resolved.";

        try
        {
            var resolvedAt = DateTime.UtcNow.ToString("o");
            var updatedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["from"] = decision.From,
                ["date"] = decision.Date,
                ["priority"] = decision.Priority,
                ["subject"] = decision.Subject,
                ["status"] = "resolved",
                ["resolvedAt"] = resolvedAt,
                ["resolvedBy"] = "human",
            };
            if (!string.IsNullOrEmpty(decision.Blocks)) updatedFields["blocks"] = decision.Blocks;

            var mainContent = FrontmatterParser.Stringify(updatedFields, decision.Body);
            var fullContent = mainContent + ResponseSeparator + response;

            var resolvedDir = paths.DecisionsResolvedDir(projectSlug);
            Directory.CreateDirectory(resolvedDir);
            var destPath = Path.Combine(resolvedDir, decision.Filename);
            File.WriteAllText(destPath, fullContent);

            // Remove from pending
            var pendingPath = Path.Combine(paths.DecisionsPendingDir(projectSlug), decision.Filename);
            if (File.Exists(pendingPath)) File.Delete(pendingPath);

            return null;
        }
        catch (Exception ex) { return ex.Message; }
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

            return new()
            {
                Filename = filename,
                Id = id,
                From = fields.GetValueOrDefault("from", string.Empty),
                Date = fields.GetValueOrDefault("date", string.Empty),
                Priority = fields.GetValueOrDefault("priority", "normal"),
                Subject = fields.GetValueOrDefault("subject", filename),
                Status = fields.GetValueOrDefault("status", "pending"),
                Blocks = fields.GetValueOrDefault("blocks", null),
                ResolvedAt = fields.GetValueOrDefault("resolvedAt", null),
                ResolvedBy = fields.GetValueOrDefault("resolvedBy", null),
                Body = body.Trim(),
                Response = response,
            };
        }
        catch { return null; }
    }
}
