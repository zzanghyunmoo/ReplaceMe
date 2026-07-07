namespace DevAutomation.Core.Options;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool Enabled { get; set; }
    public string ServiceName { get; set; } = "DevAutomation";
    public string OtlpEndpoint { get; set; } = string.Empty;
    public string OtlpHeaders { get; set; } = string.Empty;
}
