namespace AiDev.Features.Agent;

/// <summary>
/// Composes agent templates by expanding named partial includes.
/// </summary>
public partial class TemplateComposer
{
    /// <summary>
    /// Replaces all partial include markers in the template with the provided partial content.
    /// </summary>
    /// <param name="template">The template text to compose.</param>
    /// <param name="partials">The partial content keyed by partial name.</param>
    /// <returns>The composed template text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the template references an unknown partial.</exception>
    public string Compose(string template, IReadOnlyDictionary<string, string> partials)
    {
        foreach (var (key, content) in partials)
            template = template.Replace($"{{{{> {key}}}}}", content);

        var missing = IncludePattern().Match(template);
        if (missing.Success)
            throw new InvalidOperationException(
                $"Template references unknown partial '{{{{> {missing.Groups[1].Value}}}}}'. " +
                $"Add '{missing.Groups[1].Value}' to the partials dictionary.");

        return template;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\{\{> ([^}]+)\}\}")]
    private static partial System.Text.RegularExpressions.Regex IncludePattern();
}
