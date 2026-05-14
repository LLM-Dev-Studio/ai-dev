namespace AiDev.Features.Workspace;

/// <summary>
/// Represents the contents of a .ai-dev/project.json file committed to a project repository.
/// </summary>
public sealed record ProjectConfig(string ProjectSlug, int ApiPort);
