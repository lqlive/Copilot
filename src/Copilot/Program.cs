using Copilot.Apis;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCopilotCore(builder.Configuration)
    .AddGitLabProvider(builder.Configuration);

var app = builder.Build();

app.MapGroup("/")
    .WithTags("Webhooks API")
    .MapWebhooks();

app.Run();