using System.Diagnostics;
using GlowBook.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

// One-shot local migrator:
 //   dotnet run --project tools/MigrateNow -- <sqlitePath>
 // Uses Railway Postgres creds the user provided.

var sqlitePath = args.ElementAtOrDefault(0)
    ?? @"C:\Users\timofey\RiderProjects\glowbook_git\src\GlowBook.Web\Data\glowbook.db";

var host = Environment.GetEnvironmentVariable("PGHOST") ?? "postgres.railway.internal";
var port = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
var database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "railway";
var user = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
var password = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "ayNEAQtGCFvMSeeNIHbtQpUcyVoqwQnG";

var cs = new NpgsqlConnectionStringBuilder
{
    Host = host,
    Port = int.Parse(port),
    Database = database,
    Username = user,
    Password = password,
    SslMode = host.Contains("rlwy.net", StringComparison.OrdinalIgnoreCase)
        || host.Contains("railway.app", StringComparison.OrdinalIgnoreCase)
        ? SslMode.Require
        : SslMode.Prefer,
    Timeout = 30,
    CommandTimeout = 120
}.ConnectionString;

Console.WriteLine($"SQLite: {sqlitePath}");
Console.WriteLine($"Postgres: Host={host};Port={port};Database={database};Username={user}");

await using (var test = new NpgsqlConnection(cs))
{
    try
    {
        await test.OpenAsync();
        Console.WriteLine("Connected to Postgres.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("CANNOT CONNECT from this PC to Host=" + host);
        Console.Error.WriteLine(ex.Message);
        Console.Error.WriteLine();
        Console.Error.WriteLine("postgres.railway.internal works ONLY inside Railway.");
        Console.Error.WriteLine("Migration will run on the glowbook service at deploy (uses your DATABASE_URL).");
        return 2;
    }
}

var pgOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(cs)
    .Options;

await using var pg = new ApplicationDbContext(pgOptions);
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var logger = loggerFactory.CreateLogger("MigrateNow");

var summary = await SqliteToPostgresMigrator.MigrateAsync(pg, sqlitePath, logger);
Console.WriteLine(summary);
return 0;
