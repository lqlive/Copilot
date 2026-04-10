namespace Copilot.Core.Abstractions;

public interface IWorkspaceManager
{
    Task<string> CloneAsync(
      string repositoryId,
      string branch,
      string cloneUrl,
      CancellationToken ct = default);

    Task<string> CreateBranchAsync(
        string workspacePath,
        string branchName,
        CancellationToken ct = default);

    Task<string> CommitAndPushAsync(
       string workspacePath,
       string branchName,
       string commitMessage,
       CancellationToken ct = default);

    Task CleanupAsync(string workspacePath);
}
