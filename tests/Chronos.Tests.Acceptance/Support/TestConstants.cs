namespace Chronos.Tests.Acceptance.Support;

/// <summary>
/// Shared constants for acceptance tests, centralized so they are defined once.
/// </summary>
public static class TestConstants
{
    /// <summary>Private-beta invite code accepted by the registration endpoint.</summary>
    public const string InviteCode = "TESTCODE_h3587";

    /// <summary>Default password that satisfies the password policy for seeded users.</summary>
    public const string DefaultPassword = "Passw0rd1";
}
