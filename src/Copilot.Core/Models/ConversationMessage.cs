using System.Text.Json.Serialization;

namespace Copilot.Core.Models;

public sealed record ConversationMessage
{
    [JsonConstructor]
    public ConversationMessage(MessageRole role, string content)
    {
        Role = role;
        Content = content;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public MessageRole Role { get; init; }
    public string Content { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public static ConversationMessage Create(MessageRole role, string content) =>
        new(role, content);
}