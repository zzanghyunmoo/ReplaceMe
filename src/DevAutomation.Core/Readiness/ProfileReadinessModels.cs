namespace DevAutomation.Core.Readiness;

public enum ProfileReadinessMode
{
    Inspect = 0,
    Doctor = 1,
    PreRunGate = 2
}

public enum ProfileReadinessSeverity
{
    Required = 0,
    Warning = 1
}

public enum ProfileReadinessStatus
{
    Passed = 0,
    Warning = 1,
    Failed = 2,
    Skipped = 3,
    NotAttempted = 4
}

public sealed record ProfileReadinessCheckResult(
    string Id,
    string Surface,
    ProfileReadinessSeverity Severity,
    ProfileReadinessStatus Status,
    string Summary,
    string? RepairHint = null)
{
    public bool BlocksRun => Severity == ProfileReadinessSeverity.Required && Status == ProfileReadinessStatus.Failed;

    public static ProfileReadinessCheckResult Passed(string id, string surface, ProfileReadinessSeverity severity, string summary, string? repairHint = null)
        => new(id, surface, severity, ProfileReadinessStatus.Passed, summary, repairHint);

    public static ProfileReadinessCheckResult Failed(string id, string surface, ProfileReadinessSeverity severity, string summary, string? repairHint = null)
        => new(id, surface, severity, ProfileReadinessStatus.Failed, summary, repairHint);

    public static ProfileReadinessCheckResult Warning(string id, string surface, string summary, string? repairHint = null)
        => new(id, surface, ProfileReadinessSeverity.Warning, ProfileReadinessStatus.Warning, summary, repairHint);
}

public sealed record ProfileReadinessReportSurfaceResult(
    string Surface,
    ProfileReadinessSeverity Severity,
    ProfileReadinessStatus Status,
    string Summary,
    string? RepairHint = null)
{
    public bool BlocksRun => Severity == ProfileReadinessSeverity.Required && Status == ProfileReadinessStatus.Failed;

    public static ProfileReadinessReportSurfaceResult NotAttempted(string surface, ProfileReadinessSeverity severity, string summary)
        => new(surface, severity, ProfileReadinessStatus.NotAttempted, summary);

    public static ProfileReadinessReportSurfaceResult Passed(string surface, ProfileReadinessSeverity severity, string summary)
        => new(surface, severity, ProfileReadinessStatus.Passed, summary);

    public static ProfileReadinessReportSurfaceResult Failed(string surface, ProfileReadinessSeverity severity, string summary, string? repairHint = null)
        => new(surface, severity, ProfileReadinessStatus.Failed, summary, repairHint);
}

public sealed record ProfileReadinessReport(
    string ProfileName,
    ProfileReadinessMode Mode,
    DateTimeOffset GeneratedAt,
    bool IsRunnable,
    string Summary,
    IReadOnlyList<ProfileReadinessCheckResult> Checks,
    IReadOnlyList<ProfileReadinessReportSurfaceResult> ReportSurfaceResults)
{
    public static ProfileReadinessReport Create(
        string profileName,
        ProfileReadinessMode mode,
        DateTimeOffset generatedAt,
        IEnumerable<ProfileReadinessCheckResult> checks,
        IEnumerable<ProfileReadinessReportSurfaceResult> reportSurfaceResults)
    {
        var checkList = checks.ToArray();
        var surfaceList = reportSurfaceResults.ToArray();
        var blockers = checkList.Count(x => x.BlocksRun) + surfaceList.Count(x => x.BlocksRun);
        var warnings = checkList.Count(x => x.Status == ProfileReadinessStatus.Warning)
            + surfaceList.Count(x => x.Status == ProfileReadinessStatus.Warning);
        var isRunnable = blockers == 0;
        var summary = isRunnable
            ? warnings == 0 ? "Profile is runnable." : $"Profile is runnable with {warnings} warning(s)."
            : $"Profile is not runnable: {blockers} required check(s) failed.";

        return new ProfileReadinessReport(profileName, mode, generatedAt, isRunnable, summary, checkList, surfaceList);
    }
}

public sealed record ProfileReadinessContext(string ProfileName, ProfileReadinessMode Mode);

public interface IProfileReadinessCheck
{
    string Id { get; }

    Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken);
}

public interface IReadinessReportPublisher
{
    string Surface { get; }

    Task<ProfileReadinessReportSurfaceResult> PublishAsync(ProfileReadinessReport report, CancellationToken cancellationToken);
}

public interface IProfileReadinessService
{
    Task<ProfileReadinessReport> EvaluateAsync(string profileName, ProfileReadinessMode mode, CancellationToken cancellationToken);
}

public sealed record SecretCatalogEntry(string Label, string? Value);

public interface ISecretCatalog
{
    IReadOnlyList<SecretCatalogEntry> GetSecrets();
}
