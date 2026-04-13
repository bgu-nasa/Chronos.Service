using Chronos.MainApi.Auth.Validation;
using Chronos.Shared.Exceptions;

namespace Chronos.Tests.MainApi.Auth.Validation;

[TestFixture]
[Category("Unit")]
public class EmailValidatorTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GivenNullOrWhitespace_WhenValidateEmail_ThenThrowsBadRequest(string? email)
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            EmailValidator.ValidateEmail(email!));

        Assert.That(ex!.Message, Does.Contain("cannot be empty"));
    }

    [Test]
    public void GivenTooLong_WhenValidateEmail_ThenThrowsBadRequest()
    {
        var email = new string('a', 243) + "@example.com";

        var ex = Assert.Throws<BadRequestException>(() =>
            EmailValidator.ValidateEmail(email));

        Assert.That(ex!.Message, Does.Contain("must not exceed 254 characters"));
    }

    [TestCase("plaintext")]
    [TestCase("missing-at-sign.com")]
    [TestCase("@missing-local.com")]
    [TestCase("user@")]
    [TestCase("user@nodot")]
    public void GivenInvalidFormat_WhenValidateEmail_ThenThrowsBadRequest(string email)
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            EmailValidator.ValidateEmail(email));

        Assert.That(ex!.Message, Does.Contain("format is invalid"));
    }

    [TestCase("user@example.com")]
    [TestCase("USER@EXAMPLE.COM")]
    [TestCase("first.last@domain.co.il")]
    [TestCase("a@b.c")]
    public void GivenValidEmail_WhenValidateEmail_ThenDoesNotThrow(string email)
    {
        Assert.DoesNotThrow(() =>
            EmailValidator.ValidateEmail(email));
    }

    [Test]
    public void GivenExactlyMaxLength_WhenValidateEmail_ThenDoesNotThrow()
    {
        var local = new string('a', 242);
        var email = $"{local}@example.com";
        Assert.That(email.Length, Is.EqualTo(254));

        Assert.DoesNotThrow(() =>
            EmailValidator.ValidateEmail(email));
    }

    [Test]
    public void GivenOneOverMaxLength_WhenValidateEmail_ThenThrowsBadRequest()
    {
        var local = new string('a', 243);
        var email = $"{local}@example.com";
        Assert.That(email.Length, Is.EqualTo(255));

        Assert.Throws<BadRequestException>(() =>
            EmailValidator.ValidateEmail(email));
    }
}
