namespace DevAutomation.Core.Options;

public sealed class LiteLlmOptions
{
    public const string SectionName = "LiteLLM";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string VirtualKey { get; set; } = string.Empty;
}
