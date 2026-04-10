namespace Copilot.Provider.GitLab.Configuration;

public class GitLabClientOptions
{
    public string BaseUrl { get; set; } = default!;
    public string? AccessToken { get; set; }
    public string AgentUsername { get; set; } = "copilot";
    public string? WebhookSecret { get; set; }
}