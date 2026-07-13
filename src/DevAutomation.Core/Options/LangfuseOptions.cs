namespace DevAutomation.Core.Options;

public sealed class LangfuseOptions
{
    public const string SectionName = "Langfuse";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}
