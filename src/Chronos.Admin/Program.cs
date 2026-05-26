using Chronos.Admin.CredStore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Parsing;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("AppSettings/appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"AppSettings/appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddAdminCredStore(builder.Configuration);

using var host = builder.Build();

await using (var scope = host.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdminCredDbContext>();
    await db.Database.MigrateAsync();
}

var rootCommand = new RootCommand("Chronos platform administration CLI.");
rootCommand.SetHandler(() =>
{
    Console.WriteLine("Chronos.Admin — credential store ready (auth commands ship in a later PR).");
    Console.WriteLine("See docs/Chronos.Admin.md in this project for the design.");
});

return await rootCommand.Parse(args).InvokeAsync();
