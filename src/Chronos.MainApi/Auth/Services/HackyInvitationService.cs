using System.Text;

namespace Chronos.MainApi.Auth.Services;

// TODO Remove this bullshit - couple to admin service once ready
public class HackyInvitationService
{
    private static readonly IReadOnlyList<string> InviteCodes =
    [
        "BGUSTAFF2026",
        "BGUDEVTEAM2026",
        "TESTCODE_h3587",
        "STAGINGCUSTOMER",
        "PRIVATEPREVIEW10"
    ];

    public bool VerifyInviteCode(string code) => InviteCodes.Contains(code);
}