using Chronos.MainApi.Auth.Services;

namespace Chronos.Tests.MainApi.Auth;

[TestFixture]
[Category("Unit")]
public class HackyInvitationServiceTests
{
    [Test]
    public void ValidateGeneratedCode_ShouldReturnTrue()
    {
        var service = new HackyInvitationService();
        var code = service.GenerateInviteCode();
        Assert.That(service.VerifyInviteCode(code), Is.True);
    }
}