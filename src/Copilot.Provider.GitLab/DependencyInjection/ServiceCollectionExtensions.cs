using GitLabApiClient;
using Copilot.Core.Abstractions;
using Copilot.Parser;
using Copilot.Provider.GitLab;
using Copilot.Provider.GitLab.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGitLabProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddGitLabOptions(configuration)
            .AddGitLabClient()
            .AddGitLabWebhookParsing()
            .AddGitLabProviderClient();
    }

    public static IServiceCollection AddGitLabOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GitLabClientOptions>()
            .Bind(configuration.GetSection("GitLab"));

        return services;
    }

    public static IServiceCollection AddGitLabClient(
        this IServiceCollection services)
    {
        services.TryAddTransient(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GitLabClientOptions>>().Value;
            var client = new GitLabClient(options.BaseUrl, options.AccessToken);
            return client;
        });

        return services;
    }

    public static IServiceCollection AddGitLabWebhookParsing(
        this IServiceCollection services)
    {
        services.AddScoped<IWebhookParser, GitLabWebhookParser>();
        return services;
    }

    public static IServiceCollection AddGitLabProviderClient(
        this IServiceCollection services)
    {
        services.AddSingleton<IGitProviderClient, GitLabProviderClient>();
        return services;
    }
}