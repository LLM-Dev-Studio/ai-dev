namespace AiDev.Services;

/// <summary>
/// Loads and persists application feature flags.
/// </summary>
public class FeatureFlagsService
{
    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, FilePathConstants.FeatureFlagsFileName);
    private AppFeatureFlags? _cache;

    /// <summary>
    /// Gets the current application feature flags.
    /// </summary>
    /// <returns>The loaded feature flags.</returns>
    public AppFeatureFlags GetFlags() => _cache ??= Load();

    /// <summary>
    /// Saves the provided application feature flags.
    /// </summary>
    /// <param name="flags">The feature flags to persist.</param>
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
