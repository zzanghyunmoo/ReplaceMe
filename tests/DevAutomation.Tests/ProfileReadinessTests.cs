using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using DevAutomation.Infrastructure.Agents;
using DevAutomation.Infrastructure.Readiness;
using Microsoft.Extensions.Options;

namespace DevAutomation.Tests;

public sealed class ProfileReadinessTests
{
    [Fact]
    public void Required_failure_makes_report_not_runnable()
    {
        var report = ProfileReadinessReport.Create(
            ProfileReadinessOptions.PersonalGitHubLinearNotionProfile,
            ProfileReadinessMode.Inspect,
            DateTimeOffset.Parse("2026-07-08T00:00:00Z"),
            [ProfileReadinessCheckResult.Failed("github.agent.gh.capability", "GitHub", ProfileReadinessSeverity.Required, "GitHub is not ready.")],
            []);

        Assert.False(report.IsRunnable);
        Assert.Contains("not runnable", report.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Warning_only_report_stays_runnable()
    {
        var report = ProfileReadinessReport.Create(
            ProfileReadinessOptions.PersonalGitHubLinearNotionProfile,
            ProfileReadinessMode.Inspect,
            DateTimeOffset.Parse("2026-07-08T00:00:00Z"),
            [ProfileReadinessCheckResult.Warning("secrets.redaction.coverage", "Safety", "One optional secret is not covered.")],
            []);

        Assert.True(report.IsRunnable);
        Assert.Contains("warning", report.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Inspect_mode_does_not_call_publishers()
    {
        var publisher = new FakePublisher(ProfileReadinessReportSurfaceResult.Passed("Linear", ProfileReadinessSeverity.Required, "Published."));
        var service = new ProfileReadinessService(
            [new FakeCheck(ProfileReadinessCheckResult.Passed("local.postgres.connectivity", "Local", ProfileReadinessSeverity.Required, "Postgres ready."))],
            [publisher],
            new SecretRedactor([]),
            Options.Create(new ProfileReadinessOptions()));

        var report = await service.EvaluateAsync(ProfileReadinessOptions.PersonalGitHubLinearNotionProfile, ProfileReadinessMode.Inspect, CancellationToken.None);

        Assert.True(report.IsRunnable);
        Assert.False(publisher.Called);
        Assert.Single(report.ReportSurfaceResults);
        Assert.Equal(ProfileReadinessStatus.NotAttempted, report.ReportSurfaceResults[0].Status);
    }

    [Fact]
    public async Task Doctor_mode_does_not_republish_when_all_surfaces_succeed()
    {
        var publisher = new FakePublisher(ProfileReadinessReportSurfaceResult.Passed("Linear", ProfileReadinessSeverity.Required, "Published."));
        var service = new ProfileReadinessService(
            [new FakeCheck(ProfileReadinessCheckResult.Passed("local.postgres.connectivity", "Local", ProfileReadinessSeverity.Required, "Postgres ready."))],
            [publisher],
            new SecretRedactor([]),
            Options.Create(new ProfileReadinessOptions()));

        var report = await service.EvaluateAsync(ProfileReadinessOptions.PersonalGitHubLinearNotionProfile, ProfileReadinessMode.Doctor, CancellationToken.None);

        Assert.True(report.IsRunnable);
        Assert.Equal(1, publisher.CallCount);
    }

    [Fact]
    public async Task Doctor_mode_republishes_successful_surfaces_when_another_surface_fails()
    {
        var successfulPublisher = new FakePublisher(ProfileReadinessReportSurfaceResult.Passed("Notion", ProfileReadinessSeverity.Required, "Published."));
        var failingPublisher = new FakePublisher(ProfileReadinessReportSurfaceResult.Failed("Linear", ProfileReadinessSeverity.Required, "Publish failed."));
        var service = new ProfileReadinessService(
            [new FakeCheck(ProfileReadinessCheckResult.Passed("local.postgres.connectivity", "Local", ProfileReadinessSeverity.Required, "Postgres ready."))],
            [successfulPublisher, failingPublisher],
            new SecretRedactor([]),
            Options.Create(new ProfileReadinessOptions()));

        var report = await service.EvaluateAsync(ProfileReadinessOptions.PersonalGitHubLinearNotionProfile, ProfileReadinessMode.Doctor, CancellationToken.None);

        Assert.False(report.IsRunnable);
        Assert.Equal(2, successfulPublisher.CallCount);
        Assert.Equal(1, failingPublisher.CallCount);
    }

    [Fact]
    public async Task Doctor_mode_includes_required_publisher_failure_in_runnable_result()
    {
        var service = new ProfileReadinessService(
            [new FakeCheck(ProfileReadinessCheckResult.Passed("local.postgres.connectivity", "Local", ProfileReadinessSeverity.Required, "Postgres ready."))],
            [new FakePublisher(ProfileReadinessReportSurfaceResult.Failed("Linear", ProfileReadinessSeverity.Required, "Publish failed."))],
            new SecretRedactor([]),
            Options.Create(new ProfileReadinessOptions()));

        var report = await service.EvaluateAsync(ProfileReadinessOptions.PersonalGitHubLinearNotionProfile, ProfileReadinessMode.Doctor, CancellationToken.None);

        Assert.False(report.IsRunnable);
        Assert.Single(report.ReportSurfaceResults);
        Assert.Equal(ProfileReadinessStatus.Failed, report.ReportSurfaceResults[0].Status);
    }

    [Fact]
    public async Task Readiness_service_redacts_configured_secrets_from_reports()
    {
        const string secret = "linear-secret-token";
        var service = new ProfileReadinessService(
            [new FakeCheck(ProfileReadinessCheckResult.Failed("linear.read.access", "Linear", ProfileReadinessSeverity.Required, $"Linear failed with {secret}."))],
            [],
            new SecretRedactor([secret]),
            Options.Create(new ProfileReadinessOptions()));

        var report = await service.EvaluateAsync(ProfileReadinessOptions.PersonalGitHubLinearNotionProfile, ProfileReadinessMode.Inspect, CancellationToken.None);

        Assert.DoesNotContain(secret, report.Checks[0].Summary, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", report.Checks[0].Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_redactor_reports_coverage_without_exposing_values()
    {
        const string secret = "super-secret-token";
        var redactor = new SecretRedactor([secret]);

        Assert.True(redactor.Covers(secret));
        Assert.Equal("token=[REDACTED]", redactor.Redact($"token={secret}"));
    }

    private sealed class FakeCheck : IProfileReadinessCheck
    {
        private readonly ProfileReadinessCheckResult _result;

        public FakeCheck(ProfileReadinessCheckResult result)
        {
            _result = result;
        }

        public string Id => _result.Id;

        public Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken) => Task.FromResult(_result);
    }

    private sealed class FakePublisher : IReadinessReportPublisher
    {
        private readonly ProfileReadinessReportSurfaceResult _result;

        public FakePublisher(ProfileReadinessReportSurfaceResult result)
        {
            _result = result;
        }

        public string Surface => _result.Surface;
        public bool Called { get; private set; }
        public int CallCount { get; private set; }

        public Task<ProfileReadinessReportSurfaceResult> PublishAsync(ProfileReadinessReport report, CancellationToken cancellationToken)
        {
            Called = true;
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}
