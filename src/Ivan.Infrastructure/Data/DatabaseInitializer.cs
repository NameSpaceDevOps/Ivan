using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static void InitializeDatabase(IvanDbContext db)
    {
        Directory.CreateDirectory("app_data");
        db.Database.EnsureCreated();

        // In SQLite, if the database file already existed from an earlier phase, EnsureCreated does nothing.
        // We generate create script and execute with IF NOT EXISTS so any missing tables (e.g. People, Tasks) are added.
        var script = db.Database.GenerateCreateScript();
        var statements = script
            .Replace("CREATE TABLE \"", "CREATE TABLE IF NOT EXISTS \"")
            .Replace("CREATE INDEX \"", "CREATE INDEX IF NOT EXISTS \"")
            .Replace("CREATE UNIQUE INDEX \"", "CREATE UNIQUE INDEX IF NOT EXISTS \"")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var stmt in statements)
        {
            if (!string.IsNullOrWhiteSpace(stmt))
            {
                try
                {
                    db.Database.ExecuteSqlRaw(stmt);
                }
                catch
                {
                    // Ignore already existing objects
                }
            }
        }

        db.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON; PRAGMA cache_size = -64000;");
    }
}
