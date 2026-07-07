using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevAutomation.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevAutomationDbContext>
{
    public DevAutomationDbContext CreateDbContext(string[] args)
    {
        var connectionString = DatabaseConfiguration.GetPostgresConnectionStringFromEnvironment();
        var builder = new DbContextOptionsBuilder<DevAutomationDbContext>();
        builder.UseNpgsql(connectionString);
        return new DevAutomationDbContext(builder.Options);
    }
}
