using Copilot.Core;
using Copilot.Core.Abstractions;
using Copilot.Core.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCopilotCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddCopilotCoreOptions(configuration)
            .AddCopilotSessions(configuration)
            .AddCopilotRedis()
            .AddCopilotQueue()
            .AddCopilotCancellation()
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

        services.AddOptions<SessionStoreOptions>()
            .Bind(configuration.GetSection(SessionStoreOptions.SectionName));

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

    public static IServiceCollection AddCopilotSessions(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SessionStoreOptions.SectionName);
        if (section.Exists())
        {
            var sessionOptions = section.Get<SessionStoreOptions>();
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = sessionOptions?.ConnectionString;
                options.InstanceName = sessionOptions?.InstanceName;
            });
        }

        services.AddHybridCache();
        services.AddSingleton<ISessionStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SessionStoreOptions>>().Value;

            return new HybridCacheSessionStore(
                sp.GetRequiredService<HybridCache>(),
                sp.GetRequiredService<ILogger<HybridCacheSessionStore>>(),
                options?.Expiration);
        });
        return services;
    }

    public static IServiceCollection AddCopilotWorkspace(this IServiceCollection services)
    {
        services.AddSingleton<IWorkspaceManager, DefaultWorkspaceManager>();
        return services;
    }
}