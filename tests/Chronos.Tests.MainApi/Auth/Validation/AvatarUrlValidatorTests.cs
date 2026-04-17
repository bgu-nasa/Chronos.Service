using Chronos.MainApi.Auth.Validation;
using Chronos.Shared.Exceptions;

namespace Chronos.Tests.MainApi.Auth.Validation;

[TestFixture]
[Category("Unit")]
public class AvatarUrlValidatorTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GivenNullOrWhitespace_WhenValidateAvatarUrl_ThenDoesNotThrow(string? url)
    {
        Assert.DoesNotThrow(() =>
            AvatarUrlValidator.ValidateAvatarUrl(url));
    }

    [Test]
    public void GivenTooLong_WhenValidateAvatarUrl_ThenThrowsBadRequest()
    {
        var url = "https://example.com/" + new string('a', 2029);

        var ex = Assert.Throws<BadRequestException>(() =>
            AvatarUrlValidator.ValidateAvatarUrl(url));

        Assert.That(ex!.Message, Does.Contain("must not exceed 2048 characters"));
    }

    [TestCase("not-a-url")]
    [TestCase("just some text")]
    [TestCase("://missing-scheme.com/img.png")]
    public void GivenInvalidUri_WhenValidateAvatarUrl_ThenThrowsBadRequest(string url)
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            AvatarUrlValidator.ValidateAvatarUrl(url));

        Assert.That(ex!.Message, Does.Contain("format is invalid"));
    }

    [TestCase("ftp://example.com/avatar.png")]
    [TestCase("file:///C:/avatar.png")]
    [TestCase("data:image/png;base64,abc")]
    public void GivenNonHttpScheme_WhenValidateAvatarUrl_ThenThrowsBadRequest(string url)
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            AvatarUrlValidator.ValidateAvatarUrl(url));

        Assert.That(ex!.Message, Does.Contain("HTTP or HTTPS"));
    }

    [TestCase("http://example.com/avatar.png")]
    [TestCase("https://example.com/avatar.png")]
    [TestCase("https://cdn.example.com/images/user/photo.jpg?size=200")]
    public void GivenValidHttpUrl_WhenValidateAvatarUrl_ThenDoesNotThrow(string url)
    {
        Assert.DoesNotThrow(() =>
            AvatarUrlValidator.ValidateAvatarUrl(url));
    }

    [Test]
    public void GivenExactlyMaxLength_WhenValidateAvatarUrl_ThenDoesNotThrow()
    {
        var url = "https://example.com/" + new string('a', 2028);
        Assert.That(url.Length, Is.EqualTo(2048));

        Assert.DoesNotThrow(() =>
            AvatarUrlValidator.ValidateAvatarUrl(url));
    }

    [Test]
    public void GivenOneOverMaxLength_WhenValidateAvatarUrl_ThenThrowsBadRequest()
    {
        var url = "https://example.com/" + new string('a', 2029);
        Assert.That(url.Length, Is.EqualTo(2049));

        Assert.Throws<BadRequestException>(() =>
            AvatarUrlValidator.ValidateAvatarUrl(url));
    }
}
