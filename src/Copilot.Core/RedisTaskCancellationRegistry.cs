using StackExchange.Redis;
using Copilot.Core.Models;
using Copilot.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Copilot.Core;

public class RedisTaskCancellationRegistry : ITaskCancellationRegistry, IHostedService, IAsyncDisposable
{
    private const string Channel = "copilot:task:cancel";
    private const string CancelledSet = "copilot:task:cancelled";
    private static readonly TimeSpan CancelledSetTtl = TimeSpan.FromHours(1);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _active = new();
    private readonly IRedisConnectionFactory _connectionFactory;
    private readonly ILogger<RedisTaskCancellationRegistry> _logger;
    private ISubscriber? _subscriber;
    public RedisTaskCancellationRegistry(
        IRedisConnectionFactory connectionFactory,
        ILogger<RedisTaskCancellationRegistry> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }
    public async Task StartAsync(CancellationToken ct)
    {
        var connection = await _connectionFactory.GetAsync(ct);
        _subscriber = connection.GetSubscriber();
        await _subscriber.SubscribeAsync(
            RedisChannel.Literal(Channel),
            (_, message) => TryCancelLocal(message!));
    }
    public async Task StopAsync(CancellationToken ct)
    {
        if (_subscriber is not null)
            await _subscriber.UnsubscribeAsync(RedisChannel.Literal(Channel));
    }
    public CancellationToken Register(SessionKey key)
    {
        var cts = new CancellationTokenSource();
        _active[key.ToString()] = cts;
        return cts.Token;
    }
    public async Task CancelAsync(SessionKey key, CancellationToken ct = default)
    {
        var keyValue = key.ToString();
        var connection = await _connectionFactory.GetAsync(ct);
        var db = connection.GetDatabase();
        await db.SetAddAsync(CancelledSet, keyValue);
        await db.KeyExpireAsync(CancelledSet, CancelledSetTtl);
        await connection.GetSubscriber()
            .PublishAsync(RedisChannel.Literal(Channel), keyValue);
        Log.TaskCancelled(_logger, keyValue);
    }
    public void Remove(SessionKey key)
    {
        if (_active.TryRemove(key.ToString(), out var cts))
            cts.Dispose();
    }
    public ValueTask DisposeAsync()
    {
        foreach (var cts in _active.Values)
            cts.Dispose();
        _active.Clear();
        return ValueTask.CompletedTask;
    }
    private void TryCancelLocal(string key)
    {
        if (!_active.TryRemove(key, out var cts)) return;
        cts.Cancel();
        cts.Dispose();
        Log.LocalTaskCancelled(_logger, key);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _taskCancelled =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(1, nameof(TaskCancelled)),
                "Task cancelled for {Key}");

        private static readonly Action<ILogger, string, Exception?> _localTaskCancelled =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(2, nameof(LocalTaskCancelled)),
                "Local task cancelled for {Key}");

        public static void TaskCancelled(ILogger logger, string key) =>
            _taskCancelled(logger, key, null);

        public static void LocalTaskCancelled(ILogger logger, string key) =>
            _localTaskCancelled(logger, key, null);
    }
}