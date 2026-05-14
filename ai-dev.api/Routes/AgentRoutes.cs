using AiDev.Features.Agent;
using AiDev.Models.Types;

namespace AiDev.Api.Routes;

public static class AgentRoutes
{
    public static IEndpointRouteBuilder MapAgentRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/agents", (string projectSlug, IAgentRunnerService runner, AgentService agentService) =>
        {
            ProjectSlug project = projectSlug;
            var agents = agentService.ListAgents(project);
            var result = agents.Select(a => new
            {
                slug          = a.Slug,
                isRunning     = runner.IsRunning(project, a.Slug),
                isRateLimited = runner.IsRateLimited(project, a.Slug),
            });
            return Results.Ok(result);
        });

        app.MapPost("/api/agents/{slug}/run", (string slug, string projectSlug, IAgentRunnerService runner) =>
        {
            ProjectSlug project   = projectSlug;
            AgentSlug   agentSlug = slug;
            var launched = runner.LaunchAgent(project, agentSlug);
            return launched ? Results.Ok() : Results.Conflict();
        });

        app.MapPost("/api/agents/{slug}/stop", (string slug, string projectSlug, IAgentRunnerService runner) =>
        {
            ProjectSlug project   = projectSlug;
            AgentSlug   agentSlug = slug;
            var stopped = runner.StopAgent(project, agentSlug);
            return stopped ? Results.Ok() : Results.Conflict();
        });

        return app;
    }
}
