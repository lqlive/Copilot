using GitHub.Copilot.SDK;
using Copilot.Agent.Configuration;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Copilot.Agent.Commands;

internal sealed class PullRequestCommentCommandHandler : BaseCommandHandler
{
    private readonly IWorkspaceManager _workspaceManager;

    public PullRequestCommentCommandHandler(
        ISessionStore sessionStore,
        CopilotClient copilotClient,
        IWorkspaceManager workspaceManager,
        IGitProviderClient gitProviderClient,
        IOptions<CopilotOptions> options,
        ILogger<PullRequestCommentCommandHandler> logger)
        : base(sessionStore, copilotClient, gitProviderClient, options, logger)
    {
        _workspaceManager = workspaceManager;
    }

    public override async Task HandleAsync(CopilotTask task, CancellationToken ct = default)
    {
        string? workspacePath = null;

        try
        {
            var repository = await GitProviderClient.GetAsync(task.Event.RepositoryId, ct);
            var pullRequest = await GitProviderClient.GetPullRequestAsync(
                task.Event.RepositoryId,
                task.Event.SessionKey.ResourceId,
                ct);

            var sourceBranch = pullRequest.SourceBranch;

            workspacePath = await _workspaceManager.CloneAsync(
                task.Event.RepositoryId,
                sourceBranch,
                repository.HttpUrl,
                ct);

            var session = await GetOrCreateSessionAsync(task, ct);

            var prompt = $"""
                A user left a comment on the current pull request and requested code changes.

                Please update the codebase in the current branch based on the following comment:

                {task.Event.TriggerComment}

                Requirements:
                - Apply the requested changes directly in the current branch.
                - Keep the implementation minimal and consistent with the existing code style.
                - Do not create a new branch.
                - Do not open a new pull request.
                - After finishing the changes, stop and let the system commit and push automatically.
                """;

            var response = await InvokeAsync(
                session,
                prompt,
                task,
                workingDirectory: workspacePath,
                cancellationToken: ct);

            await _workspaceManager.CommitAndPushAsync(
                workspacePath,
                sourceBranch,
                $"copilot: address PR feedback - {task.Event.TriggerComment}",
                ct);

            await GitProviderClient.PostPullRequestCommentAsync(
                task.Event.RepositoryId,
                task.Event.SessionKey.ResourceId,
                $"Updated branch `{sourceBranch}` based on the requested changes.\n\n{response}",
                ct);
        }
        finally
        {
            if (workspacePath is not null)
            {
                await _workspaceManager.CleanupAsync(workspacePath);
            }
        }
    }
}