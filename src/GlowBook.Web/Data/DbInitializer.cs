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

        await db.Database.MigrateAsync();

        var provider = DatabaseConnectionResolver.Resolve(config, dataDir).Provider;
        if (provider != DatabaseProviderKind.Postgres)
            return;

        var force = string.Equals(config["MIGRATE_FROM_SQLITE"], "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("MIGRATE_FROM_SQLITE"), "1", StringComparison.OrdinalIgnoreCase);

        var marker = Path.Combine(dataDir, ".migrated-from-sqlite");
        if (!force && File.Exists(marker))
            return;

        var sqlitePath = FindSqlite(dataDir, config);
        if (sqlitePath == null)
        {
            logger.LogWarning("Postgres ready, but no SQLite source found under {DataDir}", dataDir);
            return;
        }

        var alreadyHasUsers = await db.Users.AnyAsync();
        if (alreadyHasUsers && !force)
        {
            logger.LogInformation("Postgres already has users — skip SQLite import (set MIGRATE_FROM_SQLITE=1 to force)");
            await File.WriteAllTextAsync(marker, DateTime.UtcNow.ToString("O"));
            return;
        }

        logger.LogWarning("Importing SQLite → Postgres from {Path} (force={Force})", sqlitePath, force);
        var summary = await SqliteToPostgresMigrator.MigrateAsync(db, sqlitePath, logger);
        await File.WriteAllTextAsync(marker, summary + Environment.NewLine + DateTime.UtcNow.ToString("O"));
        logger.LogWarning("Import finished: {Summary}", summary);
    }

    private static string? FindSqlite(string dataDir, IConfiguration config)
    {
        var explicitPath = config["SQLITE_PATH"] ?? Environment.GetEnvironmentVariable("SQLITE_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        foreach (var candidate in new[]
                 {
                     Path.Combine(dataDir, "glowbook.db"),
                     Path.Combine("/data", "glowbook.db"),
                     Path.Combine(AppContext.BaseDirectory, "Data", "glowbook.db"),
                     Path.Combine(Directory.GetCurrentDirectory(), "Data", "glowbook.db")
                 })
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
