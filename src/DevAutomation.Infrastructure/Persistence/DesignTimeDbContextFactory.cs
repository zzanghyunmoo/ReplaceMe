using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevAutomation.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevAutomationDbContext>
{
    public DevAutomationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVAUTOMATION_ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=devautomation;Username=devautomation;Password=devautomation";
        var builder = new DbContextOptionsBuilder<DevAutomationDbContext>();
        builder.UseNpgsql(connectionString);
        return new DevAutomationDbContext(builder.Options);
    }
}
