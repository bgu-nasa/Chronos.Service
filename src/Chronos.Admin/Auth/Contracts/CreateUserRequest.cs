namespace Chronos.Admin.Auth.Contracts;

public record CreateUserRequest(string Email, string FirstName, string LastName, string Password);
