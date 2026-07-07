namespace DevAutomation.Core.Options;

public sealed class SlackOptions
{
    public const string SectionName = "Slack";

    public string BotToken { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://slack.com/api/";
}
