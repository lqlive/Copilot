namespace Copilot.Core.Models;

public sealed class CopilotTask
{
    public string Id { get; init; }
    public CopilotEvent Event { get; init; }
    public DateTimeOffset EnqueuedAt { get; init; }
    public int RetryCount { get; init; }
}