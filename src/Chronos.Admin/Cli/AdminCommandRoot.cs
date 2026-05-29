using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.EntityFrameworkCore;
using Chronos.Admin.Auth.Contracts;
using Chronos.Admin.Auth.Services;
using Chronos.Admin.Auth.Session;
using Chronos.Admin.Cli.Output;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Chronos.Admin.Cli;

public static class AdminCommandRoot
{
    public static RootCommand Build(IHost host)
    {
        var root = new RootCommand("Chronos platform administration CLI.");

        var emailOption = new Option<string?>("--email", "Platform admin email address.");
        var passwordOption = new Option<string?>("--password", "Password (prompted if omitted).");
        var loginCommand = new Command("login", "Sign in and save a session for later commands.");
        loginCommand.AddOption(emailOption);
        loginCommand.AddOption(passwordOption);
        loginCommand.SetHandler(async (InvocationContext context) =>
        {
            var email = context.ParseResult.GetValueForOption(emailOption);
            var password = context.ParseResult.GetValueForOption(passwordOption);
            context.ExitCode = await RunLoginAsync(host, email, password);
        });

        var logoutCommand = new Command("logout", "Clear the saved admin session on this machine.");
        logoutCommand.SetHandler(async (InvocationContext context) =>
        {
            context.ExitCode = await RunLogoutAsync(host);
        });

        var accountsCommand = new Command("accounts", "Manage platform admin accounts.");

        var accountsListCommand = new Command("list", "List platform admin accounts.");
        accountsListCommand.SetHandler(async (InvocationContext context) =>
        {
            context.ExitCode = await RunAccountsListAsync(host);
        });

        var accountsAddCommand = new Command("add", "Add a platform admin account.");
        var addEmailArgument = new Argument<string>("email", "Email address for the new account.");
        var addFirstNameOption = new Option<string>("--first-name", "Account first name.");
        var addLastNameOption = new Option<string>("--last-name", "Account last name.");
        accountsAddCommand.AddArgument(addEmailArgument);
        accountsAddCommand.AddOption(addFirstNameOption);
        accountsAddCommand.AddOption(addLastNameOption);
        accountsAddCommand.AddOption(passwordOption);
        accountsAddCommand.SetHandler(async (InvocationContext context) =>
        {
            var email = context.ParseResult.GetValueForArgument(addEmailArgument);
            var firstName = context.ParseResult.GetValueForOption(addFirstNameOption);
            var lastName = context.ParseResult.GetValueForOption(addLastNameOption);
            var password = context.ParseResult.GetValueForOption(passwordOption);
            context.ExitCode = await RunAccountsAddAsync(host, email, firstName, lastName, password);
        });

        accountsCommand.AddCommand(accountsListCommand);
        accountsCommand.AddCommand(accountsAddCommand);

        root.AddCommand(loginCommand);
        root.AddCommand(logoutCommand);
        root.AddCommand(accountsCommand);

        return root;
    }

    private static async Task<int> RunLoginAsync(IHost host, string? email, string? password)
    {
        try
        {
            email = RequireValue(email, "Email");
            password = RequireValue(password, "Password", secret: true);

            using var scope = host.Services.CreateScope();
            var bootstrap = scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>();
            var auth = scope.ServiceProvider.GetRequiredService<IAdminAuthService>();
            var sessionStore = scope.ServiceProvider.GetRequiredService<IAdminSessionStore>();

            await bootstrap.EnsureBootstrapAsync();
            var response = await auth.LoginAsync(new LoginRequest(email, password));
            await sessionStore.SaveTokenAsync(response.Token);

            Console.WriteLine("Login successful.");
            return AdminExitCodes.Success;
        }
        catch (Exception ex)
        {
            return WriteError(ex);
        }
    }

    private static async Task<int> RunLogoutAsync(IHost host)
    {
        var sessionStore = host.Services.GetRequiredService<IAdminSessionStore>();
        await sessionStore.ClearAsync();
        Console.WriteLine("Session cleared.");
        return AdminExitCodes.Success;
    }

    private static async Task<int> RunAccountsListAsync(IHost host)
    {
        try
        {
            using var scope = host.Services.CreateScope();
            var bootstrap = scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>();
            var sessionGuard = scope.ServiceProvider.GetRequiredService<IAdminSessionGuard>();
            var auth = scope.ServiceProvider.GetRequiredService<IAdminAuthService>();

            await bootstrap.EnsureBootstrapAsync();
            await sessionGuard.RequireValidSessionAsync();

            var accounts = await auth.ListAccountsAsync();
            AdminAccountTableWriter.Write(accounts);
            return AdminExitCodes.Success;
        }
        catch (Exception ex)
        {
            return WriteError(ex);
        }
    }

    private static async Task<int> RunAccountsAddAsync(
        IHost host,
        string email,
        string? firstName,
        string? lastName,
        string? password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                throw new ArgumentException(
                    "Both --first-name and --last-name are required. Example: accounts add user@example.com --first-name Jane --last-name Doe --password Secret1");
            }

            password = RequireValue(password, "Password", secret: true);

            using var scope = host.Services.CreateScope();
            var bootstrap = scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>();
            var sessionGuard = scope.ServiceProvider.GetRequiredService<IAdminSessionGuard>();
            var auth = scope.ServiceProvider.GetRequiredService<IAdminAuthService>();

            await bootstrap.EnsureBootstrapAsync();
            await sessionGuard.RequireValidSessionAsync();

            var created = await auth.AddAccountAsync(
                new CreateUserRequest(email, firstName, lastName, password));

            Console.WriteLine($"Account created: {created.Email} ({created.Id})");
            return AdminExitCodes.Success;
        }
        catch (Exception ex)
        {
            return WriteError(ex);
        }
    }

    private static string RequireValue(string? value, string prompt, bool secret = false)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        Console.Write($"{prompt}: ");
        if (secret)
        {
            value = ReadSecret();
        }
        else
        {
            value = Console.ReadLine();
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{prompt} is required.");
        }

        return value;
    }

    private static string ReadSecret()
    {
        var buffer = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Count > 0)
                {
                    buffer.RemoveAt(buffer.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Add(key.KeyChar);
            }
        }

        return new string(buffer.ToArray());
    }

    private static int WriteError(Exception ex)
    {
        var message = ex switch
        {
            UnauthorizedException => "Invalid credentials.",
            BadRequestException bad => bad.Message,
            DbUpdateException db when db.InnerException?.Message.Contains("FirstName", StringComparison.OrdinalIgnoreCase) == true
                => "First name is required. Use --first-name.",
            DbUpdateException db when db.InnerException?.Message.Contains("LastName", StringComparison.OrdinalIgnoreCase) == true
                => "Last name is required. Use --last-name.",
            DbUpdateException db => db.InnerException?.Message ?? db.Message,
            _ => ex.Message
        };

        if (ex is AdminSessionRequiredException)
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.Error.WriteLine(message);
        }

        return AdminExitCodes.FromException(ex);
    }
}
