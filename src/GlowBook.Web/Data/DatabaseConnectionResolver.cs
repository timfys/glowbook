using Npgsql;

namespace GlowBook.Web.Data;

public enum DatabaseProviderKind
{
    Sqlite,
    Postgres
}

public sealed record DatabaseConnectionInfo(DatabaseProviderKind Provider, string ConnectionString);

public static class DatabaseConnectionResolver
{
    public static DatabaseConnectionInfo Resolve(IConfiguration configuration, string dataDir)
    {
        var configured = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_PRIVATE_URL");

        if (LooksLikePostgres(configured))
            return new DatabaseConnectionInfo(DatabaseProviderKind.Postgres, ToNpgsqlConnectionString(configured!));

        if (TryBuildPostgresFromParts(configuration, out var fromParts))
            return new DatabaseConnectionInfo(DatabaseProviderKind.Postgres, fromParts);

        var dbPath = Path.Combine(dataDir, "glowbook.db");
        if (string.IsNullOrWhiteSpace(configured)
            || configured.Contains("Data/glowbook.db", StringComparison.OrdinalIgnoreCase)
            || configured.Contains(@"Data\glowbook.db", StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseConnectionInfo(DatabaseProviderKind.Sqlite, $"Data Source={dbPath}");
        }

        return new DatabaseConnectionInfo(DatabaseProviderKind.Sqlite, configured);
    }

    public static string Redact(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        try
        {
            if (LooksLikePostgresUri(connectionString))
            {
                var uri = new Uri(connectionString.Replace("postgres://", "postgresql://", StringComparison.OrdinalIgnoreCase));
                var user = uri.UserInfo.Split(':')[0];
                return $"postgresql://{user}:***@{uri.Host}:{uri.Port}{uri.AbsolutePath}";
            }

            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
                builder.Password = "***";
            return builder.ConnectionString;
        }
        catch
        {
            return "***";
        }
    }

    private static bool TryBuildPostgresFromParts(IConfiguration configuration, out string connectionString)
    {
        connectionString = string.Empty;
        var host = configuration["PGHOST"]
            ?? configuration["RailwayPostgres:PGHOST"]
            ?? Environment.GetEnvironmentVariable("PGHOST");
        var user = configuration["PGUSER"]
            ?? configuration["RailwayPostgres:PGUSER"]
            ?? configuration["RailwayPostgres:POSTGRES_USER"]
            ?? Environment.GetEnvironmentVariable("PGUSER");
        var password = configuration["PGPASSWORD"]
            ?? configuration["RailwayPostgres:PGPASSWORD"]
            ?? configuration["RailwayPostgres:POSTGRES_PASSWORD"]
            ?? Environment.GetEnvironmentVariable("PGPASSWORD");
        var database = configuration["PGDATABASE"]
            ?? configuration["RailwayPostgres:PGDATABASE"]
            ?? configuration["RailwayPostgres:POSTGRES_DB"]
            ?? Environment.GetEnvironmentVariable("PGDATABASE");
        var port = configuration["PGPORT"]
            ?? configuration["RailwayPostgres:PGPORT"]
            ?? Environment.GetEnvironmentVariable("PGPORT")
            ?? "5432";

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(user)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(database))
            return false;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.TryParse(port, out var p) ? p : 5432,
            Database = database,
            Username = user,
            Password = password,
            SslMode = SslMode.Prefer,
            TrustServerCertificate = true
        };
        connectionString = builder.ConnectionString;
        return true;
    }

    private static bool LooksLikePostgres(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (LooksLikePostgresUri(value))
            return true;

        return value.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePostgresUri(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static string ToNpgsqlConnectionString(string value)
    {
        if (!LooksLikePostgresUri(value))
            return value;

        var normalized = value.Replace("postgres://", "postgresql://", StringComparison.OrdinalIgnoreCase);
        var uri = new Uri(normalized);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = string.IsNullOrWhiteSpace(database) ? "railway" : database,
            Username = username,
            Password = password,
            SslMode = SslMode.Prefer,
            TrustServerCertificate = true
        };

        // Public Railway proxy usually needs SSL; private network is fine with Prefer.
        if (uri.Host.Contains("railway.app", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Contains("rlwy.net", StringComparison.OrdinalIgnoreCase))
        {
            builder.SslMode = SslMode.Require;
        }

        return builder.ConnectionString;
    }
}
