namespace AiDev.Features.Workspace;

/// <summary>
/// Represents the contents of a <c>.ai-dev/project.json</c> file.
/// The minimal fields (<see cref="ProjectSlug"/> and <see cref="ApiPort"/>) are required by the
/// VS Code extension for project discovery. The remaining fields are written by AI Dev Studio.
/// </summary>
public sealed record ProjectConfig(
    string ProjectSlug,
    int ApiPort,
    string? Name = null,
    string? Description = null,
    DateTime? CreatedAt = null);
