namespace AiDev.Services;

public static class FrontmatterParser
{
    public static (Dictionary<string, string> Fields, string Body) Parse(string content)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");

        if (!content.StartsWith("---\n"))
            return (fields, content);

        var rest = content[4..]; // skip "---\n"
        var closeIdx = rest.IndexOf("\n---\n", StringComparison.Ordinal);
        string frontmatterText;
        string body;

        if (closeIdx >= 0)
        {
            frontmatterText = rest[..closeIdx];
            body = rest[(closeIdx + 5)..].TrimStart('\n');
        }
        else
        {
            // Check for ---\n at end of file
            var endClose = "\n---";
            var endIdx = rest.LastIndexOf(endClose, StringComparison.Ordinal);
            if (endIdx >= 0 && endIdx + endClose.Length >= rest.Length - 1)
            {
                frontmatterText = rest[..endIdx];
                body = string.Empty;
            }
            else
            {
                return (fields, content);
            }
        }

        foreach (var line in frontmatterText.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (!string.IsNullOrEmpty(key))
                fields[key] = value;
        }

        return (fields, body);
    }

    public static string Stringify(Dictionary<string, string> fields, string body)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        foreach (var (key, value) in fields)
            sb.AppendLine($"{key}: {value}");
        sb.AppendLine("---");
        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }
        return sb.ToString();
    }
}
