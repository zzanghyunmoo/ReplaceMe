namespace DevAutomation.Core.Options;

public enum DocumentToolProvider
{
    None = 0,
    Notion = 1,
    Confluence = 2
}

public sealed class DocumentToolOptions
{
    public const string SectionName = "DocumentTool";

    public DocumentToolProvider Provider { get; set; } = DocumentToolProvider.None;
}

public sealed class NotionOptions
{
    public const string SectionName = "Notion";

    public string ApiBaseUrl { get; set; } = "https://api.notion.com/v1";
    public string ApiToken { get; set; } = string.Empty;
    public string ParentPageId { get; set; } = string.Empty;
    public string NotionVersion { get; set; } = "2022-06-28";
}

public sealed class ConfluenceOptions
{
    public const string SectionName = "Confluence";

    public string BaseUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string SpaceKey { get; set; } = string.Empty;
    public string? ParentPageId { get; set; }
}
