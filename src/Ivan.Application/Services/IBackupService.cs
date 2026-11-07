namespace Ivan.Application.Services;

public record BackupResultDto(
    Guid Id,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    DateTimeOffset CreatedAt,
    bool Success,
    string? Message);

public record RestoreDryRunResultDto(
    bool IsValid,
    string DatabaseVersion,
    int TableCount,
    long TotalRows,
    IReadOnlyList<string> Tables,
    string? ErrorMessage);

public record BackupRecordDto(
    Guid Id,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    DateTimeOffset CreatedAtTime,
    bool IsVerified,
    string? Notes);

public interface IBackupService
{
    Task<BackupResultDto> CreateBackupAsync(string? targetDirectory = null, CancellationToken ct = default);
    Task<RestoreDryRunResultDto> DryRunRestoreAsync(string backupFilePath, CancellationToken ct = default);
    Task<byte[]> ExportHouseholdZipAsync(Guid householdId, CancellationToken ct = default);
    Task<IReadOnlyList<BackupRecordDto>> GetBackupRecordsAsync(CancellationToken ct = default);
}
