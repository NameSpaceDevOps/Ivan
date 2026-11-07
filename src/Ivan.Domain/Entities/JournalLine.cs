using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class JournalLine : BaseTenantEntity
{
    public Guid EntryId { get; set; }
    public Guid AccountId { get; set; }
    public long Debit { get; set; }
    public long Credit { get; set; }
    public string Currency { get; set; } = "IRR";
    public decimal? ExchangeRate { get; set; }
    public Guid? PropertyId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? AssetId { get; set; }
    public string? Memo { get; set; }

    // Navigation
    public JournalEntry? Entry { get; set; }
    public Account? Account { get; set; }
    public Property? Property { get; set; }
    public Person? Person { get; set; }
    public Asset? Asset { get; set; }
}
