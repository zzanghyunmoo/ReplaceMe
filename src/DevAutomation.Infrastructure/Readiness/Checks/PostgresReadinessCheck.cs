using DevAutomation.Core.Readiness;
using DevAutomation.Infrastructure.Persistence;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class PostgresReadinessCheck : IProfileReadinessCheck
{
    private readonly DevAutomationDbContext _dbContext;

    public PostgresReadinessCheck(DevAutomationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Id => "local.postgres.connectivity";

    public async Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken)
                ? ProfileReadinessCheckResult.Passed(Id, "Local", ProfileReadinessSeverity.Required, "PostgreSQL is reachable.")
                : ProfileReadinessCheckResult.Failed(Id, "Local", ProfileReadinessSeverity.Required, "PostgreSQL is not reachable.", "Start PostgreSQL and verify ConnectionStrings:Postgres.");
        }
        catch (Exception ex)
        {
            return ProfileReadinessCheckResult.Failed(Id, "Local", ProfileReadinessSeverity.Required, $"PostgreSQL connectivity check failed: {ex.Message}", "Start PostgreSQL and verify ConnectionStrings:Postgres.");
        }
    }
}
