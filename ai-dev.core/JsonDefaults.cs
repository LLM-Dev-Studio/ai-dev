namespace AiDev;

public static class JsonDefaults
{
    /// <summary>Deserialize camelCase JSON — no indentation required on input.</summary>
    public static readonly JsonSerializerOptions Read = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Serialize to indented camelCase JSON.</summary>
    public static readonly JsonSerializerOptions Write = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Serialize to single-line camelCase JSON.</summary>
    public static readonly JsonSerializerOptions WriteCompact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Serialize to indented camelCase JSON, omitting null values.</summary>
    public static readonly JsonSerializerOptions WriteIgnoreNull = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
