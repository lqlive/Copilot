namespace Copilot.Core.Models;

public sealed record CopilotEvent(
    string Platform,
    string RepositoryId,
    CopilotEventType Type,
    SessionKey SessionKey,
    string? TriggerComment,
    string? AuthorUsername,
    string? ResourceUrl
);
