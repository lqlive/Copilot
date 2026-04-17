namespace Copilot.Agent.Configuration;

public sealed class CopilotOptions
{
    public const string SectionName = "Copilot";
    public string Model { get; set; } = "gpt-4o";
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromMinutes(3);
    public string CliPath { get; set; } = "copilot";
    public string? GitHubToken { get; set; }
}