using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, string dataDir)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var config = sp.GetRequiredService<IConfiguration>();

        var provider = DatabaseConnectionResolver.Resolve(config, dataDir).Provider;

        if (provider == DatabaseProviderKind.Postgres)
        {
            // Schema only. SQLite→Postgres data import is tools/MigrateNow (not at app startup).
            await db.Database.MigrateAsync();
            logger.LogInformation("Postgres schema up to date (MigrateAsync)");
            return;
        }

        // Legacy local SQLite: create schema without applying Postgres migrations.
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("SQLite schema ensured at {DataDir}", dataDir);
    }
}
