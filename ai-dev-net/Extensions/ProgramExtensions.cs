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

        return services;
    }
}
