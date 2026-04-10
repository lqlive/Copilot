using StackExchange.Redis;

namespace Copilot.Core.Abstractions;

public interface IRedisConnectionFactory
{
    Task<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken = default);
}
