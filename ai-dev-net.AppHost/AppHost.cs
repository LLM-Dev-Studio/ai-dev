var builder = DistributedApplication.CreateBuilder(args);

var workspaceRoot = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory,
    "..",
    "..",
    "..",
    "..",
    "workspaces"));

builder.AddProject<Projects.ai_dev_mcp>("ai-dev-mcp")
    .WithEnvironment("WORKSPACE_ROOT", workspaceRoot);

builder.AddProject<Projects.ai_dev_ui_web>("ai-dev-net");

builder.Build().Run();
