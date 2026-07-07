namespace DevAutomation.Core.Options;

public sealed class ApprovalOptions
{
    public const string SectionName = "Approval";

    public TimeSpan ApprovalTimeout { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
}
