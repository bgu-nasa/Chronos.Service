using System.Text;

namespace Chronos.MainApi.Auth.Services;

public class HackyInvitationService
{
    private const int PrefixLength = 8;
    private static readonly char[] alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly ILogger<HackyInvitationService> _logger;
    private readonly string _prefix = "";

    public HackyInvitationService(ILogger<HackyInvitationService> logger, bool setPrefix = true)
    {
        _logger = logger;

        if (setPrefix)
        {
            _prefix = GenerateRandomString(PrefixLength);
            logger.LogInformation("Generated a prefix for invite codes: {InviteCodePrefix}", _prefix);
        }
        else
        {
            _logger.LogWarning("No prefix set for invite codes - should not happen outside of development environments.");
        }
    }

    private string GenerateRandomString(int length)
    {
        var random = new Random();

        return new string(Enumerable.Range(0, length)
            .Select(_ => alphabet[random.Next(alphabet.Length)])
            .ToArray());
    }

    // 
    private static readonly IReadOnlyList<string> InviteCodes =
    [
        "BGUSTAFF2026",
        "BGUDEVTEAM2026",
        "TESTCODE_h3587",
        "STAGINGCUSTOMER",
        "PRIVATEPREVIEW10"
    ];

    public bool VerifyInviteCode(string code)
    {
        if (string.IsNullOrWhiteSpace(_prefix))
        {
            return InviteCodes.Contains(code);
        }

        if (!code.StartsWith(_prefix))
        {
            return false;
        }

        return InviteCodes.Contains(code[_prefix.Length..]);
    }
}