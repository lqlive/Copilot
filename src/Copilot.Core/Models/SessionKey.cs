namespace Copilot.Core.Models;

public sealed record SessionKey(
    string Platform,
    string RepositoryId,
    ConversationScope Scope,
    long ResourceId,
    string? FilePath = null,
    int? LineNumber = null)
{
    public override string ToString() => Scope switch
    {
        ConversationScope.ReviewComment
            => $"{Platform}:{RepositoryId}:review:{ResourceId}:{FilePath}:{LineNumber}",
        _
            => $"{Platform}:{RepositoryId}:{Scope.ToString().ToLower()}:{ResourceId}"
    };
}