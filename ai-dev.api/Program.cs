using AiDev;
using AiDev.Api.Hubs;
using AiDev.Api.Routes;
using AiDev.Extensions;
using AiDev.Features.Workspace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// WORKSPACE_ROOT is the folder opened in VS Code — the extension passes it when spawning
// this process. .ai-dev is always a direct child of that folder.
var workspaceRoot = builder.Configuration["WORKSPACE_ROOT"]
    ?? throw new InvalidOperationException(
        "WORKSPACE_ROOT is not set. The VS Code extension sets this when spawning the backend, " +
        "or add it to launchSettings.json for direct development runs.");

builder.Services.AddAiDevCore();

// Pre-activate the holder for this codebase. AddAiDevCore registers the type-based singleton,
// so we override it with a pre-activated instance to ensure WorkspacePaths resolves correctly.
var holder = new ActiveWorkspaceHolder();
holder.Activate(workspaceRoot);
builder.Services.AddSingleton(holder);
builder.Services.AddSingleton(holder.Paths.Root);
builder.Services.AddSignalR();
builder.Services.AddHostedService<ProjectStateRelayService>();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
                  origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
                  origin.StartsWith("vscode-webview://", StringComparison.OrdinalIgnoreCase))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();

app.MapAgentRoutes();
app.MapMessageRoutes();
app.MapDecisionRoutes();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
app.MapHub<ProjectStateHub>("/hubs/project");

app.Run();

// Exposes Program for WebApplicationFactory in integration tests.
public partial class Program { }
