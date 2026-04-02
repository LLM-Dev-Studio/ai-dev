namespace AiDev.Ui.Web.Extensions;

public static class ProgramExtensions
{
    internal static IServiceCollection AddProgramDependencies(this IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        services.AddSingleton<WorkspacePaths>(sp =>
            new(sp.GetRequiredService<IWebHostEnvironment>().RootDir()));

        services.AddAiDevCore();

        // Opt ollama clients out of the global Polly resilience pipeline.
        // The inference client has a 10-minute timeout — retrying it would be catastrophic.
        // The health probe client should fail fast on each poll cycle, not retry.
        services.AddHttpClient("ollama");
        services.AddHttpClient("ollama-health");

        return services;
    }
}
