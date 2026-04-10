using Copilot.Core.Models;

namespace Copilot.Core.Abstractions;

public interface ISessionStore
{
    Task<ConversationSession?> GetAsync(SessionKey key, CancellationToken cancellationToken = default);
    Task RemoveAsync(SessionKey key, CancellationToken cancellationToken = default);
}