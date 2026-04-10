using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddCopilotCore(builder.Configuration)
    .AddGitLabProvider(builder.Configuration)
    .AddCopilotAgent(builder.Configuration);

await builder.Build().RunAsync();