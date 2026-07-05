using AiDev.Features.Decision;
using AiDev.Models;
using AiDev.Models.Types;

namespace AiDev.Api.Routes;

public static class DecisionRoutes
{
    private sealed record ResolveRequest(string Resolution);

    public static IEndpointRouteBuilder MapDecisionRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/decisions", (string projectSlug, string? status, IDecisionsService decisions) =>
        {
            ProjectSlug    project      = projectSlug;
            DecisionStatus statusFilter = status != null ? DecisionStatus.From(status) : DecisionStatus.Pending;

            var result = decisions.ListDecisions(project, statusFilter);
            return Results.Ok(result);
        });

        app.MapPost("/api/decisions/{id}/resolve", async (string id, string projectSlug, ResolveRequest body,
            IDecisionsService decisions, CancellationToken ct) =>
        {
            ProjectSlug project    = projectSlug;
            DecisionId  decisionId = id;

            var result = await decisions.ResolveDecisionAsync(project, decisionId, body.Resolution, ct);

            return result switch
            {
                Ok<Unit>  => Results.Ok(),
                Err<Unit> err when err.Error.Code == "DECISION_NOT_FOUND"        => Results.NotFound(),
                Err<Unit> err when err.Error.Code == "DECISION_ALREADY_RESOLVED" => Results.Conflict(),
                Err<Unit> err => Results.Problem(err.Error.Message),
                _ => Results.Problem("Unexpected result."),
            };
        });

        return app;
    }
}
