namespace Chronos.Admin.Auth.Session;

public sealed class AdminSessionRequiredException(string message) : Exception(message);
