using Copilot.Agent.Commands;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Copilot.Agent;

internal sealed class CopilotTaskDispatcher : ITaskDispatcher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CopilotTaskDispatcher> _logger;
    public CopilotTaskDispatcher(IServiceProvider services, ILogger<CopilotTaskDispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }
    public Task DispatchAsync(CopilotTask task, CancellationToken cancellationToken = default)
    {
        Log.TaskDispatching(_logger, task.Id, task.Event.Type);

        return task.Event.Type switch
        {
            CopilotEventType.IssueAssigned => HandleIssueAssignedAsync(task, cancellationToken),
            CopilotEventType.IssueComment => IssueCommentAsync(task, cancellationToken),
            CopilotEventType.PullRequestReview => HandlePullRequestReviewAsync(task, cancellationToken),
            CopilotEventType.PullRequestComment => HandlePullRequestCommentAsync(task, cancellationToken),
            _ => throw new NotSupportedException(
                    $"Unsupported event type: {task.Event.Type}")
        };
    }

    private Task HandleIssueAssignedAsync(CopilotTask task, CancellationToken cancellationToken)
    {
        var handler = _services.GetRequiredKeyedService<ICommandHandler>("assign");
        return handler.HandleAsync(task, cancellationToken);
    }
    private Task HandlePullRequestReviewAsync(CopilotTask task, CancellationToken cancellationToken)
    {
        var handler = _services.GetRequiredKeyedService<ICommandHandler>("review");
        return handler.HandleAsync(task, cancellationToken);
    }
    private Task IssueCommentAsync(CopilotTask task, CancellationToken cancellationToken)
    {
        var handler = _services.GetRequiredKeyedService<ICommandHandler>("issueComment");
        return handler.HandleAsync(task, cancellationToken);
    }
    private Task HandlePullRequestCommentAsync(CopilotTask task, CancellationToken cancellationToken)
    {
        var handler = _services.GetRequiredKeyedService<ICommandHandler>("pullRequestComment");
        return handler.HandleAsync(task, cancellationToken);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, CopilotEventType, Exception?> _taskDispatching =
            LoggerMessage.Define<string, CopilotEventType>(
                LogLevel.Debug,
                new EventId(1, nameof(TaskDispatching)),
                "Dispatching task {TaskId} EventType={EventType}");

        public static void TaskDispatching(ILogger logger, string taskId, CopilotEventType eventType) =>
            _taskDispatching(logger, taskId, eventType, null);
    }
}
