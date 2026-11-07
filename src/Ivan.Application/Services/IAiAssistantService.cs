using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record HouseholdAiConfigDto(
    bool IsEnabled,
    AiProviderType Provider,
    string? Endpoint,
    string? ModelId,
    int MonthlyCapTokens,
    int UsedTokensThisMonth,
    double UsagePercent);

public record UpdateAiConfigRequest(
    bool IsEnabled,
    AiProviderType Provider,
    string? Endpoint = null,
    string? ApiKey = null,
    string? ModelId = null,
    int MonthlyCapTokens = 100_000);

public record ParsedQuickAddDto(
    string IntentType, // "Task", "Expense", "CalendarEvent", "Reminder"
    string Title,
    string? Details,
    DateTimeOffset? DueDate,
    string? Assignee,
    long? Amount,
    bool ConfidenceHigh);

public record InvoiceDraftLineItem(
    string Description,
    long Amount,
    string? SuggestedAccountCode);

public record ParsedInvoiceDraftDto(
    string Vendor,
    DateOnly? InvoiceDate,
    long TotalAmount,
    string Currency,
    IReadOnlyList<InvoiceDraftLineItem> Items);

public interface IAiAssistantService
{
    Task<HouseholdAiConfigDto> GetConfigAsync(CancellationToken ct = default);
    Task<HouseholdAiConfigDto> UpdateConfigAsync(UpdateAiConfigRequest request, CancellationToken ct = default);
    Task<ParsedQuickAddDto> ParseQuickAddAsync(string naturalLanguageInput, CancellationToken ct = default);
    Task<ParsedInvoiceDraftDto> ParseInvoiceAsync(string invoiceText, CancellationToken ct = default);
    Task<string> SummarizeDocumentAsync(string documentText, CancellationToken ct = default);
}
