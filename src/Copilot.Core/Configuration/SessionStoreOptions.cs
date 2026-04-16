namespace Copilot.Core.Configuration;

public sealed class SessionStoreOptions
{
    public const string SectionName = "SessionStore";

    public string? ConnectionString { get; set; }

    public string InstanceName { get; set; } = "copilot:session:";

    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan? LocalCacheExpiration { get; set; }
}