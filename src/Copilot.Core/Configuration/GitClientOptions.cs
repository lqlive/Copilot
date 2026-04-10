namespace Copilot.Core.Configuration;

public class GitClientOptions
{
    public const string SectionName = "GitClient";
    public string GitPath { get; set; } = "git";
    public string AgentName { get; set; } = "Copilot Bot";
    public string AgentEmail { get; set; } = "copilot-bot@noreply";
    public string? AccessToken { get; set; }
    public IReadOnlyDictionary<string, string>? Environment { get; set; }
}