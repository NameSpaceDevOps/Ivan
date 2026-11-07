using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class JournalEntry : BaseTenantEntity
{
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Posted;
    public Guid? ReversalOfId { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }

    // Navigation
    public JournalEntry? ReversalOf { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}
