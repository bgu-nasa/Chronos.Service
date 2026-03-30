using Chronos.Data.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Required by AppDbContext — null in non-web context, bypasses tenant query filters
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var host = builder.Build();
host.Run();
