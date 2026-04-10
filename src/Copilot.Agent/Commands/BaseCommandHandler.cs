using System.Text;
using Copilot.Agent.Configuration;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Copilot.Agent.Commands;

internal abstract class BaseCommandHandler : ICommandHandler
{
    protected readonly ISessionStore SessionStore;
    protected readonly CopilotClient CopilotClient;
    protected readonly IGitProviderClient GitProviderClient;
    protected readonly ILogger Logger;
    protected readonly CopilotOptions Options;
    protected BaseCommandHandler(
        ISessionStore sessionStore,
        CopilotClient copilotClient,
        IGitProviderClient gitProviderClient,
        IOptions<CopilotOptions> options,
        ILogger logger)
    {
        SessionStore = sessionStore;
        CopilotClient = copilotClient;
        GitProviderClient = gitProviderClient;
        Options = options.Value;
        Logger = logger;
    }

    protected async Task<ConversationSession> GetOrCreateSessionAsync(
      CopilotTask task, CancellationToken ct)
    {
        return await SessionStore.GetAsync(task.Event.SessionKey, ct)
               ?? new ConversationSession
               {
                   ResourceUrl = task.Event.ResourceUrl,
                   Metadata = BuildMetadata(task),
               };
    }

    protected async Task<string> InvokeAsync(
        ConversationSession session,
        string prompt,
        CopilotTask task,
        string? workingDirectory = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        using var timeoutCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Options.ResponseTimeout);
        var responseBuilder = new StringBuilder();
        var done = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var _ = timeoutCts.Token.Register(
            () => done.TrySetCanceled(timeoutCts.Token));

        await using var copilotSession = await CreateOrResumeCopilotSessionAsync(
            session, workingDirectory, timeoutCts.Token);
        copilotSession.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    responseBuilder.Append(msg.Data.Content);
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent err:
                    done.TrySetException(
                        new InvalidOperationException(
                            $"[{err.Data.ErrorType}] {err.Data.Message}"));
                    break;
                case SessionShutdownEvent shutdown when shutdown.Data.ShutdownType == SessionShutdownDataShutdownType.Error:
                    done.TrySetException(
                        new InvalidOperationException(
                            $"Session shutdown: {shutdown.Data.ErrorReason}"));
                    break;
            }
        });
        await copilotSession.SendAsync(
            new MessageOptions { Prompt = prompt },
            timeoutCts.Token);
        await done.Task;
        var result = responseBuilder.ToString();

        session.SessionId = copilotSession.SessionId;
        session.LastActiveAt = DateTimeOffset.UtcNow;
        session.Messages.Add(new ConversationMessage(MessageRole.User, prompt));
        session.Messages.Add(new ConversationMessage(MessageRole.Assistant, result));

        return result;
    }
    public abstract Task HandleAsync(CopilotTask task, CancellationToken ct = default);

    private async Task<CopilotSession> CreateOrResumeCopilotSessionAsync(
     ConversationSession session,
     string? workingDirectory = null,
     CancellationToken ct = new CancellationToken())
    {
        if (session.SessionId is not null)
        {
            try
            {
                var resumeConfig = new ResumeSessionConfig
                {
                    Model = Options.Model,
                    WorkingDirectory = workingDirectory,
                    OnPermissionRequest = PermissionHandler.ApproveAll,
                };
                return await CopilotClient.ResumeSessionAsync(
                    session.SessionId, resumeConfig, ct);
            }
            catch (Exception ex)
            {
                Log.ResumeCopilotSessionFailed(Logger, session.SessionId, ex);
            }
        }

        var createConfig = new SessionConfig
        {
            Model = Options.Model,
            WorkingDirectory = workingDirectory,
            OnPermissionRequest = PermissionHandler.ApproveAll,
        };
        return await CopilotClient.CreateSessionAsync(createConfig, ct);
    }
    private static Dictionary<string, string> BuildMetadata(CopilotTask task) => new()
    {
        ["platform"] = task.Event.Platform,
        ["repositoryId"] = task.Event.RepositoryId,
        ["author"] = task.Event.AuthorUsername ?? string.Empty,
    };

    private static class Log
    {
        private static readonly Action<ILogger, string?, Exception?> _resumeCopilotSessionFailed =
            LoggerMessage.Define<string?>(
                LogLevel.Warning,
                new EventId(1, nameof(ResumeCopilotSessionFailed)),
                "Failed to resume Copilot session {SessionId}, creating new");

        public static void ResumeCopilotSessionFailed(ILogger logger, string? sessionId, Exception? exception) =>
            _resumeCopilotSessionFailed(logger, sessionId, exception);
    }
}
