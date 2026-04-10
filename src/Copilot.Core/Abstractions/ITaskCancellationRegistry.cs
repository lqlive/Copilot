using Copilot.Core.Models;

namespace Copilot.Core.Abstractions;

public interface ITaskCancellationRegistry
{
    CancellationToken Register(SessionKey key);
    Task CancelAsync(SessionKey key, CancellationToken ct = default);
    void Remove(SessionKey key);
}
