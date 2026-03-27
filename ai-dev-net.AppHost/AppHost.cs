var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ai_dev_net>("ai-dev-net");

builder.Build().Run();
