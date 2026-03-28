using AiDevNet.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<WorkspacePaths>();
builder.Services.AddSingleton<WorkspaceService>();
builder.Services.AddSingleton<StudioSettingsService>();
builder.Services.AddSingleton<AgentTemplatesService>();
builder.Services.AddSingleton<AgentService>();
builder.Services.AddSingleton<BoardService>((sp) =>
    new BoardService(
        sp.GetRequiredService<WorkspacePaths>(),
        sp.GetRequiredService<AgentRunnerService>(),
        sp.GetRequiredService<ILogger<BoardService>>()));
builder.Services.AddSingleton<MessageChangedNotifier>();
builder.Services.AddSingleton<MessagesService>();
builder.Services.AddSingleton<DecisionsService>();
builder.Services.AddSingleton<JournalsService>();
builder.Services.AddSingleton<KbService>();
builder.Services.AddSingleton<DigestService>();
builder.Services.AddSingleton<GitService>();
builder.Services.AddHttpClient("ollama");
builder.Services.AddSingleton<IAgentExecutor, ClaudeAgentExecutor>();
builder.Services.AddSingleton<IAgentExecutor, OllamaAgentExecutor>();
builder.Services.AddSingleton<AgentRunnerService>();

builder.Services.AddHostedService<DispatcherService>();
builder.Services.AddHostedService<OverwatchService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
