var builder = DistributedApplication.CreateBuilder(args);

// WORKSPACE_ROOT must be set in the developer's environment to the managed project directory
// (e.g. M:\ai-dev-net). The API and MCP will walk up from there to find .ai-dev/project.json.
var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT")
    ?? throw new InvalidOperationException(
        "WORKSPACE_ROOT environment variable is not set. " +
        "Set it to the managed project directory (e.g. M:\\ai-dev-net).");

builder.AddProject<Projects.ai_dev_mcp>("ai-dev-mcp")
    .WithEnvironment("WORKSPACE_ROOT", workspaceRoot);

builder.AddProject<Projects.ai_dev_api>("ai-dev-api")
    .WithEnvironment("WORKSPACE_ROOT", workspaceRoot);

builder.AddProject<Projects.ai_dev_ui_winui>("ai-dev-winui");

builder.Build().Run();
