using System.Text.Json.Serialization;

namespace Copilot.Provider.GitLab.Models;

internal sealed class GitLabIssuePayload
{
    [JsonPropertyName("user")]
    public GitLabUser? User { get; set; }
    [JsonPropertyName("project")]
    public GitLabProject? Project { get; set; }
    [JsonPropertyName("object_attributes")]
    public GitLabIssueAttributes? ObjectAttributes { get; set; }
    [JsonPropertyName("changes")]
    public GitLabIssueChanges? Changes { get; set; }
}
internal sealed class GitLabIssueAttributes
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("action")]
    public string? Action { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
internal sealed class GitLabIssueChanges
{
    [JsonPropertyName("assignees")]
    public GitLabAssigneeChange? Assignees { get; set; }
}
internal sealed class GitLabAssigneeChange
{
    [JsonPropertyName("previous")]
    public List<GitLabUser> Previous { get; set; } = [];
    [JsonPropertyName("current")]
    public List<GitLabUser> Current { get; set; } = [];
}
internal sealed class GitLabUser
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }
}
internal sealed class GitLabProject
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    [JsonPropertyName("path_with_namespace")]
    public string? PathWithNamespace { get; set; }
}