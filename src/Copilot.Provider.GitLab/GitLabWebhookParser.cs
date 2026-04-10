using System.Text.Json;
using Copilot.Core.Abstractions;
using Copilot.Core.Models;
using Copilot.Provider.GitLab.Configuration;
using Copilot.Provider.GitLab.Models;
using Microsoft.Extensions.Options;

namespace Copilot.Parser;

public class GitLabWebhookParser : IWebhookParser
{
    private readonly GitLabClientOptions _gitLabClientOptions;

    public GitLabWebhookParser(IOptions<GitLabClientOptions> gitLabClientOptions)
    {
        _gitLabClientOptions = gitLabClientOptions.Value;
    }

    public CopilotEvent? Parse(string eventHeader, JsonElement payload)
    {
        return eventHeader switch
        {
            "Note Hook" => ParseNoteHook(payload),
            "Issue Hook" => ParseIssueHook(payload),
            _ => null
        };
    }

    private CopilotEvent? ParseNoteHook(JsonElement payload)
    {
        var data = payload.Deserialize<GitLabNotePayload>();
        if (data?.ObjectAttributes is not { } note) return null;

        if (note.System) return null;

        if (note.Note?.Contains($"@{_gitLabClientOptions.AgentUsername}", StringComparison.OrdinalIgnoreCase) != true)
            return null;

        var repositoryId = data.Project?.PathWithNamespace ?? string.Empty;
        var scope = note.NoteableType == "MergeRequest" ? ConversationScope.PullRequest : ConversationScope.Issue;
        var type = note.NoteableType == "MergeRequest" ? CopilotEventType.PullRequestComment : CopilotEventType.IssueComment;
        var resourceId = note.NoteableId;

        return new CopilotEvent(
            Platform: "gitlab",
            RepositoryId: repositoryId,
            Type: type,
            SessionKey: new SessionKey("gitlab", repositoryId, scope, resourceId),
            TriggerComment: note.Note,
            AuthorUsername: data.User?.Username,
            ResourceUrl: note.Url
        );
    }

    private CopilotEvent? ParseIssueHook(JsonElement payload)
    {
        var data = payload.Deserialize<GitLabIssuePayload>();
        var issue = data?.ObjectAttributes;

        if (data is null || issue is null)
            return null;

        if (!IsSupportedIssueAction(issue.Action))
            return null;

        var repositoryId = data.Project?.Id;
        if (repositoryId is null)
            return null;

        var agentUsername = _gitLabClientOptions.AgentUsername;
        var assigneeChange = data.Changes?.Assignees;

        if (WasAssignedToAgent(assigneeChange, agentUsername))
        {
            return new CopilotEvent(
                Platform: "gitlab",
                RepositoryId: repositoryId.ToString(),
                Type: CopilotEventType.IssueAssigned,
                SessionKey: new SessionKey("gitlab", repositoryId.ToString(), ConversationScope.Issue, issue.Id),
                TriggerComment: issue.Description,
                AuthorUsername: data.User?.Username,
                ResourceUrl: issue.Url);
        }

        if (WasUnassignedFromAgent(assigneeChange, agentUsername))
        {
            return new CopilotEvent(
                Platform: "gitlab",
                RepositoryId: repositoryId.ToString(),
                Type: CopilotEventType.IssueUnassigned,
                SessionKey: new SessionKey("gitlab", repositoryId.ToString(), ConversationScope.Issue, issue.Id),
                TriggerComment: null,
                AuthorUsername: data.User?.Username,
                ResourceUrl: issue.Url);
        }

        return null;
    }

    private static bool IsSupportedIssueAction(string? action) => action is "open" or "update";

    private static bool WasAssignedToAgent(GitLabAssigneeChange? assignees, string agentUsername)
    {
        if (string.IsNullOrWhiteSpace(agentUsername))
            return false;

        var comparer = StringComparer.OrdinalIgnoreCase;
        var current = assignees?.Current ?? [];
        var previous = assignees?.Previous ?? [];
        var assignedNow = current.Any(x => comparer.Equals(x.Username, agentUsername));
        if (!assignedNow)
            return false;

        var alreadyAssigned = previous.Any(x => comparer.Equals(x.Username, agentUsername));
        return !alreadyAssigned;
    }

    private static bool WasUnassignedFromAgent(GitLabAssigneeChange? assignees, string agentUsername)
    {
        if (string.IsNullOrWhiteSpace(agentUsername))
            return false;

        var comparer = StringComparer.OrdinalIgnoreCase;
        var current = assignees?.Current ?? [];
        var previous = assignees?.Previous ?? [];

        return previous.Any(x => comparer.Equals(x.Username, agentUsername))
            && !current.Any(x => comparer.Equals(x.Username, agentUsername));
    }
}