using Copilot.Agent;
using Copilot.Agent.Commands;
using Copilot.Agent.Configuration;
using Copilot.Core.Abstractions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCopilotAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddCopilotAgentOptions(configuration)
            .AddCopilotClient()
            .AddCopilotDispatching()
            .AddCopilotCommands()
            .AddCopilotWorker();
    }

    public static IServiceCollection AddCopilotAgentOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CopilotAgentOptions>()
            .Bind(configuration.GetSection(CopilotAgentOptions.SectionName));

        services.AddOptions<CopilotOptions>()
            .Bind(configuration.GetSection(CopilotOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddCopilotClient(
        this IServiceCollection services)
    {
        services.AddSingleton<CopilotClient>();
        return services;
    }

    public static IServiceCollection AddCopilotDispatching(
        this IServiceCollection services)
    {
        services.AddSingleton<ITaskDispatcher, CopilotTaskDispatcher>();
        return services;
    }

    public static IServiceCollection AddCopilotCommands(
        this IServiceCollection services)
    {
        services.AddKeyedSingleton<ICommandHandler, ReviewCommandHandler>("review");
        services.AddKeyedSingleton<ICommandHandler, AssignCommandHandler>("assign");
        services.AddKeyedSingleton<ICommandHandler, IssueCommentCommandHandler>("issueComment");
        services.AddKeyedSingleton<ICommandHandler, PullRequestCommentCommandHandler>("pullRequestComment");
        return services;
    }

    public static IServiceCollection AddCopilotWorker(
        this IServiceCollection services)
    {
        services.AddHostedService<CopilotAgent>();
        return services;
    }
}