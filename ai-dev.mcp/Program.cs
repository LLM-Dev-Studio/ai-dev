using AiDev.Mcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

string? configuredWorkspaceRoot = args.Length switch
{
    1 => args[0],
    0 => Environment.GetEnvironmentVariable("WORKSPACE_ROOT"),
    _ => null,
};

if (string.IsNullOrWhiteSpace(configuredWorkspaceRoot))
{
    Console.Error.WriteLine("Usage: ai-dev.mcp <workspace-root>");
    Console.Error.WriteLine("  workspace-root: absolute path to the shared workspace directory");
    Console.Error.WriteLine("  Alternatively set WORKSPACE_ROOT to the shared workspace directory.");
    return 1;
}

var workspaceRoot = Path.GetFullPath(configuredWorkspaceRoot);
if (!Directory.Exists(workspaceRoot))
{
    Console.Error.WriteLine($"Workspace root does not exist: {workspaceRoot}");
    return 1;
}

var builder = Host.CreateApplicationBuilder();

// Suppress console logging — stdout is reserved for MCP JSON-RPC
builder.Logging.ClearProviders();
builder.Logging.AddDebug();

builder.Services.AddSingleton(new PathValidator(workspaceRoot));
builder.Services.AddSingleton(new AuditLog(workspaceRoot));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
return 0;
