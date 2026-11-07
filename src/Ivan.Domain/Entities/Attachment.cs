using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Attachment : BaseTenantEntity
{
    public string OwnerType { get; set; } = string.Empty; // e.g. "Asset", "Expense", "Health", "Property", "Book"
    public Guid OwnerId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty; // Deduplication hash (Section 3.6)
    public string StoragePath { get; set; } = string.Empty;
    public ResourceVisibility Visibility { get; set; } = ResourceVisibility.Household;
    public Guid UploadedBy { get; set; }
}
