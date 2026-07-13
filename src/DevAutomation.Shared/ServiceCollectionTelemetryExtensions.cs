using DevAutomation.Core.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DevAutomation.Infrastructure.Telemetry;

public static class ServiceCollectionTelemetryExtensions
{
    public static IServiceCollection AddDevAutomationOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        var telemetryOptions = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();
        if (!telemetryOptions.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(telemetryOptions.ServiceName))
            .WithTracing(tracing =>
            {
                configureTracing?.Invoke(tracing);
                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource(DevAutomationTelemetry.ActivitySourceName);

                if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
                        if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpHeaders))
                        {
                            options.Headers = telemetryOptions.OtlpHeaders;
                        }
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                configureMetrics?.Invoke(metrics);
                metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(DevAutomationTelemetry.MeterName);

                if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
                        if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpHeaders))
                        {
                            options.Headers = telemetryOptions.OtlpHeaders;
                        }
                    });
                }
            });

        return services;
    }
}
