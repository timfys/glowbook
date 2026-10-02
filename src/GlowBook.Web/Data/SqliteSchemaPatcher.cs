using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Data;

public static class SqliteSchemaPatcher
{
    public static async Task ApplyAsync(ApplicationDbContext db, ILogger logger)
    {
        await EnsureColumnAsync(db, "Services", "Color", "Color TEXT");
        await EnsureColumnAsync(db, "MasterProfiles", "PageAccentColor", "PageAccentColor TEXT");
        await EnsureColumnAsync(db, "MasterProfiles", "ShowOnMap", "ShowOnMap INTEGER NOT NULL DEFAULT 1");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MasterPortfolioPhotos" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MasterPortfolioPhotos" PRIMARY KEY AUTOINCREMENT,
                "MasterProfileId" INTEGER NOT NULL,
                "Data" BLOB NOT NULL,
                "ContentType" TEXT NOT NULL DEFAULT 'image/jpeg',
                "Caption" TEXT NULL,
                "SortOrder" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_MasterPortfolioPhotos_MasterProfiles_MasterProfileId"
                    FOREIGN KEY ("MasterProfileId") REFERENCES "MasterProfiles" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_MasterPortfolioPhotos_MasterProfileId_SortOrder"
            ON "MasterPortfolioPhotos" ("MasterProfileId", "SortOrder");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MasterPromos" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MasterPromos" PRIMARY KEY AUTOINCREMENT,
                "MasterProfileId" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Description" TEXT NULL,
                "Badge" TEXT NULL,
                "ValidUntil" TEXT NULL,
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_MasterPromos_MasterProfiles_MasterProfileId"
                    FOREIGN KEY ("MasterProfileId") REFERENCES "MasterProfiles" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_MasterPromos_MasterProfileId_IsActive"
            ON "MasterPromos" ("MasterProfileId", "IsActive");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MasterReviews" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MasterReviews" PRIMARY KEY AUTOINCREMENT,
                "MasterProfileId" INTEGER NOT NULL,
                "ClientId" INTEGER NULL,
                "AuthorName" TEXT NOT NULL,
                "AuthorPhone" TEXT NOT NULL,
                "Rating" INTEGER NOT NULL DEFAULT 5,
                "Text" TEXT NOT NULL,
                "IsPublished" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_MasterReviews_MasterProfiles_MasterProfileId"
                    FOREIGN KEY ("MasterProfileId") REFERENCES "MasterProfiles" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_MasterReviews_Clients_ClientId"
                    FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE SET NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_MasterReviews_MasterProfileId_IsPublished_CreatedAt"
            ON "MasterReviews" ("MasterProfileId", "IsPublished", "CreatedAt");
            """);

        logger.LogInformation("SQLite mini-site schema patch applied");
    }

    private static async Task EnsureColumnAsync(ApplicationDbContext db, string table, string column, string definition)
    {
        var exists = false;
        var conn = db.Database.GetDbConnection();
        var shouldClose = conn.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync();

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }
        finally
        {
            if (shouldClose)
                await conn.CloseAsync();
        }

        if (!exists)
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN {definition}");
    }
}
