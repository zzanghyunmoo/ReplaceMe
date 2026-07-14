using DevAutomation.Infrastructure.DependencyInjection;
using DevAutomation.Infrastructure.Persistence;
using DevAutomation.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "DEVAUTOMATION_");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/devautomation-worker-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.AddDevAutomationCore(builder.Configuration);
builder.Services.AddDevAutomationInfrastructure(builder.Configuration);
builder.Services.AddDevAutomationAgentWorker();

builder.Services.AddDevAutomationOpenTelemetry(builder.Configuration);

var host = builder.Build();

if (builder.Configuration.GetValue("Database:ApplyMigrations", true))
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DevAutomationDbContext>();
    await dbContext.Database.MigrateAsync();
}

await host.RunAsync();
