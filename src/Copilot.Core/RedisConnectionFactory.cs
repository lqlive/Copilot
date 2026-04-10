using System.Text;
using Copilot.Core.Abstractions;
using Copilot.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Copilot.Core;

internal sealed class RedisConnectionFactory : IRedisConnectionFactory, IAsyncDisposable
{
    private readonly RedisTaskQueueOptions _options;
    private readonly ILogger<RedisConnectionFactory> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnectionMultiplexer? _connection;
    private bool _disposed;

    public RedisConnectionFactory(
        IOptions<RedisTaskQueueOptions> options,
        ILogger<RedisConnectionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is not null)
            return _connection;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null)
                return _connection;

            using var writer = new LoggerTextWriter(_logger);
            _connection = await _options.ConnectAsync(writer).ConfigureAwait(false);
            Log.RedisConnected(_logger);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionLock.Dispose();
    }

    private sealed class LoggerTextWriter : TextWriter
    {
        private readonly ILogger _logger;

        public LoggerTextWriter(ILogger logger)
        {
            _logger = logger;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
        }

        public override void WriteLine(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Log.ConnectionMultiplexerMessage(_logger, value);
            }
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, Exception?> _redisConnected =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(1, nameof(RedisConnected)),
                "Redis connection established.");

        private static readonly Action<ILogger, string, Exception?> _connectionMultiplexerMessage =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(2, nameof(ConnectionMultiplexerMessage)),
                "Redis: {Message}");

        public static void RedisConnected(ILogger logger) =>
            _redisConnected(logger, null);

        public static void ConnectionMultiplexerMessage(ILogger logger, string message) =>
            _connectionMultiplexerMessage(logger, message, null);
    }
}
