using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class ExamEvent : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public Guid SubjectId { get; set; }
    public DateOnly Date { get; set; }
    public string Topics { get; set; } = string.Empty;
    public decimal? Score { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public Subject? Subject { get; set; }
}
