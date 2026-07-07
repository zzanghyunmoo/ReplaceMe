using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DevAutomation.Infrastructure.Persistence;

public static class DatabaseConfiguration
{
    public static string GetPostgresConnectionString(IConfiguration configuration)
    {
        return configuration.GetConnectionString("Postgres")
            ?? "Host=postgres;Port=5432;Database=devautomation;Username=devautomation;Password=devautomation";
    }

    public static string GetPostgresConnectionStringFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("DEVAUTOMATION_ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=devautomation;Username=devautomation;Password=devautomation";
    }

    public static void UsePostgresDatabase(this DbContextOptionsBuilder options, IConfiguration configuration)
    {
        options.UseNpgsql(GetPostgresConnectionString(configuration));
    }
}
