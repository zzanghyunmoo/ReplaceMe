using DevAutomation.Core.Readiness;

namespace DevAutomation.Core.Contracts.Readiness;

public sealed record ProfileReadinessReportResponse(
    string ProfileName,
    string Mode,
    DateTimeOffset GeneratedAt,
    bool IsRunnable,
    string Summary,
    IReadOnlyList<ProfileReadinessCheckResponse> Checks,
    IReadOnlyList<ProfileReadinessSurfaceResponse> ReportSurfaceResults)
{
    public static ProfileReadinessReportResponse From(ProfileReadinessReport report) => new(
        report.ProfileName,
        report.Mode.ToString(),
        report.GeneratedAt,
        report.IsRunnable,
        report.Summary,
        report.Checks.Select(ProfileReadinessCheckResponse.From).ToArray(),
        report.ReportSurfaceResults.Select(ProfileReadinessSurfaceResponse.From).ToArray());
}

public sealed record ProfileReadinessCheckResponse(
    string Id,
    string Surface,
    string Severity,
    string Status,
    string Summary,
    string? RepairHint)
{
    public static ProfileReadinessCheckResponse From(ProfileReadinessCheckResult result) => new(
        result.Id,
        result.Surface,
        result.Severity.ToString(),
        result.Status.ToString(),
        result.Summary,
        result.RepairHint);
}

public sealed record ProfileReadinessSurfaceResponse(
    string Surface,
    string Severity,
    string Status,
    string Summary,
    string? RepairHint)
{
    public static ProfileReadinessSurfaceResponse From(ProfileReadinessReportSurfaceResult result) => new(
        result.Surface,
        result.Severity.ToString(),
        result.Status.ToString(),
        result.Summary,
        result.RepairHint);
}
