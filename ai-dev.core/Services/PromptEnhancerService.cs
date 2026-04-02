using System.Text;
using AiDev.Features.KnowledgeBase;
using AiDev.Features.Workspace;

namespace AiDev.Services;

public record EnhanceResult(string Title, string Description);

/// <summary>
/// Spawns the Claude CLI directly (not via IAgentExecutor) for a one-shot JSON generation call.
/// Uses stdin for the prompt to avoid Windows command-line length and newline-escaping issues.
/// No tools are granted and no agent CLAUDE.md is loaded — this is pure text generation.
/// </summary>
public partial class PromptEnhancerService(
    StudioSettingsService settings,
    WorkspaceService workspaceService,
    KbService kbService,
    WorkspacePaths paths,
    ILogger<PromptEnhancerService> logger)
{
    public async Task<EnhanceResult?> EnhanceAsync(
        ProjectSlug projectSlug, string title, string description, CancellationToken ct = default)
    {
        var modelAlias = "haiku";
        var modelId = settings.GetSettings().Models.GetValueOrDefault(modelAlias, "claude-haiku-4-5-20251001");
        var context = BuildContext(projectSlug);

        // Use the project directory as working dir — neutral ground with no agent CLAUDE.md,
        // and shallow enough that the repo-level Knowledge Extraction Rules are not picked up.
        var workingDir = paths.ProjectDir(projectSlug).Value;
        if (!Directory.Exists(workingDir))
            workingDir = paths.Root.Value;

        var prompt =
            $$"""
            Your job is to rewrite a task title and description to be more specific and actionable for an AI developer agent.

            IMPORTANT — follow these rules exactly:
            - Do NOT ask clarifying questions. Do NOT request more information.
            - Do NOT refuse. Always produce an enhanced version, even if the original is vague.
            - If details are missing, infer reasonable specifics from the project context and your knowledge.
            - Return ONLY a single JSON object — no prose, no markdown, no explanation:
              {"title": "...", "description": "..."}

            Output format rules:
            - title: imperative verb phrase, under 80 characters.
            - description: 2–4 sentences — what to do, where to do it, and how to verify it is done.

            Project context:
            {{context}}

            Task to enhance:
            Title: {{title}}
            Description: {{(string.IsNullOrWhiteSpace(description) ? "(none provided)" : description)}}
            """;

        var psi = BuildProcessStartInfo(workingDir, modelId);
        using var process = new Process { StartInfo = psi };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteAsync(prompt);
            process.StandardInput.Close();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                logger.LogWarning("[enhancer] Claude exited {Code}. stderr: {Err}", process.ExitCode, stderr);
                return null;
            }

            var raw = stdout.ToString();
            var jsonStart = raw.IndexOf('{');
            var jsonEnd   = raw.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                logger.LogWarning("[enhancer] No JSON object found in output: {Raw}", raw);
                return null;
            }

            var json = raw[jsonStart..(jsonEnd + 1)];
            using var doc = JsonDocument.Parse(json);
            var enhancedTitle = doc.RootElement.GetProperty("title").GetString() ?? title;
            var enhancedDesc  = doc.RootElement.GetProperty("description").GetString() ?? description;
            return new EnhanceResult(enhancedTitle, enhancedDesc);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[enhancer] Failed. stdout: {Out} stderr: {Err}", stdout, stderr);
            return null;
        }
    }

    private static ProcessStartInfo BuildProcessStartInfo(string workingDir, string modelId)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding  = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
            CreateNoWindow = true,
        };

        if (OperatingSystem.IsWindows())
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("claude");
        }
        else
        {
            psi.FileName = "claude";
        }

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(modelId);

        return psi;
    }

    private string BuildContext(ProjectSlug projectSlug)
    {
        var sb = new StringBuilder();

        var project = workspaceService.GetProject(projectSlug);
        if (project != null)
        {
            sb.AppendLine($"Project: {project.Name}");
            if (!string.IsNullOrWhiteSpace(project.Description))
                sb.AppendLine($"Description: {project.Description}");
            if (!string.IsNullOrWhiteSpace(project.CodebasePath))
                sb.AppendLine($"Codebase: {project.CodebasePath}");
        }

        var articles = kbService.ListArticles(projectSlug);
        if (articles.Count > 0)
        {
            sb.AppendLine("\nKnowledge Base articles:");
            foreach (var article in articles.Take(6))
            {
                var content = kbService.GetContent(projectSlug, article.Slug);
                var summary = content.Length > 400 ? content[..400].TrimEnd() + "…" : content.Trim();
                sb.AppendLine($"- [{article.Title}]: {summary}");
            }
        }

        return sb.ToString();
    }
}
