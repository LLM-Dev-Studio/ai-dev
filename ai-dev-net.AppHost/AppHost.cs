var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ai_dev_ui_web>("ai-dev-net");

builder.Build().Run();
