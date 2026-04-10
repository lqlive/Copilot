using System.IO;
using StackExchange.Redis;

namespace Copilot.Core.Configuration;

public sealed class RedisTaskQueueOptions
{
    public const string SectionName = "RedisTaskQueue";
    public string? ConnectionString { get; set; }
    public TimeSpan ClaimExpiry { get; set; } = TimeSpan.FromMinutes(5);

    public Func<TextWriter, Task<IConnectionMultiplexer>>? ConnectionFactory { get; set; }

    internal async Task<IConnectionMultiplexer> ConnectAsync(TextWriter log)
    {
        var factory = ConnectionFactory;
        if (factory is not null)
            return await factory(log).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("RedisTaskQueue:ConnectionString is required.");

        return await ConnectionMultiplexer.ConnectAsync(ConnectionString, log).ConfigureAwait(false);
    }
}