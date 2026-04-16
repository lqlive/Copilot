using Copilot.Core.Models;

namespace Copilot.Core.Abstractions;

public interface ISessionStore
{
    Task<ConversationSession?> GetOrCreateAsync(SessionKey key, CancellationToken cancellationToken = default);
    Task UpdateAsync(SessionKey key, ConversationSession session, CancellationToken cancellationToken = default);
    Task RemoveAsync(SessionKey key, CancellationToken cancellationToken = default);
}