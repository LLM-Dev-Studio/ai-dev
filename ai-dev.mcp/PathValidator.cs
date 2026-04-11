namespace AiDev.Mcp;

/// <summary>
/// Validates that all file-system paths resolve within the workspace root.
/// Prevents directory traversal attacks from agent-supplied path arguments.
/// </summary>
public sealed class PathValidator(string workspaceRoot)
{
    private readonly string _root = EnsureTrailingSeparator(Path.GetFullPath(workspaceRoot));

    public string WorkspaceRoot => _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Returns the resolved absolute path if it falls within the workspace root.
    /// Throws <see cref="InvalidOperationException"/> on traversal attempts.
    /// </summary>
    public string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(WorkspaceRoot, relativePath));
        ValidateWithinRoot(_root, full, relativePath);
        return full;
    }

    /// <summary>
    /// Returns the absolute root directory for a specific project slug within the workspace.
    /// </summary>
    public string ResolveProjectRoot(string projectSlug)
    {
        ValidateSlug(projectSlug, "projectSlug");
        return Resolve(projectSlug);
    }

    /// <summary>
    /// Returns the resolved absolute path for a path within a specific project root.
    /// Throws <see cref="InvalidOperationException"/> on traversal attempts.
    /// </summary>
    public string ResolveProject(string projectSlug, string relativePath)
    {
        var projectRoot = EnsureTrailingSeparator(ResolveProjectRoot(projectSlug));
        var full = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        ValidateWithinRoot(projectRoot, full, relativePath);
        return full;
    }

    /// <summary>
    /// Validates that an already-absolute path falls within the specified project root.
    /// </summary>
    public string ValidateProjectAbsolute(string projectSlug, string absolutePath)
    {
        var projectRoot = EnsureTrailingSeparator(ResolveProjectRoot(projectSlug));
        var full = Path.GetFullPath(absolutePath);
        ValidateWithinRoot(projectRoot, full, absolutePath);
        return full;
    }

    /// <summary>
    /// Returns the resolved absolute path, or null if it escapes the workspace.
    /// </summary>
    public string? TryResolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(WorkspaceRoot, relativePath));
        return full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    /// <summary>
    /// Validates that an already-absolute path falls within the workspace root.
    /// </summary>
    public string ValidateAbsolute(string absolutePath)
    {
        var full = Path.GetFullPath(absolutePath);
        ValidateWithinRoot(_root, full, absolutePath);
        return full;
    }

    /// <summary>
    /// Validates an agent slug contains only safe characters (lowercase letters, digits, hyphens).
    /// </summary>
    public static void ValidateSlug(string slug, string label)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException($"{label} is required.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$"))
            throw new InvalidOperationException($"Invalid {label}: '{slug}'. Use lowercase letters, digits, and hyphens.");
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static void ValidateWithinRoot(string root, string fullPath, string originalPath)
    {
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path escapes workspace root: {originalPath}");
    }
}
