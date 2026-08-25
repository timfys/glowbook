using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GlowBook.Web.Data;

public static class SqliteToPostgresMigrator
{
    public static async Task<string> MigrateAsync(
        ApplicationDbContext postgres,
        string sqlitePath,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (!File.Exists(sqlitePath))
            throw new FileNotFoundException("SQLite file not found", sqlitePath);

        await using (var probe = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sqlitePath}"))
        {
            await probe.OpenAsync(ct);
            await using var checkpoint = probe.CreateCommand();
            checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
            await checkpoint.ExecuteNonQueryAsync(ct);
        }

        var sqliteOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={sqlitePath}")
            .Options;

        await using var sqlite = new ApplicationDbContext(sqliteOptions);
        logger.LogInformation("SQLite source {Path}: users={Users}", sqlitePath, await sqlite.Users.CountAsync(ct));

        // Caller (MigrateNow) already applied Postgres migrations.
        if (!await postgres.Database.CanConnectAsync(ct))
            throw new InvalidOperationException("Cannot connect to Postgres");

        await using var tx = await postgres.Database.BeginTransactionAsync(ct);
        await postgres.Database.ExecuteSqlRawAsync("SET session_replication_role = 'replica';", ct);

        try
        {
            await ClearPostgresAsync(postgres, ct);

            postgres.Roles.AddRange(await sqlite.Roles.AsNoTracking().ToListAsync(ct));
            postgres.Users.AddRange(await sqlite.Users.AsNoTracking().ToListAsync(ct));
            await postgres.SaveChangesAsync(ct);

            postgres.UserRoles.AddRange(await sqlite.UserRoles.AsNoTracking().ToListAsync(ct));
            postgres.UserClaims.AddRange(await sqlite.UserClaims.AsNoTracking().ToListAsync(ct));
            postgres.RoleClaims.AddRange(await sqlite.RoleClaims.AsNoTracking().ToListAsync(ct));
            postgres.UserLogins.AddRange(await sqlite.UserLogins.AsNoTracking().ToListAsync(ct));
            postgres.UserTokens.AddRange(await sqlite.UserTokens.AsNoTracking().ToListAsync(ct));
            await postgres.SaveChangesAsync(ct);

            await AddAndSave(postgres, await sqlite.MasterProfiles.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.MasterAvatars.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.Clients.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.Services.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.WorkingHours.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.Subscriptions.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.Appointments.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.PaymentOrders.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.TreatmentRecords.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.ClientPhotos.AsNoTracking().ToListAsync(ct), ct);
            await AddAndSave(postgres, await sqlite.HomeCarePrescriptions.AsNoTracking().ToListAsync(ct), ct);

            await postgres.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';", ct);
            await ResetSequencesAsync(postgres, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await postgres.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';", ct); } catch { /* ignore */ }
            await tx.RollbackAsync(ct);
            throw;
        }

        var summary =
            $"OK SQLite→Postgres users={await postgres.Users.CountAsync(ct)} " +
            $"profiles={await postgres.MasterProfiles.CountAsync(ct)} " +
            $"clients={await postgres.Clients.CountAsync(ct)} " +
            $"appointments={await postgres.Appointments.CountAsync(ct)} " +
            $"services={await postgres.Services.CountAsync(ct)}";
        logger.LogInformation("{Summary}", summary);
        return summary;
    }

    private static async Task AddAndSave<T>(ApplicationDbContext db, List<T> rows, CancellationToken ct) where T : class
    {
        if (rows.Count == 0) return;
        db.Set<T>().AddRange(rows);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ClearPostgresAsync(ApplicationDbContext db, CancellationToken ct)
    {
        db.HomeCarePrescriptions.RemoveRange(await db.HomeCarePrescriptions.ToListAsync(ct));
        db.ClientPhotos.RemoveRange(await db.ClientPhotos.ToListAsync(ct));
        db.TreatmentRecords.RemoveRange(await db.TreatmentRecords.ToListAsync(ct));
        db.Appointments.RemoveRange(await db.Appointments.ToListAsync(ct));
        db.PaymentOrders.RemoveRange(await db.PaymentOrders.ToListAsync(ct));
        db.Subscriptions.RemoveRange(await db.Subscriptions.ToListAsync(ct));
        db.WorkingHours.RemoveRange(await db.WorkingHours.ToListAsync(ct));
        db.Services.RemoveRange(await db.Services.ToListAsync(ct));
        db.Clients.RemoveRange(await db.Clients.ToListAsync(ct));
        db.MasterAvatars.RemoveRange(await db.MasterAvatars.ToListAsync(ct));
        db.MasterProfiles.RemoveRange(await db.MasterProfiles.ToListAsync(ct));
        db.UserTokens.RemoveRange(await db.UserTokens.ToListAsync(ct));
        db.UserLogins.RemoveRange(await db.UserLogins.ToListAsync(ct));
        db.UserClaims.RemoveRange(await db.UserClaims.ToListAsync(ct));
        db.UserRoles.RemoveRange(await db.UserRoles.ToListAsync(ct));
        db.RoleClaims.RemoveRange(await db.RoleClaims.ToListAsync(ct));
        db.Users.RemoveRange(await db.Users.ToListAsync(ct));
        db.Roles.RemoveRange(await db.Roles.ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }

    private static async Task ResetSequencesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        foreach (var table in new[]
                 {
                     "MasterProfiles", "Clients", "Services", "WorkingHours", "Appointments",
                     "Subscriptions", "PaymentOrders", "TreatmentRecords", "ClientPhotos",
                     "HomeCarePrescriptions", "AspNetUserClaims", "AspNetRoleClaims"
                 })
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"""SELECT setval(pg_get_serial_sequence('"{table}"', 'Id'), COALESCE((SELECT MAX("Id") FROM "{table}"), 1), true);""",
                    ct);
            }
            catch
            {
                // ignore missing sequences
            }
        }
    }
}
