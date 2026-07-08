using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using DevAutomation.Infrastructure.Agents;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness;

public sealed class ProfileReadinessService : IProfileReadinessService
{
    private readonly IEnumerable<IProfileReadinessCheck> _checks;
    private readonly IEnumerable<IReadinessReportPublisher> _publishers;
    private readonly SecretRedactor _redactor;
    private readonly ProfileReadinessOptions _options;

    public ProfileReadinessService(
        IEnumerable<IProfileReadinessCheck> checks,
        IEnumerable<IReadinessReportPublisher> publishers,
        SecretRedactor redactor,
        IOptions<ProfileReadinessOptions> options)
    {
        _checks = checks;
        _publishers = publishers;
        _redactor = redactor;
        _options = options.Value;
    }

    public async Task<ProfileReadinessReport> EvaluateAsync(string profileName, ProfileReadinessMode mode, CancellationToken cancellationToken)
    {
        var normalizedProfile = string.IsNullOrWhiteSpace(profileName) ? _options.SelectedProfile : profileName.Trim();
        if (!string.Equals(normalizedProfile, ProfileReadinessOptions.PersonalGitHubLinearNotionProfile, StringComparison.OrdinalIgnoreCase))
        {
            return ProfileReadinessReport.Create(
                normalizedProfile,
                mode,
                DateTimeOffset.UtcNow,
                [ProfileReadinessCheckResult.Failed("profile.identity", "Profile", ProfileReadinessSeverity.Required, $"Unknown readiness profile '{normalizedProfile}'.", $"Use '{ProfileReadinessOptions.PersonalGitHubLinearNotionProfile}'.")],
                []);
        }

        var context = new ProfileReadinessContext(normalizedProfile, mode);
        var checkResults = new List<ProfileReadinessCheckResult>();
        foreach (var check in _checks.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                checkResults.Add(Sanitize(await check.CheckAsync(context, cancellationToken)));
            }
            catch (Exception ex)
            {
                checkResults.Add(Sanitize(ProfileReadinessCheckResult.Failed(
                    check.Id,
                    "Readiness",
                    ProfileReadinessSeverity.Required,
                    $"Readiness check '{check.Id}' failed unexpectedly: {ex.Message}",
                    "Inspect server logs and fix the checker implementation or configuration.")));
            }
        }

        var shouldPublish = mode == ProfileReadinessMode.Doctor
            || (mode == ProfileReadinessMode.PreRunGate && checkResults.Any(x => x.BlocksRun));

        var reportBeforePublishing = ProfileReadinessReport.Create(normalizedProfile, mode, DateTimeOffset.UtcNow, checkResults, []);
        var surfaceResults = shouldPublish
            ? await PublishAsync(reportBeforePublishing, cancellationToken)
            : _publishers.Select(x => ProfileReadinessReportSurfaceResult.NotAttempted(x.Surface, PublisherSeverity(x.Surface), "Publishing was not attempted in this mode.")).ToArray();

        surfaceResults = surfaceResults.Select(Sanitize).ToArray();
        var finalReport = ProfileReadinessReport.Create(normalizedProfile, mode, DateTimeOffset.UtcNow, checkResults, surfaceResults);
        if (shouldPublish && surfaceResults.Any(x => x.Status != ProfileReadinessStatus.Passed))
        {
            await PublishFinalReportBestEffortAsync(finalReport, surfaceResults, cancellationToken);
        }

        return finalReport;
    }

    private async Task<IReadOnlyList<ProfileReadinessReportSurfaceResult>> PublishAsync(ProfileReadinessReport report, CancellationToken cancellationToken)
    {
        var results = new List<ProfileReadinessReportSurfaceResult>();
        foreach (var publisher in _publishers.OrderBy(x => x.Surface, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                results.Add(Sanitize(await publisher.PublishAsync(report, cancellationToken)));
            }
            catch (Exception ex)
            {
                results.Add(Sanitize(ProfileReadinessReportSurfaceResult.Failed(
                    publisher.Surface,
                    PublisherSeverity(publisher.Surface),
                    $"{publisher.Surface} readiness report publishing failed: {ex.Message}",
                    "Check provider credentials, target IDs, and network access.")));
            }
        }

        return results;
    }

    private async Task PublishFinalReportBestEffortAsync(ProfileReadinessReport finalReport, IReadOnlyList<ProfileReadinessReportSurfaceResult> firstPassResults, CancellationToken cancellationToken)
    {
        foreach (var publisher in _publishers.OrderBy(x => x.Surface, StringComparer.Ordinal))
        {
            var firstPass = firstPassResults.FirstOrDefault(x => x.Surface.Equals(publisher.Surface, StringComparison.OrdinalIgnoreCase));
            if (firstPass?.Status != ProfileReadinessStatus.Passed)
            {
                continue;
            }

            try
            {
                await publisher.PublishAsync(finalReport, cancellationToken);
            }
            catch
            {
                // The first pass already recorded whether this surface was reachable. The
                // second pass only tries to synchronize the final cross-surface result.
            }
        }
    }

    private ProfileReadinessCheckResult Sanitize(ProfileReadinessCheckResult result) => result with
    {
        Summary = _redactor.Redact(result.Summary),
        RepairHint = result.RepairHint is null ? null : _redactor.Redact(result.RepairHint)
    };

    private ProfileReadinessReportSurfaceResult Sanitize(ProfileReadinessReportSurfaceResult result) => result with
    {
        Summary = _redactor.Redact(result.Summary),
        RepairHint = result.RepairHint is null ? null : _redactor.Redact(result.RepairHint)
    };

    private ProfileReadinessSeverity PublisherSeverity(string surface)
    {
        return surface.Equals("Linear", StringComparison.OrdinalIgnoreCase)
            ? _options.Publishers.LinearSeverity
            : surface.Equals("Notion", StringComparison.OrdinalIgnoreCase)
                ? _options.Publishers.NotionSeverity
                : ProfileReadinessSeverity.Warning;
    }
}
