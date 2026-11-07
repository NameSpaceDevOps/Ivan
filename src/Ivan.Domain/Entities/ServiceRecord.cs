using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class ServiceRecord : BaseTenantEntity
{
    public Guid AssetId { get; set; }
    public DateOnly Date { get; set; }
    public ServiceRecordType Type { get; set; } = ServiceRecordType.Routine;
    public Guid? ProviderContactId { get; set; }
    public long? Cost { get; set; }
    public string? Notes { get; set; }
    public Guid? JournalEntryId { get; set; }

    // Navigation
    public Asset? Asset { get; set; }
    public Contact? ProviderContact { get; set; }
}
