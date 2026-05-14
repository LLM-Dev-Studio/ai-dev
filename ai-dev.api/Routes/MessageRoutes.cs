using AiDev.Models.Types;
using AiDev.Services;

namespace AiDev.Api.Routes;

public static class MessageRoutes
{
    public static IEndpointRouteBuilder MapMessageRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/messages", (string projectSlug, string? agentSlug, bool? processed, MessagesService messages) =>
        {
            ProjectSlug project = projectSlug;
            AgentSlug?  agent   = agentSlug != null ? (AgentSlug)agentSlug : null;

            var all = messages.ListMessages(project, agent);

            var result = processed.HasValue
                ? all.Where(m => m.IsProcessed == processed.Value).ToList()
                : all;

            return Results.Ok(result);
        });

        app.MapPost("/api/messages/{filename}/process", (string filename, string projectSlug, string agentSlug, MessagesService messages) =>
        {
            ProjectSlug project = projectSlug;
            AgentSlug   agent   = agentSlug;

            // Verify the message exists in the inbox (unprocessed) before archiving.
            var all = messages.ListMessages(project, agent);
            var exists = all.Any(m => m.Filename == filename && !m.IsProcessed);
            if (!exists)
                return Results.NotFound();

            messages.MarkProcessed(project, agent, filename);
            return Results.Ok();
        });

        return app;
    }
}
