using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class AiUsageLog : BaseTenantEntity
{
    public AiRequestKind RequestKind { get; set; }
    public int TokensUsed { get; set; }
    public string? PromptSnippet { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
