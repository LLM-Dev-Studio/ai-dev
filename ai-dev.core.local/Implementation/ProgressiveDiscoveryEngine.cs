using AiDev.Core.Local.Contracts;
using System.Text;

namespace AiDev.Core.Local.Implementation;

internal sealed class ProgressiveDiscoveryEngine : IProgressiveDiscoveryEngine
{
    private static readonly string[] CodeExtensions =
        [".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".go", ".rs", ".java", ".kt", ".md"];

    private const int ContextLines = 10;
    private const decimal HighConfidenceRatio = 0.5m;

    private readonly string _root;

    public ProgressiveDiscoveryEngine(WorkspacePaths paths) : this(paths.Root.Value) { }

    internal ProgressiveDiscoveryEngine(string root) => _root = root;

    public async Task<Result<DiscoveryBatch>> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken ct = default)
    {
        // Phase 1 — candidate discovery
        var candidates = await DiscoverCandidatesAsync(request, ct);

        if (candidates.Count == 0)
            return new Ok<DiscoveryBatch>(new DiscoveryBatch(
                [],
                $"No files found containing '{request.Query}'.",
                0m,
                "Try a broader query or add CandidatePaths."));

        // Phase 2 + 3 — targeted slice reads and evidence synthesis
        var terms = ExtractTerms(request.Query);
        var slices = await BuildSlicesAsync(request, candidates, terms, ct);

        // Phase 4 — confidence and recommendation
        var confidence = CalculateConfidence(slices.Count, candidates.Count);
        var synthesis = BuildSynthesis(request.Query, slices);
        var recommendation = BuildRecommendation(confidence, slices, candidates.Count);

        return new Ok<DiscoveryBatch>(new DiscoveryBatch(slices, synthesis, confidence, recommendation));
    }

    // Phase 1: locate candidate files via explicit paths, then fall back to extension-filtered grep.
    private async Task<List<string>> DiscoverCandidatesAsync(DiscoveryRequest request, CancellationToken ct)
    {
        if (request.CandidatePaths.Count > 0)
        {
            var explicitPaths = request.CandidatePaths
                .Select(p => Path.IsPathRooted(p) ? p : Path.Combine(_root, p))
                .Where(File.Exists)
                .ToList();
            if (explicitPaths.Count > 0) return explicitPaths;
        }

        if (!Directory.Exists(_root)) return [];

        var terms = ExtractTerms(request.Query);
        var files = Directory
            .EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Where(f => !IsIgnored(f));

        if (request.RestrictToCodeFiles)
            files = files.Where(f => CodeExtensions.Contains(
                Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

        var matching = new List<string>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            if (await FileContainsAnyTermAsync(file, terms, ct))
                matching.Add(file);
        }

        return matching;
    }

    // Phase 2 + 3: read a slice around the first match in each candidate.
    private static async Task<IReadOnlyList<DiscoverySlice>> BuildSlicesAsync(
        DiscoveryRequest request,
        List<string> candidates,
        string[] terms,
        CancellationToken ct)
    {
        var slices = new List<DiscoverySlice>();

        foreach (var file in candidates)
        {
            if (slices.Count >= request.MaxSlices) break;
            ct.ThrowIfCancellationRequested();

            var slice = await TryBuildSliceAsync(file, terms, ct);
            if (slice is not null) slices.Add(slice);
        }

        return slices;
    }

    private static async Task<DiscoverySlice?> TryBuildSliceAsync(
        string filePath,
        string[] terms,
        CancellationToken ct)
    {
        string[] lines;
        try { lines = await File.ReadAllLinesAsync(filePath, ct); }
        catch { return null; }

        var matchLine = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (terms.Any(t => lines[i].Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                matchLine = i;
                break;
            }
        }

        if (matchLine < 0) return null;

        var start = Math.Max(0, matchLine - ContextLines);
        var end = Math.Min(lines.Length - 1, matchLine + ContextLines);

        var evidence = new List<string>();
        for (var i = start; i <= end; i++)
        {
            if (terms.Any(t => lines[i].Contains(t, StringComparison.OrdinalIgnoreCase)))
                evidence.Add($"{filePath}:{i + 1}");
        }

        return new DiscoverySlice(
            Path: filePath,
            StartLine: start + 1,
            EndLine: end + 1,
            Summary: $"Match for '{terms[0]}' near line {matchLine + 1} in {Path.GetFileName(filePath)}",
            Evidence: evidence);
    }

    private static decimal CalculateConfidence(int sliceCount, int candidateCount)
    {
        if (sliceCount == 0) return 0m;
        var ratio = (decimal)sliceCount / candidateCount;
        return ratio >= HighConfidenceRatio ? 0.8m : 0.5m;
    }

    private static string BuildSynthesis(string query, IReadOnlyList<DiscoverySlice> slices)
    {
        if (slices.Count == 0) return $"No evidence found for '{query}'.";
        var sb = new StringBuilder();
        sb.Append($"Found {slices.Count} slice(s) for '{query}'.");
        foreach (var s in slices)
            sb.Append($" {s.Summary}.");
        return sb.ToString();
    }

    private static string BuildRecommendation(
        decimal confidence,
        IReadOnlyList<DiscoverySlice> slices,
        int candidateCount)
    {
        if (slices.Count == 0)
            return "Widen the query or provide explicit CandidatePaths.";
        if (confidence >= 0.8m)
            return $"Evidence is strong across {slices.Count}/{candidateCount} candidates. Proceed with planning.";
        return $"Evidence found in {slices.Count}/{candidateCount} candidates. Consider reading more slices.";
    }

    private static string[] ExtractTerms(string query)
        => query.Split([' ', '.', ':', '/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Take(5)
            .ToArray();

    private static async Task<bool> FileContainsAnyTermAsync(
        string filePath,
        string[] terms,
        CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (terms.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static bool IsIgnored(string path)
    {
        var lower = path.Replace('\\', '/');
        return lower.Contains("/obj/") || lower.Contains("/bin/") || lower.Contains("/.git/")
            || lower.Contains("/node_modules/") || lower.Contains("/.vs/");
    }
}
