namespace Copilot.Core.Models;

public sealed record ConversationMessage(
    MessageRole Role,
    string Content,
    DateTimeOffset CreatedAt)
{
    public ConversationMessage(MessageRole role, string content)
        : this(role, content, DateTimeOffset.UtcNow) { }
}