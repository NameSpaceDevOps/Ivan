using System.IO.Compression;
using System.Text.Json;
using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ivan.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IConfiguration _config;
    private readonly IAuditService _auditService;

    public BackupService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IConfiguration config,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _config = config;
        _auditService = auditService;
    }

    public async Task<BackupResultDto> CreateBackupAsync(string? targetDirectory = null, CancellationToken ct = default)
    {
        var backupDir = targetDirectory ?? Path.Combine(AppContext.BaseDirectory, "app_data", "backups");
        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        var fileName = $"ivan_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.db";
        var backupPath = Path.Combine(backupDir, fileName);

        // Get SQLite connection
        var db = (DbContext)_db;
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        // Run VACUUM INTO to create an atomic snapshot
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}';";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var fileInfo = new FileInfo(backupPath);
        var fileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;

        var record = new BackupRecord
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            FileName = fileName,
            FilePath = backupPath,
            FileSizeBytes = fileSizeBytes,
            CreatedAtTime = DateTimeOffset.UtcNow,
            IsVerified = true,
            Notes = "Manual or automated online SQLite backup."
        };

        _db.BackupRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateBackup", "BackupRecord", record.Id.ToString(), $"Created backup {fileName} ({fileSizeBytes:N0} bytes)", ct);

        // Retention: keep last 10 backups
        try
        {
            var oldBackups = await _db.BackupRecords
                .OrderByDescending(b => b.CreatedAtTime)
                .Skip(10)
                .ToListAsync(ct);

            foreach (var old in oldBackups)
            {
                if (File.Exists(old.FilePath))
                {
                    File.Delete(old.FilePath);
                }
                _db.BackupRecords.Remove(old);
            }
            if (oldBackups.Any())
            {
                await _db.SaveChangesAsync(ct);
            }
        }
        catch
        {
            // Ignore retention cleanup errors
        }

        return new BackupResultDto(
            record.Id,
            fileName,
            backupPath,
            fileSizeBytes,
            record.CreatedAtTime,
            true,
            "Backup created successfully.");
    }

    public async Task<RestoreDryRunResultDto> DryRunRestoreAsync(string backupFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
        {
            return new RestoreDryRunResultDto(false, "Unknown", 0, 0, Array.Empty<string>(), $"Backup file not found at: {backupFilePath}");
        }

        try
        {
            var connStr = $"Data Source={backupFilePath};Mode=ReadOnly;";
            using var conn = new SqliteConnection(connStr);
            await conn.OpenAsync(ct);

            // 1. Check integrity
            using var cmdIntegrity = conn.CreateCommand();
            cmdIntegrity.CommandText = "PRAGMA integrity_check;";
            var integrityResult = (await cmdIntegrity.ExecuteScalarAsync(ct))?.ToString();

            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new RestoreDryRunResultDto(false, "Unknown", 0, 0, Array.Empty<string>(), $"Integrity check failed: {integrityResult}");
            }

            // 2. Query SQLite version
            using var cmdVersion = conn.CreateCommand();
            cmdVersion.CommandText = "SELECT sqlite_version();";
            var version = (await cmdVersion.ExecuteScalarAsync(ct))?.ToString() ?? "3.x";

            // 3. Query tables
            using var cmdTables = conn.CreateCommand();
            cmdTables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            var tables = new List<string>();
            using (var reader = await cmdTables.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    tables.Add(reader.GetString(0));
                }
            }

            // 4. Sample row count
            long totalRows = 0;
            foreach (var t in tables)
            {
                using var cmdCount = conn.CreateCommand();
                cmdCount.CommandText = $"SELECT count(*) FROM \"{t}\";";
                var c = (long)(await cmdCount.ExecuteScalarAsync(ct) ?? 0L);
                totalRows += c;
            }

            return new RestoreDryRunResultDto(true, version, tables.Count, totalRows, tables, null);
        }
        catch (Exception ex)
        {
            return new RestoreDryRunResultDto(false, "Error", 0, 0, Array.Empty<string>(), ex.Message);
        }
    }

    public async Task<byte[]> ExportHouseholdZipAsync(Guid householdId, CancellationToken ct = default)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var options = new JsonSerializerOptions { WriteIndented = true };

            // Household info
            var household = await _db.Households.FindAsync([householdId], ct);
            AddZipEntry(archive, "household.json", JsonSerializer.Serialize(household, options));

            // People
            var people = await _db.People.Where(p => p.HouseholdId == householdId).ToListAsync(ct);
            AddZipEntry(archive, "people.json", JsonSerializer.Serialize(people, options));

            // Properties & Spaces
            var properties = await _db.Properties.Include(p => p.Spaces).Where(p => p.HouseholdId == householdId).ToListAsync(ct);
            AddZipEntry(archive, "properties.json", JsonSerializer.Serialize(properties, options));

            // Assets
            var assets = await _db.Assets.Where(a => a.HouseholdId == householdId).ToListAsync(ct);
            AddZipEntry(archive, "assets.json", JsonSerializer.Serialize(assets, options));

            // Tasks
            var tasks = await _db.TaskItems.Where(t => t.HouseholdId == householdId).ToListAsync(ct);
            AddZipEntry(archive, "tasks.json", JsonSerializer.Serialize(tasks, options));

            // Books
            var books = await _db.Books.Where(b => b.HouseholdId == householdId).ToListAsync(ct);
            AddZipEntry(archive, "books.json", JsonSerializer.Serialize(books, options));

            // Media
            var media = await _db.MediaItems.Where(m => m.HouseholdId == householdId).ToListAsync(ct);
            AddZipEntry(archive, "media.json", JsonSerializer.Serialize(media, options));

            // Journal Entries
            var entries = await _db.JournalEntries.Include(j => j.Lines).Where(j => j.HouseholdId == householdId).ToListAsync(ct);
            AddZipEntry(archive, "journal_entries.json", JsonSerializer.Serialize(entries, options));
        }

        return memoryStream.ToArray();
    }

    public async Task<IReadOnlyList<BackupRecordDto>> GetBackupRecordsAsync(CancellationToken ct = default)
    {
        var records = await _db.BackupRecords.ToListAsync(ct);
        return records.OrderByDescending(r => r.CreatedAtTime).Select(r => new BackupRecordDto(
            r.Id,
            r.FileName,
            r.FilePath,
            r.FileSizeBytes,
            r.CreatedAtTime,
            r.IsVerified,
            r.Notes)).ToList();
    }

    private static void AddZipEntry(ZipArchive archive, string entryName, string jsonContent)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(jsonContent);
    }
}
