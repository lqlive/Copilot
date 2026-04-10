namespace Copilot.Core.Models;

public class Repository
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string HttpUrl { get; set; }
    public required string SshUrl { get; set; }
    public required string DefaultBranch { get; set; }
}
