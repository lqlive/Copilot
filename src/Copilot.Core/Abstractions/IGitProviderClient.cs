using Copilot.Core.Models;

namespace Copilot.Core.Abstractions;

public interface IGitProviderClient
{
    Task<Repository> GetAsync(string repositoryId, CancellationToken ct = default);
    Task<PullRequest> GetPullRequestAsync(string repositoryId, long resourceId, CancellationToken ct = default);
    Task<string> GetPullRequestDiffAsync(
        string repositoryId, long resourceId, CancellationToken ct = default);
    Task PostIssueCommentAsync(
        string repositoryId, long resourceId, string body, CancellationToken ct = default);
    Task PostPullRequestCommentAsync(
        string repositoryId,long resourceId, string body, CancellationToken ct = default);
    Task<string> CreatePullRequestAsync(
        string repositoryId, string title,
        string sourceBranch, string targetBranch,
        string description, CancellationToken ct = default);


}
