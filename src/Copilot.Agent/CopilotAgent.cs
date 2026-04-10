using Copilot.Agent.Configuration;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Copilot.Agent;

internal sealed class CopilotAgent : IHostedService, IAsyncDisposable
{
    private readonly ITaskQueue _queue;
    private readonly ITaskDispatcher _dispatcher;
    private readonly ITaskCancellationRegistry _cancellationRegistry;
    private readonly ILogger<CopilotAgent> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly CopilotAgentOptions _options;
    private CancellationTokenSource? _shutdownTokenSource;
    private Task? _executeTask;

    private TimeSpan _retryInterval = TimeSpan.FromSeconds(5);
    private int _continuousError = 0;

    public CopilotAgent(
        ITaskQueue queue,
        ITaskDispatcher dispatcher,
        ITaskCancellationRegistry cancellationRegistry,
        IOptions<CopilotAgentOptions> options,
        ILogger<CopilotAgent> logger)
    {
        _queue = queue;
        _dispatcher = dispatcher;
        _cancellationRegistry = cancellationRegistry;
        _options = options.Value;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrentTasks);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _shutdownTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);

        _executeTask = ExecuteAsync(_shutdownTokenSource.Token);
        Log.AgentStarted(_logger, Environment.MachineName);
        return Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _queue.ReclaimExpiredAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await GetNextTaskAsync(stoppingToken);
                if (task is not null)
                {
                    _ = ExecuteCoreAsync(task, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.UnhandledAgentLoopError(_logger, ex);
            }
        }
    }

    private async Task ExecuteCoreAsync(CopilotTask task, CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            var taskToken = _cancellationRegistry.Register(task.Event.SessionKey);
            using var linked = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, taskToken);

            Log.TaskStarted(_logger, task.Id);
            await _dispatcher.DispatchAsync(task, linked.Token);
            await _queue.AcknowledgeAsync(task.Id, cancellationToken);
            Log.TaskCompleted(_logger, task.Id);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.TaskCancelledByUnassignment(_logger, task.Id);
            await _queue.AcknowledgeAsync(task.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Log.TaskCancelledDuringShutdown(_logger, task.Id);
        }
        catch (Exception ex)
        {
            Log.TaskFailed(_logger, task.Id, ex);
        }
        finally
        {
            _cancellationRegistry.Remove(task.Event.SessionKey);
            _concurrencyLimiter.Release();
        }
    }
    private async Task<CopilotTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var task = await _queue.ClaimNextAsync(cancellationToken);

                if (_continuousError > 0)
                {
                    Log.QueueConnectionRestored(_logger);
                    _continuousError = 0;
                    _retryInterval = TimeSpan.FromSeconds(5);
                }

                if (task is null)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(Random.Shared.Next(1, 3)),
                        cancellationToken);
                    continue;
                }

                return task;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _continuousError++;
                _retryInterval = TimeSpan.FromSeconds(
                    Math.Min(Math.Pow(2, _continuousError) * 1.5, 300));

                if (_continuousError == 1)
                {
                    Log.QueueConnectionError(_logger, _retryInterval.TotalSeconds, ex);
                }

                await Task.Delay(_retryInterval, cancellationToken);
            }
        }

        return null;
    }
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executeTask is null) return;
        Log.AgentStopping(_logger);

        await _shutdownTokenSource!.CancelAsync();
        try
        {
            await Task.WhenAny(_executeTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
        catch (OperationCanceledException)
        {
        }
        Log.AgentStopped(_logger);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdownTokenSource?.Dispose();

        for (var i = 0; i < _options.MaxConcurrentTasks; i++)
            await _concurrencyLimiter.WaitAsync();
        _concurrencyLimiter.Dispose();
        if (_executeTask is not null)
            await _executeTask.ConfigureAwait(false);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _agentStarted =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(1, nameof(AgentStarted)),
                "Copilot Agent started - NodeId: {NodeId}");

        private static readonly Action<ILogger, Exception?> _unhandledAgentLoopError =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(2, nameof(UnhandledAgentLoopError)),
                "Unhandled error in agent loop");

        private static readonly Action<ILogger, string, Exception?> _taskStarted =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(3, nameof(TaskStarted)),
                "Task {TaskId} started");

        private static readonly Action<ILogger, string, Exception?> _taskCompleted =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(4, nameof(TaskCompleted)),
                "Task {TaskId} completed");

        private static readonly Action<ILogger, string, Exception?> _taskCancelledByUnassignment =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(5, nameof(TaskCancelledByUnassignment)),
                "Task {TaskId} cancelled by unassignment");

        private static readonly Action<ILogger, string, Exception?> _taskCancelledDuringShutdown =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(6, nameof(TaskCancelledDuringShutdown)),
                "Task {TaskId} cancelled during shutdown");

        private static readonly Action<ILogger, string, Exception?> _taskFailed =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(7, nameof(TaskFailed)),
                "Task {TaskId} failed, will be reclaimed after TTL");

        private static readonly Action<ILogger, Exception?> _queueConnectionRestored =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(8, nameof(QueueConnectionRestored)),
                "Queue connection restored");

        private static readonly Action<ILogger, double, Exception?> _queueConnectionError =
            LoggerMessage.Define<double>(
                LogLevel.Error,
                new EventId(9, nameof(QueueConnectionError)),
                "Queue connection error, retrying every {Seconds}s");

        private static readonly Action<ILogger, Exception?> _agentStopping =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(10, nameof(AgentStopping)),
                "Copilot Agent stopping - waiting for active tasks...");

        private static readonly Action<ILogger, Exception?> _agentStopped =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(11, nameof(AgentStopped)),
                "Copilot Agent stopped");

        public static void AgentStarted(ILogger logger, string nodeId) =>
            _agentStarted(logger, nodeId, null);

        public static void UnhandledAgentLoopError(ILogger logger, Exception? exception) =>
            _unhandledAgentLoopError(logger, exception);

        public static void TaskStarted(ILogger logger, string taskId) =>
            _taskStarted(logger, taskId, null);

        public static void TaskCompleted(ILogger logger, string taskId) =>
            _taskCompleted(logger, taskId, null);

        public static void TaskCancelledByUnassignment(ILogger logger, string taskId) =>
            _taskCancelledByUnassignment(logger, taskId, null);

        public static void TaskCancelledDuringShutdown(ILogger logger, string taskId) =>
            _taskCancelledDuringShutdown(logger, taskId, null);

        public static void TaskFailed(ILogger logger, string taskId, Exception? exception) =>
            _taskFailed(logger, taskId, exception);

        public static void QueueConnectionRestored(ILogger logger) =>
            _queueConnectionRestored(logger, null);

        public static void QueueConnectionError(ILogger logger, double seconds, Exception? exception) =>
            _queueConnectionError(logger, seconds, exception);

        public static void AgentStopping(ILogger logger) =>
            _agentStopping(logger, null);

        public static void AgentStopped(ILogger logger) =>
            _agentStopped(logger, null);
    }
}