namespace AiDev.Features.Git;

public partial class GitService
{
    // Only allow hex commit hashes (4–64 chars). Rejects any flag injection.
    private static readonly Regex ValidHashRegex =
        MyGitHashRegex();

    public bool IsGitRepo(string repoPath)
    {
        if (!Directory.Exists(repoPath)) return false;
        var result = Run(repoPath, "rev-parse", "--is-inside-work-tree");
        return result.ExitCode == 0 && result.Output.Trim() == "true";
    }

    public List<GitCommit> GetLog(string repoPath, int count = 50)
    {
        var sep = "\x1f";
        var result = Run(repoPath, "log", $"--format=%H{sep}%h{sep}%s{sep}%an{sep}%ae{sep}%aI", $"-{count}");
        if (result.ExitCode != 0) return [];

        var commits = new List<GitCommit>();
        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(sep);
            if (parts.Length < 6) continue;
            commits.Add(new GitCommit
            {
                Hash = parts[0],
                ShortHash = parts[1],
                Subject = parts[2],
                Author = parts[3],
                AuthorEmail = parts[4],
                Date = parts[5],
            });
        }
        return commits;
    }

    public GitCommitDetail? GetCommit(string repoPath, string hash)
    {
        if (!ValidHashRegex.IsMatch(hash)) return null;

        var sep = "\x1f";
        var logResult = Run(repoPath, "log", "-1", $"--format=%H{sep}%h{sep}%s{sep}%an{sep}%ae{sep}%aI{sep}%b", hash);
        if (logResult.ExitCode != 0) return null;

        var parts = logResult.Output.Trim().Split(sep, 7);
        if (parts.Length < 6) return null;

        var commit = new GitCommit
        {
            Hash = parts[0],
            ShortHash = parts[1],
            Subject = parts[2],
            Author = parts[3],
            AuthorEmail = parts[4],
            Date = parts[5],
        };

        var body = parts.Length > 6 ? parts[6].Trim() : string.Empty;

        var diffResult = Run(repoPath, "show", hash, "--stat", "--patch", "--no-color");
        var diff = diffResult.ExitCode == 0 ? diffResult.Output : string.Empty;

        return new GitCommitDetail { Commit = commit, Body = body, Diff = diff };
    }

    private static (int ExitCode, string Output) Run(string workingDir, params string[] args)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in args)
                proc.StartInfo.ArgumentList.Add(arg);

            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10_000);
            return (proc.ExitCode, output);
        }
        catch
        {
            return (-1, string.Empty);
        }
    }

    [GeneratedRegex(@"^[0-9a-f]{4,64}$", RegexOptions.Compiled)]
    private static partial Regex MyGitHashRegex();

}
