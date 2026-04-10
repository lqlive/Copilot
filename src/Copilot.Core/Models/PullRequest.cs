namespace Copilot.Core.Models;

public class PullRequest
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? SourceBranch { get; set; }
    public string? TargetBranch { get; set; }
    public string? WebUrl { get; set; }
}
