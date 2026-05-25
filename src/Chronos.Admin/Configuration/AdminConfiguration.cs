namespace Chronos.Admin.Configuration;

/// <summary>
/// Platform admin settings. See docs/Chronos.Admin.md in this project.
/// </summary>
public sealed class AdminConfiguration
{
    public const string SectionName = "AdminConfiguration";

    public string CredStorePath { get; set; } = "./data/admin-creds.db";

    public string? CredStoreConnection { get; set; }

    public string DefaultEmail { get; set; } = string.Empty;

    public string DefaultPassword { get; set; } = string.Empty;

    public string DefaultFirstName { get; set; } = "Platform";

    public string DefaultLastName { get; set; } = "Administrator";

    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ChronosAdmin";

    public string Audience { get; set; } = "ChronosAdminCli";

    public int TokenExpiryMinutes { get; set; } = 480;
}
