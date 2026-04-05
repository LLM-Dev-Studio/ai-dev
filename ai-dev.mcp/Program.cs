using AiDev.Mcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: ai-dev.mcp <workspace-root>");
    Console.Error.WriteLine("  workspace-root: absolute path to the project workspace directory");
    return 1;
}

var workspaceRoot = Path.GetFullPath(args[0]);
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
