namespace Copilot.Core.Models;

public sealed class ConversationSession
{
    public string? SessionId { get; set; }
    public string? ResourceUrl { get; set; }
    public List<ConversationMessage> Messages { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;
}