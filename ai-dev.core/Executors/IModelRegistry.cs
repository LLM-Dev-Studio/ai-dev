namespace AiDev.Executors;

/// <summary>
/// Provides a unified view of all models available across all registered executors.
/// Each executor contributes its <see cref="IAgentExecutor.KnownModels"/> (static)
/// and any models discovered via its most-recent health check (dynamic).
/// Dynamic discoveries override static entries with the same Id.
/// </summary>
public interface IModelRegistry
{
    /// <summary>
    /// Returns all models available for the specified executor,
    /// merging static KnownModels with any dynamically discovered models.
    /// </summary>
    IReadOnlyList<ModelDescriptor> GetModelsForExecutor(string executorName);

    /// <summary>
    /// Finds a specific model by executor name and model id.
    /// Returns null if not found.
    /// </summary>
    ModelDescriptor? Find(string executorName, string modelId);

    /// <summary>Returns every model across all executors.</summary>
    IReadOnlyList<ModelDescriptor> GetAll();
}
