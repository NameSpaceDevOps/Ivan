using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class HouseholdAiConfig : BaseTenantEntity
{
    public bool IsEnabled { get; set; } = false;
    public AiProviderType Provider { get; set; } = AiProviderType.Ollama;
    public string? Endpoint { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public string? ModelId { get; set; }
    public int MonthlyCapTokens { get; set; } = 100_000;
    public int UsedTokensThisMonth { get; set; } = 0;
    public DateOnly CurrentMonth { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}
