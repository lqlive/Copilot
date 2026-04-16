using Copilot.Agent.Configuration;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Copilot.Agent.Commands;

internal sealed class ReviewCommandHandler : BaseCommandHandler
{
    public ReviewCommandHandler(ISessionStore sessionStore,
        CopilotClient copilotClient, 
        IGitProviderClient gitProviderClient,
        IOptions<CopilotOptions> options, ILogger<ReviewCommandHandler> logger) 
        : base(sessionStore, copilotClient, gitProviderClient, options, logger)
    {
    }

    private const string SystemPrompt = """
        You are a professional code review assistant. Follow these rules:
        - Only focus on security vulnerabilities, performance issues, and obvious bugs.
        - Report no more than 5 issues in each review, ordered by severity.
        - Provide specific fix suggestions and include line references.
        - If the code looks good, explicitly reply with "LGTM".
        - Format the response in Markdown.
        """;

    public override async Task HandleAsync(CopilotTask task, CancellationToken cancellationToken = default)
    {
        var session = await GetOrCreateSessionAsync(task, cancellationToken);
 
        var diff = await GitProviderClient.GetPullRequestDiffAsync(
            task.Event.RepositoryId, task.Event.SessionKey.ResourceId, cancellationToken);

        var prompt = $@"
            {SystemPrompt}

            Please review this merge request.
            {task.Event.TriggerComment}

            ```diff
            {diff}
            ```";

        var response = await InvokeAsync(session, prompt, task, cancellationToken: cancellationToken);

        await GitProviderClient.PostPullRequestCommentAsync(
            task.Event.RepositoryId,
            task.Event.SessionKey.ResourceId,
            response, cancellationToken);

        Log.ReviewCompleted(Logger, task.Event.RepositoryId, task.Event.SessionKey.ResourceId);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, long, Exception?> _reviewCompleted =
            LoggerMessage.Define<string, long>(
                LogLevel.Information,
                new EventId(1, nameof(ReviewCompleted)),
                "Review completed for {RepositoryId} PR#{Id}");

        public static void ReviewCompleted(ILogger logger, string repositoryId, long id) =>
            _reviewCompleted(logger, repositoryId, id, null);
    }
}
