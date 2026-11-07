using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class BackupRecord : BaseTenantEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset CreatedAtTime { get; set; } = DateTimeOffset.UtcNow;
    public bool IsVerified { get; set; }
    public string? Notes { get; set; }
}
