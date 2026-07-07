namespace DevAutomation.Core.Options;

public enum NotifierProvider
{
    None = 0,
    Slack = 1,
    Gmail = 2
}

public sealed class NotifierOptions
{
    public const string SectionName = "Notifier";

    public NotifierProvider Provider { get; set; } = NotifierProvider.Slack;
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";
}

public sealed class GmailOptions
{
    public const string SectionName = "Gmail";

    public string ApiBaseUrl { get; set; } = "https://gmail.googleapis.com/gmail/v1";
    public string UserId { get; set; } = "me";
    public string AccessToken { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string SubjectPrefix { get; set; } = "[DevAutomation]";
}
