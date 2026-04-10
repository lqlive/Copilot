using Copilot.Core.Models;

namespace Copilot.Agent.Commands;

public interface ICommandHandler
{
    Task HandleAsync(CopilotTask task, CancellationToken cancellationToken);
}
