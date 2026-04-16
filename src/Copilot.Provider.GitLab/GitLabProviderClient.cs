using System.Text;

using Copilot.Core.Abstractions;
using Copilot.Core.Models;

using GitLabApiClient;
using GitLabApiClient.Models.MergeRequests.Requests;
using GitLabApiClient.Models.Notes.Requests;

namespace Copilot.Provider.GitLab;

public class GitLabProviderClient : IGitProviderClient
{
    private readonly GitLabClient _gitLabClient; 
    public GitLabProviderClient(GitLabClient gitLabClient)
    {
        _gitLabClient = gitLabClient;
    }

    public async Task<Repository> GetAsync(string repositoryId, CancellationToken ct = default)
    {
        var projectId = int.Parse(repositoryId);
        var response = await _gitLabClient.Projects.GetAsync(projectId);

        return new Repository
        {
            Id = response.Id,
            Name = response.Name,
            HttpUrl = response.HttpUrlToRepo,
            SshUrl = response.SshUrlToRepo,
            DefaultBranch = response.DefaultBranch
        };
    }

    public async Task<PullRequest> GetPullRequestAsync(string repositoryId, long resourceId, CancellationToken ct = default)
    {
        var projectId = int.Parse(repositoryId);
        var mergeRequest = await _gitLabClient.MergeRequests.GetAsync(projectId, (int)resourceId);

        return new PullRequest
        {
            Id = mergeRequest.Id,
            Title = mergeRequest.Title,
            Description = mergeRequest.Description,
            SourceBranch = mergeRequest.SourceBranch,
            TargetBranch = mergeRequest.TargetBranch,
            WebUrl = mergeRequest.WebUrl
        };
    }

    public async Task<string> GetPullRequestDiffAsync(string repositoryId, long resourceId, CancellationToken ct = default)
    {
        var projectId = int.Parse(repositoryId);
        var mergeRequest = await _gitLabClient.MergeRequests.GetAsync(projectId, (int)resourceId);

        var diffs = await _gitLabClient.Commits.GetDiffsAsync(projectId, mergeRequest.Sha);
        if (diffs is null || diffs.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var diff in diffs)
        {
            var oldPath = string.IsNullOrWhiteSpace(diff.OldPath) ? diff.NewPath : diff.OldPath;
            var newPath = string.IsNullOrWhiteSpace(diff.NewPath) ? diff.OldPath : diff.NewPath;
            sb.AppendLine($"diff --git a/{oldPath} b/{newPath}");
            if (diff.IsNewFile)
                sb.AppendLine("new file mode 100644");
            if (diff.IsDeletedFile)
                sb.AppendLine("deleted file mode 100644");
            if (diff.IsRenamedFile)
            {
                sb.AppendLine($"rename from {oldPath}");
                sb.AppendLine($"rename to {newPath}");
            }
            sb.AppendLine($"--- a/{oldPath}");
            sb.AppendLine($"+++ b/{newPath}");
            if (!string.IsNullOrWhiteSpace(diff.DiffText))
                sb.AppendLine(diff.DiffText);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public  async Task PostIssueCommentAsync(string repositoryId, long resourceId, string body, CancellationToken ct = default)
    {
        if (body.Length > 65000)
            body = body[..65000];

        var projectId = int.Parse(repositoryId);
        var request = new CreateIssueNoteRequest(body);

        var response = await _gitLabClient.Issues.CreateNoteAsync(projectId, (int)resourceId, request);
    }
    public async  Task PostPullRequestCommentAsync(string repositoryId, long resourceId, string body, CancellationToken ct = default)
    {
        if (body.Length > 65000)
            body = body[..65000];

        var projectId = int.Parse(repositoryId);
        var request = new CreateMergeRequestNoteRequest(body);

        var reponse = await _gitLabClient.MergeRequests.CreateNoteAsync(projectId, (int)resourceId, request);
    }

    public async Task<string> CreatePullRequestAsync(string repositoryId, string title, string sourceBranch,
        string targetBranch, string description, CancellationToken ct = default)
    {
        var projectId = int.Parse(repositoryId);
        var request = new CreateMergeRequest(sourceBranch, targetBranch, title)
        {
            RemoveSourceBranch = true,
            Description = description
        };
        var response = await _gitLabClient.MergeRequests.CreateAsync(projectId, request);
        return response?.WebUrl ?? string.Empty;
    }
}