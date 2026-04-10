using System.Text.Json;
using Copilot.Core.Abstractions;
using Copilot.Core.Configuration;
using Copilot.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Copilot.Core;

public class RedisTaskQueue : ITaskQueue
{
    private const string StreamKey = "copilot:tasks";
    private const string GroupName = "copilot-workers";
    private const string CancelledSetKey = "copilot:task:cancelled";
    private const string PayloadField = "payload";
    private const string EnqueuedAtField = "enqueuedAt";
    private const string RetryCountField = "retryCount";

    private readonly IRedisConnectionFactory _connectionFactory;
    private readonly RedisTaskQueueOptions _options;
    private readonly ILogger<RedisTaskQueue> _logger;
    private readonly SemaphoreSlim _ensureConnectionLock  = new(1, 1);
    private readonly string _nodeId = $"{Environment.MachineName}_{Guid.NewGuid():N}";
    private IDatabase? _database;

    public RedisTaskQueue(
        IRedisConnectionFactory connectionFactory,
        Microsoft.Extensions.Options.IOptions<RedisTaskQueueOptions> options,
        ILogger<RedisTaskQueue> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }
    public async Task AcknowledgeAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var database = await EnsureConnectedAsync(cancellationToken);
        await database.StreamAcknowledgeAsync(StreamKey, GroupName, taskId);
        Log.TaskAcknowledged(_logger, taskId);
    }

    public async Task<CopilotTask?> ClaimNextAsync(CancellationToken cancellationToken = default)
    {
        var database = await EnsureConnectedAsync(cancellationToken);

        var entries = await database.StreamReadGroupAsync(
            StreamKey,
            GroupName,
            _nodeId,
            count: 1,
            noAck: false);

        if (entries.Length == 0)
            return null;

        var task = ParseEntry(entries[0]);
        if (task is null)
            return null;

        if (await TrySkipCancelledAsync(database, task))
            return null;

        return task;
    }

    public async Task ProduceAsync(CopilotEvent @event, CancellationToken cancellationToken = default)
    {
        var database = await EnsureConnectedAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(@event);
        await database.StreamAddAsync(StreamKey,
        [
            new NameValueEntry(PayloadField,    payload),
            new NameValueEntry(EnqueuedAtField, DateTimeOffset.UtcNow.ToString("O")),
            new NameValueEntry(RetryCountField, "0"),
        ]);
        Log.TaskProduced(_logger, @event.Platform, @event.RepositoryId);
    }

    public async Task ReclaimExpiredAsync(CancellationToken cancellationToken = default)
    {
        var database = await EnsureConnectedAsync(cancellationToken);
        var result = await database.StreamAutoClaimAsync(
            StreamKey, GroupName, _nodeId,
            minIdleTimeInMs: (long)_options.ClaimExpiry.TotalMilliseconds,
            startAtId: "0-0",
            count: 100);
        if (result.ClaimedEntries.Length > 0)
        {
            Log.ExpiredTasksReclaimed(_logger, result.ClaimedEntries.Length);
        }
    }
    private async Task<bool> TrySkipCancelledAsync(IDatabase database, CopilotTask task)
    {
        var key = task.Event.SessionKey.ToString();
        if (!await database.SetContainsAsync(CancelledSetKey, key))
            return false;

        await Task.WhenAll(
            database.StreamAcknowledgeAsync(StreamKey, GroupName, task.Id),
            database.SetRemoveAsync(CancelledSetKey, key));
        Log.CancelledTaskSkipped(_logger, task.Id);
        return true;
    }

    private async Task<IDatabase> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_database is not null)
            return _database;

        await _ensureConnectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_database is not null)
                return _database;

            var connection = await _connectionFactory.GetAsync(cancellationToken);
            var database = connection.GetDatabase();

            try
            {
                await database.StreamCreateConsumerGroupAsync(
                    StreamKey, GroupName,
                    StreamPosition.NewMessages,
                    createStream: true);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
            }

            _database = database;
            return _database;
        }
        finally
        {
            _ensureConnectionLock.Release();
        }
    }
    private static CopilotTask? ParseEntry(StreamEntry entry)
    {
        var payload = entry[PayloadField];
        if (payload.IsNullOrEmpty) return null;
        var @event = JsonSerializer.Deserialize<CopilotEvent>(payload.ToString()!);
        if (@event is null) return null;
        _ = int.TryParse(entry[RetryCountField].ToString(), out var retryCount);
        _ = DateTimeOffset.TryParse(entry[EnqueuedAtField], out var enqueuedAt);
        return new CopilotTask
        {
            Id = entry.Id!,
            Event = @event,
            EnqueuedAt = enqueuedAt,
            RetryCount = retryCount,
        };
    }
    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _taskAcknowledged =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(1, nameof(TaskAcknowledged)),
                "Task {TaskId} acknowledged");

        private static readonly Action<ILogger, string, string, Exception?> _taskProduced =
            LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(2, nameof(TaskProduced)),
                "Task enqueued for {Platform}/{RepositoryId}");

        private static readonly Action<ILogger, int, Exception?> _expiredTasksReclaimed =
            LoggerMessage.Define<int>(
                LogLevel.Warning,
                new EventId(3, nameof(ExpiredTasksReclaimed)),
                "Reclaimed {Count} expired tasks from previous nodes");

        private static readonly Action<ILogger, string, Exception?> _cancelledTaskSkipped =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(4, nameof(CancelledTaskSkipped)),
                "Skipped cancelled task {TaskId}");

        public static void TaskAcknowledged(ILogger logger, string taskId) =>
            _taskAcknowledged(logger, taskId, null);

        public static void TaskProduced(ILogger logger, string platform, string repositoryId) =>
            _taskProduced(logger, platform, repositoryId, null);

        public static void ExpiredTasksReclaimed(ILogger logger, int count) =>
            _expiredTasksReclaimed(logger, count, null);

        public static void CancelledTaskSkipped(ILogger logger, string taskId) =>
            _cancelledTaskSkipped(logger, taskId, null);
    }
}
