using Chronos.MainApi.Auth.Validation;
using Chronos.Shared.Exceptions;

namespace Chronos.Tests.MainApi.Auth.Validation;

[TestFixture]
[Category("Unit")]
public class PasswordValidatorTests
{
    [TestCase(null)]
    [TestCase("")]
    public void GivenNullOrEmpty_WhenValidatePassword_ThenThrowsBadRequest(string? password)
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            PasswordValidator.ValidatePassword(password!));

        Assert.That(ex!.Message, Does.Contain("cannot be empty"));
    }

    [TestCase("Ab1xyzw")]
    [TestCase("Ab1")]
    [TestCase("A1x")]
    public void GivenTooShort_WhenValidatePassword_ThenThrowsBadRequest(string password)
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            PasswordValidator.ValidatePassword(password));

        Assert.That(ex!.Message, Does.Contain("at least 8 characters"));
    }

    [Test]
    public void GivenTooLong_WhenValidatePassword_ThenThrowsBadRequest()
    {
        var password = new string('A', 64) + new string('a', 64) + "1";

        var ex = Assert.Throws<BadRequestException>(() =>
            PasswordValidator.ValidatePassword(password));

        Assert.That(ex!.Message, Does.Contain("must not exceed 128 characters"));
    }

    [TestCase("Passw0rd test")]
    [TestCase("Passw0rd\there")]
    [TestCase("Pass w0rd")]
    public void GivenWhitespace_WhenValidatePassword_ThenThrowsBadRequest(string password)
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            PasswordValidator.ValidatePassword(password));

        Assert.That(ex!.Message, Does.Contain("whitespace"));
    }

    [Test]
    public void GivenNoUppercase_WhenValidatePassword_ThenThrowsBadRequest()
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            PasswordValidator.ValidatePassword("password1"));

        Assert.That(ex!.Message, Does.Contain("uppercase"));
    }

    [Test]
    public void GivenNoLowercase_WhenValidatePassword_ThenThrowsBadRequest()
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            PasswordValidator.ValidatePassword("PASSWORD1"));

        Assert.That(ex!.Message, Does.Contain("lowercase"));
    }

    [Test]
    public void GivenNoDigit_WhenValidatePassword_ThenThrowsBadRequest()
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            PasswordValidator.ValidatePassword("Password"));

        Assert.That(ex!.Message, Does.Contain("digit"));
    }

    [Test]
    public void GivenExactlyMinLength_WhenValidatePassword_ThenDoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
            PasswordValidator.ValidatePassword("Passw0rd"));
    }

    [Test]
    public void GivenExactlyMaxLength_WhenValidatePassword_ThenDoesNotThrow()
    {
        var password = new string('A', 63) + new string('a', 64) + "1";

        Assert.DoesNotThrow(() =>
            PasswordValidator.ValidatePassword(password));
    }

    [Test]
    public void GivenValidPassword_WhenValidatePassword_ThenDoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
            PasswordValidator.ValidatePassword("MySecure1"));
    }
}
