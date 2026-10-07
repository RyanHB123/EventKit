using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace EventKit.Api.Data;

public static class DatabaseConnection
{
    public static string Parse(string value)
    {
        if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://")) return value;
        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2) throw new InvalidOperationException("DATABASE_URL must include a user and password.");
        return new NpgsqlConnectionStringBuilder {
            Host = uri.Host, Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]), Password = Uri.UnescapeDataString(credentials[1]),
            SslMode = SslMode.VerifyFull, Pooling = true, MaxPoolSize = 20, Timeout = 30
        }.ConnectionString;
    }
}

public static class LocalEnvironment
{
    public static void Load()
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development") return;
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, ".env.local");
            if (File.Exists(path))
            {
                foreach (var line in File.ReadLines(path))
                {
                    var text = line.Trim();
                    if (text.StartsWith('#') || !text.Contains('=')) continue;
                    var pair = text.Split('=', 2);
                    if (Environment.GetEnvironmentVariable(pair[0]) is null)
                        Environment.SetEnvironmentVariable(pair[0], pair[1].Trim().Trim('"', '\''));
                }
                return;
            }
            directory = directory.Parent;
        }
    }
}

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EventKitDbContext>
{
    public EventKitDbContext CreateDbContext(string[] args)
    {
        LocalEnvironment.Load();
        var connection = Environment.GetEnvironmentVariable("DATABASE_URL_UNPOOLED")
            ?? "Host=localhost;Database=eventkit;Username=postgres;Password=unused";
        return new EventKitDbContext(new DbContextOptionsBuilder<EventKitDbContext>().UseNpgsql(DatabaseConnection.Parse(connection)).Options);
    }
}
