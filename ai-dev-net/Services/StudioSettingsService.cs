namespace AiDevNet.Services;

public class StudioSettingsService(WorkspacePaths paths)
{
    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["sonnet"] = "claude-sonnet-4-6",
        ["opus"] = "claude-opus-4-6",
        ["haiku"] = "claude-haiku-4-5-20251001",
    };

    public StudioSettings GetSettings()
    {
        var path = paths.StudioSettingsPath;
        if (!File.Exists(path))
            return new() { Models = new(Defaults) };

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<StudioSettings>(json, JsonDefaults.Write);
            if (data?.Models == null)
                return new() { Models = new(Defaults) };

            // Merge file values over defaults
            var merged = new Dictionary<string, string>(Defaults);
            foreach (var (k, v) in data.Models)
                merged[k] = v;
            return new() { Models = merged };
        }
        catch
        {
            return new() { Models = new(Defaults) };
        }
    }

    public void SaveSettings(StudioSettings settings)
    {
        var path = paths.StudioSettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonDefaults.Write));
    }
}
