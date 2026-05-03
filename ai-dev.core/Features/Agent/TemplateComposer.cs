namespace AiDev.Features.Agent;

public partial class TemplateComposer
{
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
