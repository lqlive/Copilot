using Copilot.Core.Models;

namespace Copilot.Core.Abstractions;
public interface ITaskQueue
{
    Task ProduceAsync(CopilotEvent @event, CancellationToken cancellationToken = default);
    Task<CopilotTask?> ClaimNextAsync(CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(string taskId, CancellationToken cancellationToken = default);
    Task ReclaimExpiredAsync(CancellationToken cancellationToken = default);
}
