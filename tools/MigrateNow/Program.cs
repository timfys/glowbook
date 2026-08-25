using GlowBook.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// One-shot: schema + SQLite→Postgres into tunnel/local Postgres.
//
// Prerequisites: railway connect Postgres --tunnel-only -P 5432
//
//   dotnet run --project tools/MigrateNow -- [sqlitePath] [--wipe]
//
// --wipe drops public schema first (destructive). Env: PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD

var wipe = args.Any(a => string.Equals(a, "--wipe", StringComparison.OrdinalIgnoreCase));
var sqlitePath = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
    ?? Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "GlowBook.Web", "Data", "glowbook.db"));

if (!File.Exists(sqlitePath))
{
    sqlitePath = @"C:\Users\timofey\RiderProjects\glowbook_git\src\GlowBook.Web\Data\glowbook.db";
}

var host = Environment.GetEnvironmentVariable("PGHOST") ?? "127.0.0.1";
var port = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
var database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "railway";
var user = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
var password = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "ayNEAQtGCFvMSeeNIHbtQpUcyVoqwQnG";

var useSsl = host.Contains("rlwy.net", StringComparison.OrdinalIgnoreCase)
    || host.Contains("railway.app", StringComparison.OrdinalIgnoreCase);

var cs = new NpgsqlConnectionStringBuilder
{
    Host = host,
    Port = int.Parse(port),
    Database = database,
    Username = user,
    Password = password,
    SslMode = useSsl ? SslMode.Require : SslMode.Disable,
    Timeout = 30,
    CommandTimeout = 300
}.ConnectionString;

Console.WriteLine($"SQLite:   {sqlitePath}");
Console.WriteLine($"Postgres: Host={host};Port={port};Database={database};Username={user};SSL={(useSsl ? "Require" : "Disable")}");

await using (var test = new NpgsqlConnection(cs))
{
    try
    {
        await test.OpenAsync();
        Console.WriteLine("Connected to Postgres.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("CANNOT CONNECT to Postgres.");
        Console.Error.WriteLine(ex.Message);
        Console.Error.WriteLine();
        Console.Error.WriteLine("Keep tunnel open in another window:");
        Console.Error.WriteLine("  railway connect Postgres --tunnel-only -P 5432");
        return 2;
    }
}

if (wipe)
{
    Console.WriteLine("Wiping public schema (--wipe)...");
    await using var wipeConn = new NpgsqlConnection(cs);
    await wipeConn.OpenAsync();
    await using var cmd = wipeConn.CreateCommand();
    cmd.CommandText = """
        DROP SCHEMA IF EXISTS public CASCADE;
        CREATE SCHEMA public;
        GRANT ALL ON SCHEMA public TO postgres;
        GRANT ALL ON SCHEMA public TO public;
        """;
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("Schema wiped.");
}

var pgOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(cs)
    .Options;

await using var pg = new ApplicationDbContext(pgOptions);
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("MigrateNow");

Console.WriteLine("Applying EF migrations...");
await pg.Database.MigrateAsync();
Console.WriteLine("Schema OK.");

if (!File.Exists(sqlitePath))
{
    Console.WriteLine($"No SQLite file at {sqlitePath} — schema only, skip data import.");
    return 0;
}

Console.WriteLine("Importing SQLite → Postgres...");
var summary = await SqliteToPostgresMigrator.MigrateAsync(pg, sqlitePath, logger);
Console.WriteLine(summary);
return 0;
