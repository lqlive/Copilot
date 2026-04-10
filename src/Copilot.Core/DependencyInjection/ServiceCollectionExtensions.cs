using Copilot.Core;
using Copilot.Core.Abstractions;
using Copilot.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCopilotCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddCopilotCoreOptions(configuration)
            .AddCopilotRedis()
            .AddCopilotQueue()
            .AddCopilotCancellation()
            .AddCopilotSessions()
            .AddCopilotWorkspace();

        return services;
    }

    public static IServiceCollection AddCopilotCoreOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RedisTaskQueueOptions>()
            .Bind(configuration.GetSection(RedisTaskQueueOptions.SectionName));

        services.AddOptions<WorkspaceOptions>()
            .Bind(configuration.GetSection(WorkspaceOptions.SectionName));

        services.AddOptions<GitClientOptions>()
            .Bind(configuration.GetSection(GitClientOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddCopilotRedis(this IServiceCollection services)
    {
        services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();

        return services;
    }

    public static IServiceCollection AddCopilotQueue(this IServiceCollection services)
    {
        services.AddSingleton<ITaskQueue, RedisTaskQueue>();
        return services;
    }

    public static IServiceCollection AddCopilotCancellation(this IServiceCollection services)
    {
        services.AddSingleton<RedisTaskCancellationRegistry>();
        services.AddSingleton<ITaskCancellationRegistry>(sp =>
            sp.GetRequiredService<RedisTaskCancellationRegistry>());

        services.AddHostedService(sp =>
            sp.GetRequiredService<RedisTaskCancellationRegistry>());

        return services;
    }

    public static IServiceCollection AddCopilotSessions(this IServiceCollection services)
    {
        services.AddHybridCache();
        services.AddSingleton<ISessionStore, HybridCacheSessionStore>();
        return services;
    }

    public static IServiceCollection AddCopilotWorkspace(this IServiceCollection services)
    {
        services.AddSingleton<IWorkspaceManager, DefaultWorkspaceManager>();
        return services;
    }
}