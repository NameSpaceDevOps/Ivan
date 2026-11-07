using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public interface IChartOfAccountsSeeder
{
    Task SeedAsync(Guid householdId, ChartPreset preset = ChartPreset.Full, string currency = "IRR", CancellationToken ct = default);
}
