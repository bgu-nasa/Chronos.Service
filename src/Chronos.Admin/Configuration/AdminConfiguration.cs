namespace Chronos.Admin.Configuration;

/// <summary>
/// Platform admin settings. Properties are placeholders until auth is implemented;
/// see docs/Chronos.Admin.md in this project.
/// </summary>
public sealed class AdminConfiguration
{
    public const string SectionName = "AdminConfiguration";

    public string CredStorePath { get; set; } = "./data/admin-creds.db";

    public string DefaultEmail { get; set; } = string.Empty;

    public string DefaultPassword { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public int TokenExpiryMinutes { get; set; } = 480;
}
