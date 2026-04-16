using System.Text.Json.Serialization;

namespace Copilot.Provider.GitLab.Models;

internal sealed class GitLabMergeRequestPayload
{
    [JsonPropertyName("user")]
    public GitLabUser? User { get; set; }
    [JsonPropertyName("project")]
    public GitLabProject? Project { get; set; }
    [JsonPropertyName("object_attributes")]
    public GitLabMergeRequestAttributes? ObjectAttributes { get; set; }
    [JsonPropertyName("changes")]
    public GitLabMergeRequestChanges? Changes { get; set; }
    [JsonPropertyName("reviewers")]
    public List<GitLabUser> Reviewers { get; set; } = [];
}
internal sealed class GitLabMergeRequestAttributes
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("iid")]
    public long Iid { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("action")]
    public string? Action { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
internal sealed class GitLabMergeRequestChanges
{
    [JsonPropertyName("reviewers")]
    public GitLabReviewerChange? Reviewers { get; set; }
}
internal sealed class GitLabReviewerChange
{
    [JsonPropertyName("previous")]
    public List<GitLabUser> Previous { get; set; } = [];
    [JsonPropertyName("current")]
    public List<GitLabUser> Current { get; set; } = [];
}