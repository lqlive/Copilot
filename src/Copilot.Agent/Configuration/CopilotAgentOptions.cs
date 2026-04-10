namespace Copilot.Agent.Configuration;

public class CopilotAgentOptions
{
    public const string SectionName = "CopilotAgent";
    public int MaxConcurrentTasks { get; set; }
}
