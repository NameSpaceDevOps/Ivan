namespace Ivan.Application.Services;

public record DashboardDueItemDto(
    Guid Id,
    string Module,
    string Title,
    DateTimeOffset DueAt,
    string? Details,
    string? Url,
    string Severity); // "danger", "warning", "info"

public record ParentDashboardDto(
    string HouseholdName,
    long NetWorth,
    long TotalCash,
    int PendingRoutineApprovals,
    int PendingRewardRedemptions,
    int PendingWalletRequests,
    int OverdueTasksCount,
    int ActiveLoansCount,
    IReadOnlyList<DashboardDueItemDto> DueSoonItems,
    IReadOnlyList<DashboardDueItemDto> ExpiringItems);

public record TeenDashboardDto(
    Guid PersonId,
    string PersonName,
    int PendingHomeworkCount,
    int UpcomingExamsCount,
    long WalletBalance,
    double SavingsGoalProgress,
    int TodayRoutinesCount,
    int TodayCompletedRoutines,
    string? NextWatchMediaTitle,
    string? NextWatchEpisodeInfo,
    IReadOnlyList<DashboardDueItemDto> MyDueItems);

public record ChildDashboardDto(
    Guid PersonId,
    string PersonName,
    int StarsBalance,
    long PiggyBankBalance,
    int TodayTotalRoutines,
    int TodayCompletedRoutines,
    int PendingHomeworkCount,
    IReadOnlyList<RoutineTaskDto> TodayRoutines,
    IReadOnlyList<RewardDto> AffordableRewards);

public interface IDashboardService
{
    Task<ParentDashboardDto> GetParentDashboardAsync(CancellationToken ct = default);
    Task<TeenDashboardDto> GetTeenDashboardAsync(Guid personId, CancellationToken ct = default);
    Task<ChildDashboardDto> GetChildDashboardAsync(Guid personId, CancellationToken ct = default);
}
