using Chronos.Admin.Auth.Session;
using Chronos.Shared.Exceptions;

namespace Chronos.Admin.Cli;

public static class AdminExitCodes
{
    public const int Success = 0;
    public const int GeneralError = 1;
    public const int AuthenticationRequired = 2;
    public const int InvalidArguments = 3;
    public const int NotFound = 4;

    public static int FromException(Exception ex) =>
        ex switch
        {
            AdminSessionRequiredException => AuthenticationRequired,
            UnauthorizedException => GeneralError,
            BadRequestException => InvalidArguments,
            ArgumentException => InvalidArguments,
            NotFoundException => NotFound,
            _ => GeneralError
        };
}
