using Copilot.Core.Models;

namespace Copilot.Core.Abstractions;

public interface ITaskDispatcher
{
    Task DispatchAsync(CopilotTask task, CancellationToken cancellationToken = default);
}
