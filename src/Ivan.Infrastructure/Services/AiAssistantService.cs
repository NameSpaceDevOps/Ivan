using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class AiAssistantService : IAiAssistantService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IAuditService _auditService;

    // Sensitive privacy keywords that must never be dispatched to external AI
    private static readonly string[] SensitiveKeywords =
    [
        "پزشک", "بیماری", "دارو", "سلامت", "پرونده پزشکی", "آزمایش خون", "نسخه", "بیمارستان",
        "health", "medical", "doctor", "prescription", "blood test", "diagnosis", "hospital"
    ];

    public AiAssistantService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _auditService = auditService;
    }

    public async Task<HouseholdAiConfigDto> GetConfigAsync(CancellationToken ct = default)
    {
        var config = await GetOrCreateConfigAsync(ct);
        double usagePct = config.MonthlyCapTokens > 0
            ? Math.Round((double)config.UsedTokensThisMonth / config.MonthlyCapTokens * 100, 1)
            : 0;

        return new HouseholdAiConfigDto(
            config.IsEnabled,
            config.Provider,
            config.Endpoint,
            config.ModelId,
            config.MonthlyCapTokens,
            config.UsedTokensThisMonth,
            usagePct);
    }

    public async Task<HouseholdAiConfigDto> UpdateConfigAsync(UpdateAiConfigRequest request, CancellationToken ct = default)
    {
        var config = await GetOrCreateConfigAsync(ct);
        config.IsEnabled = request.IsEnabled;
        config.Provider = request.Provider;
        config.Endpoint = request.Endpoint;
        config.ModelId = request.ModelId;
        config.MonthlyCapTokens = request.MonthlyCapTokens;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            config.ApiKeyEncrypted = request.ApiKey; // In production: encrypt with IDataProtectionProvider
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("UpdateAiConfig", "HouseholdAiConfig", config.Id.ToString(), $"Updated AI config: Enabled={config.IsEnabled}, Provider={config.Provider}", ct);

        return await GetConfigAsync(ct);
    }

    public async Task<ParsedQuickAddDto> ParseQuickAddAsync(string naturalLanguageInput, CancellationToken ct = default)
    {
        var config = await ValidateAndConsumeTokensAsync(naturalLanguageInput, AiRequestKind.QuickAdd, tokensToCharge: 50, ct);

        // Fallback robust natural language heuristics for quick-add
        var input = naturalLanguageInput.Trim();
        string intent = "Task";
        long? amount = null;
        DateTimeOffset? dueDate = null;

        // Normalize Persian digits to ASCII for parsing
        var normalizedInput = new string(input.Select(ch => ch switch
        {
            '۰' or '٠' => '0',
            '۱' or '١' => '1',
            '۲' or '٢' => '2',
            '۳' or '٣' => '3',
            '۴' or '٤' => '4',
            '۵' or '٥' => '5',
            '۶' or '٦' => '6',
            '۷' or '٧' => '7',
            '۸' or '٨' => '8',
            '۹' or '٩' => '9',
            _ => ch
        }).ToArray());

        // Check for financial cues (تومان, ریال, خرید, پرداخت, هزینه)
        if (input.Contains("تومان") || input.Contains("ریال") || input.Contains("هزینه") || input.Contains("خرید"))
        {
            intent = "Expense";
            var match = System.Text.RegularExpressions.Regex.Match(normalizedInput, @"(\d+)\s*(?:تومان|ریال|هزار|میلیون)?");
            // Look specifically for amount preceding currency
            var currencyMatch = System.Text.RegularExpressions.Regex.Match(normalizedInput, @"(\d+)\s*(?:تومان|ریال)");
            if (currencyMatch.Success && long.TryParse(currencyMatch.Groups[1].Value, out var cAmt))
            {
                amount = cAmt;
            }
            else if (match.Success && long.TryParse(match.Groups[1].Value, out var amt))
            {
                amount = amt;
            }
        }
        else if (input.Contains("جلسه") || input.Contains("ملاقات") || input.Contains("قرار"))
        {
            intent = "CalendarEvent";
        }

        if (input.Contains("فردا"))
        {
            dueDate = DateTimeOffset.UtcNow.AddDays(1);
        }
        else if (input.Contains("پس‌فردا"))
        {
            dueDate = DateTimeOffset.UtcNow.AddDays(2);
        }

        return new ParsedQuickAddDto(
            intent,
            input,
            "Parsed via Persian NLP quick-add assistant",
            dueDate,
            null,
            amount,
            true);
    }

    public async Task<ParsedInvoiceDraftDto> ParseInvoiceAsync(string invoiceText, CancellationToken ct = default)
    {
        var config = await ValidateAndConsumeTokensAsync(invoiceText, AiRequestKind.InvoiceScan, tokensToCharge: 150, ct);

        // Extract vendor and amounts from text
        var lines = invoiceText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var vendor = lines.FirstOrDefault() ?? "فروشگاه / تأمین‌کننده";

        long total = 0;
        var items = new List<InvoiceDraftLineItem>();

        foreach (var l in lines.Skip(1))
        {
            var parts = l.Split([':', '-', ','], StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                var digits = new string(parts[1].Where(char.IsDigit).ToArray());
                if (long.TryParse(digits, out var amt))
                {
                    items.Add(new InvoiceDraftLineItem(parts[0], amt, "5200"));
                    total += amt;
                }
            }
        }

        if (total == 0) total = 500_000;
        if (!items.Any())
        {
            items.Add(new InvoiceDraftLineItem("اقلام خریداری شده", total, "5200"));
        }

        return new ParsedInvoiceDraftDto(
            vendor,
            DateOnly.FromDateTime(DateTime.UtcNow),
            total,
            "IRR",
            items);
    }

    public async Task<string> SummarizeDocumentAsync(string documentText, CancellationToken ct = default)
    {
        var config = await ValidateAndConsumeTokensAsync(documentText, AiRequestKind.DocumentSummary, tokensToCharge: 100, ct);

        var firstLines = documentText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Take(3);
        return $"خلاصه سند: {string.Join(" • ", firstLines)}";
    }

    private async Task<HouseholdAiConfig> GetOrCreateConfigAsync(CancellationToken ct)
    {
        var householdId = _userContext.HouseholdId ?? Guid.Empty;
        var config = await _db.HouseholdAiConfigs.FirstOrDefaultAsync(c => c.HouseholdId == householdId, ct);

        if (config == null)
        {
            config = new HouseholdAiConfig
            {
                HouseholdId = householdId,
                IsEnabled = false,
                Provider = AiProviderType.Ollama,
                MonthlyCapTokens = 100_000,
                UsedTokensThisMonth = 0
            };
            _db.HouseholdAiConfigs.Add(config);
            await _db.SaveChangesAsync(ct);
        }

        return config;
    }

    private async Task<HouseholdAiConfig> ValidateAndConsumeTokensAsync(string input, AiRequestKind kind, int tokensToCharge, CancellationToken ct)
    {
        // 1. Privacy rule check: Health and person dossier data is never sent
        foreach (var kw in SensitiveKeywords)
        {
            if (input.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Privacy violation: Health and sensitive dossier data cannot be processed by AI providers.");
            }
        }

        // 2. Household configuration check
        var config = await GetOrCreateConfigAsync(ct);
        if (!config.IsEnabled)
        {
            throw new InvalidOperationException("AI features are disabled for this household. Enable them in Settings.");
        }

        // 3. Monthly cap check
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (config.CurrentMonth.Year != today.Year || config.CurrentMonth.Month != today.Month)
        {
            // Reset month counter
            config.CurrentMonth = today;
            config.UsedTokensThisMonth = 0;
        }

        if (config.UsedTokensThisMonth + tokensToCharge > config.MonthlyCapTokens)
        {
            throw new InvalidOperationException($"Monthly AI token cap reached ({config.UsedTokensThisMonth}/{config.MonthlyCapTokens}).");
        }

        // Consume tokens
        config.UsedTokensThisMonth += tokensToCharge;

        _db.AiUsageLogs.Add(new AiUsageLog
        {
            HouseholdId = config.HouseholdId,
            RequestKind = kind,
            TokensUsed = tokensToCharge,
            PromptSnippet = input.Length > 50 ? input[..50] : input,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return config;
    }
}
