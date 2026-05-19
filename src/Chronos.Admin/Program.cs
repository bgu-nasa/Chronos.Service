using Chronos.Admin;
using Chronos.Admin.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("AppSettings/appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"AppSettings/appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<AdminConfiguration>(
    builder.Configuration.GetSection(AdminConfiguration.SectionName));

// Required by AppDbContext when registered — null bypasses tenant query filters (see Offboarding).
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

builder.Services.AddAdminModule(builder.Configuration);

using var host = builder.Build();

var rootCommand = new RootCommand("Chronos platform administration CLI.");
rootCommand.SetHandler(() =>
{
    Console.WriteLine("Chronos.Admin scaffold — no commands implemented yet.");
    Console.WriteLine("See docs/Chronos.Admin.md in this project for the design.");
});

return await rootCommand.InvokeAsync(args);
