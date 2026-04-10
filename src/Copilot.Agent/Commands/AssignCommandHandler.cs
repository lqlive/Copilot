using Copilot.Agent.Configuration;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Copilot.Agent.Commands;

internal sealed class AssignCommandHandler : BaseCommandHandler
{
    private readonly IWorkspaceManager _workspaceManager;
    public AssignCommandHandler(
        ISessionStore sessionStore,
        CopilotClient copilotClient,
        IWorkspaceManager workspaceManager,
        IGitProviderClient gitProviderClient, 
        IOptions<CopilotOptions> options,
        ILogger<AssignCommandHandler> logger) 
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

            await GitProviderClient.PostIssueCommentAsync(
             task.Event.RepositoryId,
             task.Event.SessionKey.ResourceId,
                "The task has been assigned to @Copilot", ct);

            workspacePath = await _workspaceManager.CloneAsync(
                task.Event.RepositoryId, repository.DefaultBranch, repository.HttpUrl, ct);

            var newBranch = $"copilot/{Guid.NewGuid():N}";
            await _workspaceManager.CreateBranchAsync(workspacePath, newBranch, ct);

            var session = await GetOrCreateSessionAsync(task, ct);

            var prompt = $"""
            Please implement the requested changes based on the following instruction:
            {task.Event.TriggerComment}
            You are already in the repository root directory and may read or modify files directly.
            Do not commit or push changes manually. The system will handle that automatically after you finish.
            """;

            await InvokeAsync(session, prompt, task, workingDirectory: workspacePath, ct);

            await _workspaceManager.CommitAndPushAsync(
                workspacePath, newBranch,
                $"copilot: {task.Event.TriggerComment}", ct);

            var prUrl = await GitProviderClient.CreatePullRequestAsync(
                task.Event.RepositoryId,
                task.Event.TriggerComment ?? string.Empty,
                sourceBranch: newBranch,
                targetBranch: repository.DefaultBranch,
                description: "Created by Copilot", ct);

            await GitProviderClient.PostIssueCommentAsync(
                task.Event.RepositoryId,
                task.Event.SessionKey.ResourceId,
                $"PR has been created: {prUrl}", ct);
        }
        finally
        {
            if (workspacePath is not null)
                await _workspaceManager.CleanupAsync(workspacePath);
        }
    }

    private sealed record CreatePrArgs(
        string SourceBranch,
        string TargetBranch,
        string Title);
}
