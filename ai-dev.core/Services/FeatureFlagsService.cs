namespace AiDev.Services;

public class FeatureFlagsService
{
    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, FilePathConstants.FeatureFlagsFileName);
    private AppFeatureFlags? _cache;

    public AppFeatureFlags GetFlags() => _cache ??= Load();

    public void SaveFlags(AppFeatureFlags flags)
    {
        ArgumentNullException.ThrowIfNull(flags);
        _cache = flags;
        var root = new Dictionary<string, object?> { ["FeatureFlags"] = flags };
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(root, JsonDefaults.WriteIgnoreNull));
    }

    private AppFeatureFlags Load()
    {
        if (!File.Exists(_filePath))
            return new AppFeatureFlags();

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_filePath));
            if (doc.RootElement.TryGetProperty("FeatureFlags", out var section))
                return JsonSerializer.Deserialize<AppFeatureFlags>(section, JsonDefaults.Read) ?? new AppFeatureFlags();
        }
        catch { }

        return new AppFeatureFlags();
    }
}
