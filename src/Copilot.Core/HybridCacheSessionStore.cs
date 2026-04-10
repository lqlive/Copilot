using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Copilot.Core;
internal sealed class HybridCacheSessionStore : ISessionStore
{
    private static readonly TimeSpan DefaultSessionTimeout = TimeSpan.FromHours(24);
    private readonly HybridCache _cache;
    private readonly ILogger<HybridCacheSessionStore> _logger;
    private readonly HybridCacheEntryOptions _cacheEntryOptions;

    public HybridCacheSessionStore(
        HybridCache cache,
        ILogger<HybridCacheSessionStore> logger,
        TimeSpan? sessionTimeout = null)
    {
        _cache = cache;
        _logger = logger;
        var resolvedTimeout = sessionTimeout ?? DefaultSessionTimeout;
        _cacheEntryOptions = new()
        {
            Expiration = resolvedTimeout,
            LocalCacheExpiration = TimeSpan.FromMinutes(
                Math.Min(resolvedTimeout.TotalMinutes / 2, 5)),
        };
    }

    public async Task<ConversationSession?> GetAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var cacheKey = key.ToString();

        try
        {
            var session = await _cache.GetOrCreateAsync(
                cacheKey,
                _ => ValueTask.FromResult<ConversationSession?>(null),
                options: _cacheEntryOptions,
                cancellationToken: cancellationToken);

            if (session is not null)
            {
                await _cache.SetAsync(cacheKey, session, _cacheEntryOptions, cancellationToken: cancellationToken);
                Log.SessionRetrieved(_logger, cacheKey);
            }
            return session;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.GetSessionFailed(_logger, cacheKey, ex);
            throw;
        }
    }

    public async Task RemoveAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var cacheKey = key.ToString();

        try
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            Log.SessionDeleted(_logger, cacheKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.DeleteSessionFailed(_logger, cacheKey, ex);
            // Don't rethrow - session removal is a best-effort cleanup operation
            // The session will expire naturally if removal fails
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _sessionRetrieved =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(1, nameof(SessionRetrieved)),
                "Session retrieved for {Key}");

        private static readonly Action<ILogger, string, Exception?> _getSessionFailed =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(2, nameof(GetSessionFailed)),
                "Failed to get session for {Key}");

        private static readonly Action<ILogger, string, Exception?> _sessionDeleted =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(3, nameof(SessionDeleted)),
                "Session deleted for {Key}");

        private static readonly Action<ILogger, string, Exception?> _deleteSessionFailed =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(4, nameof(DeleteSessionFailed)),
                "Failed to delete session for {Key}");

        public static void SessionRetrieved(ILogger logger, string key) =>
            _sessionRetrieved(logger, key, null);

        public static void GetSessionFailed(ILogger logger, string key, Exception? exception) =>
            _getSessionFailed(logger, key, exception);

        public static void SessionDeleted(ILogger logger, string key) =>
            _sessionDeleted(logger, key, null);

        public static void DeleteSessionFailed(ILogger logger, string key, Exception? exception) =>
            _deleteSessionFailed(logger, key, exception);
    }
}