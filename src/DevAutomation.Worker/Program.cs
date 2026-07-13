using DevAutomation.Core.Options;
using DevAutomation.Infrastructure.DependencyInjection;
using DevAutomation.Infrastructure.Persistence;
using DevAutomation.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

var telemetryOptions = builder.Configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();
if (telemetryOptions.Enabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(telemetryOptions.ServiceName))
        .WithTracing(tracing =>
        {
            tracing
                .AddHttpClientInstrumentation()
                .AddSource(DevAutomationTelemetry.ActivitySourceName);

            if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
                    if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpHeaders)) options.Headers = telemetryOptions.OtlpHeaders;
                });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(DevAutomationTelemetry.MeterName);

            if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpEndpoint))
            {
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
                    if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpHeaders)) options.Headers = telemetryOptions.OtlpHeaders;
                });
            }
        });
}

var host = builder.Build();

if (builder.Configuration.GetValue("Database:ApplyMigrations", true))
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DevAutomationDbContext>();
    await dbContext.Database.MigrateAsync();
}

await host.RunAsync();
