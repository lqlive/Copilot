using System.Text.Json.Serialization;

namespace Copilot.Provider.GitLab.Models;

internal sealed class GitLabNotePayload
{
    [JsonPropertyName("object_kind")]
    public string? ObjectKind { get; set; }
    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }
    [JsonPropertyName("project_id")]
    public long ProjectId { get; set; }
    [JsonPropertyName("user")]
    public GitLabUser? User { get; set; }
    [JsonPropertyName("project")]
    public GitLabProject? Project { get; set; }
    [JsonPropertyName("object_attributes")]
    public GitLabNoteAttributes? ObjectAttributes { get; set; }
    [JsonPropertyName("repository")]
    public GitLabRepository? Repository { get; set; }
    [JsonPropertyName("issue")]
    public GitLabIssueInfo? Issue { get; set; }
    [JsonPropertyName("merge_request")]
    public GitLabMergeRequestInfo? MergeRequest { get; set; }
}
internal sealed class GitLabRepository
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }
}
internal sealed class GitLabIssueInfo
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("iid")]
    public long Iid { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("state")]
    public string? State { get; set; }
    [JsonPropertyName("assignee_ids")]
    public List<long> AssigneeIds { get; set; } = [];
}
internal sealed class GitLabNoteAttributes
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("author_id")]
    public long AuthorId { get; set; }
    [JsonPropertyName("note")]
    public string? Note { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("noteable_id")]
    public long NoteableId { get; set; }
    [JsonPropertyName("noteable_type")]
    public string? NoteableType { get; set; } // Issue / MergeRequest / Commit
    [JsonPropertyName("system")]
    public bool System { get; set; }
    [JsonPropertyName("internal")]
    public bool Internal { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("action")]
    public string? Action { get; set; }
    [JsonPropertyName("discussion_id")]
    public string? DiscussionId { get; set; }
}

internal sealed class GitLabMergeRequestInfo
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("iid")]
    public long Iid { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}