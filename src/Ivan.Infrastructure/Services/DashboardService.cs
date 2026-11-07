using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IFinanceService _financeService;
    private readonly IChildrenService _childrenService;
    private readonly IMediaService _mediaService;

    public DashboardService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IFinanceService financeService,
        IChildrenService childrenService,
        IMediaService mediaService)
    {
        _db = db;
        _userContext = userContext;
        _financeService = financeService;
        _childrenService = childrenService;
        _mediaService = mediaService;
    }

    public async Task<ParentDashboardDto> GetParentDashboardAsync(CancellationToken ct = default)
    {
        var householdId = _userContext.HouseholdId ?? Guid.Empty;
        var household = await _db.Households.FindAsync([householdId], ct);
        var householdName = household?.Name ?? "Family";

        // Finance KPIs
        var netWorthReport = await _financeService.GetNetWorthReportAsync(ct: ct);
        var cashAccounts = await _financeService.GetAccountsAsync(type: AccountType.Asset, ct: ct);
        long totalCash = cashAccounts
            .Where(a => a.SubType == AccountSubType.Cash || a.SubType == AccountSubType.Bank)
            .Sum(a => a.CurrentBalance);

        // Pending approvals
        var pendingRoutines = (await _childrenService.GetPendingApprovalsAsync(ct)).Count;
        var pendingRewards = (await _childrenService.GetPendingRedemptionsAsync(ct)).Count;
        var pendingWallets = (await _childrenService.GetWalletRequestsAsync(status: WalletRequestStatus.Pending, ct: ct)).Count;

        // Overdue & Active
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var pendingTasks = await _db.TaskItems
            .Where(t => t.DueAt != null && t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled)
            .Select(t => new { t.Id, t.Title, t.Details, t.DueAt, t.Priority })
            .ToListAsync(ct);

        var overdueTasks = pendingTasks.Count(t => t.DueAt!.Value < now);

        var activeLoans = await _db.BookLoans
            .CountAsync(l => l.Status == LoanStatus.Active, ct);

        // Due soon items (next 7 days)
        var next7DaysUtc = now.AddDays(7);
        var next7DaysDate = today.AddDays(7);

        var dueSoonList = new List<DashboardDueItemDto>();

        var tasks = pendingTasks
            .Where(t => t.DueAt!.Value >= now && t.DueAt.Value <= next7DaysUtc)
            .ToList();

        foreach (var t in tasks)
        {
            dueSoonList.Add(new DashboardDueItemDto(
                t.Id, "Task", t.Title, t.DueAt!.Value, t.Details, $"/tasks/{t.Id}", t.Priority == TaskPriority.Urgent ? "danger" : "warning"));
        }

        var cheques = await _db.Cheques
            .Where(c => c.Status == ChequeStatus.Pending && c.DueDate >= today && c.DueDate <= next7DaysDate)
            .ToListAsync(ct);

        foreach (var ch in cheques)
        {
            var due = new DateTimeOffset(ch.DueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            dueSoonList.Add(new DashboardDueItemDto(
                ch.Id, "Cheque", $"Cheque #{ch.Number} ({ch.Bank})", due, $"{ch.Amount:N0} {ch.Currency}", "/money", "warning"));
        }

        var installments = await _db.Installments
            .Include(i => i.Loan)
            .Where(i => i.Status == InstallmentStatus.Pending && i.DueDate >= today && i.DueDate <= next7DaysDate)
            .ToListAsync(ct);

        foreach (var inst in installments)
        {
            var due = new DateTimeOffset(inst.DueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            dueSoonList.Add(new DashboardDueItemDto(
                inst.Id, "Installment", $"Installment #{inst.No} ({inst.Loan?.Direction})", due, $"{inst.Amount:N0} {inst.Loan?.Currency}", "/money", "warning"));
        }

        // Expiring items (next 30 days)
        var next30DaysDate = today.AddDays(30);
        var expiringList = new List<DashboardDueItemDto>();

        var docs = await _db.PersonDocuments
            .Include(d => d.Person)
            .Where(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value >= today && d.ExpiryDate.Value <= next30DaysDate)
            .ToListAsync(ct);

        foreach (var d in docs)
        {
            var due = new DateTimeOffset(d.ExpiryDate!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            expiringList.Add(new DashboardDueItemDto(
                d.Id, "Document", $"Doc Expiry: {d.Type} ({d.Person?.DisplayName})", due, d.Number, $"/people/{d.PersonId}", "danger"));
        }

        var warranties = await _db.Assets
            .Where(a => a.WarrantyEnd.HasValue && a.WarrantyEnd.Value >= today && a.WarrantyEnd.Value <= next30DaysDate)
            .ToListAsync(ct);

        foreach (var a in warranties)
        {
            var due = new DateTimeOffset(a.WarrantyEnd!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            expiringList.Add(new DashboardDueItemDto(
                a.Id, "Warranty", $"Warranty: {a.Name}", due, a.Brand, $"/assets/{a.Id}", "warning"));
        }

        return new ParentDashboardDto(
            householdName,
            netWorthReport.NetWorth,
            totalCash,
            pendingRoutines,
            pendingRewards,
            pendingWallets,
            overdueTasks,
            activeLoans,
            dueSoonList.OrderBy(i => i.DueAt).ToList(),
            expiringList.OrderBy(i => i.DueAt).ToList());
    }

    public async Task<TeenDashboardDto> GetTeenDashboardAsync(Guid personId, CancellationToken ct = default)
    {
        if (personId == Guid.Empty)
        {
            var defaultTeen = await _db.People.FirstOrDefaultAsync(p => p.Kind == PersonKind.Member, ct);
            if (defaultTeen != null)
            {
                personId = defaultTeen.Id;
            }
        }

        var person = await _db.People.FindAsync([personId], ct);
        var personName = person?.DisplayName ?? "Teen";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var homework = await _childrenService.GetHomeworkAsync(personId, ct: ct);
        var activeHw = homework.Count(h => h.Status != HomeworkStatus.Reviewed);

        var exams = await _childrenService.GetExamsAsync(personId, ct: ct);
        var upcomingExams = exams.Count(e => e.Date >= today);

        var wallet = await _childrenService.GetKidWalletAsync(personId, ct: ct);
        var goals = await _childrenService.GetSavingsGoalsAsync(personId, ct: ct);
        double avgGoalProgress = goals.Any() ? Math.Round(goals.Average(g => g.ProgressPercent), 1) : 0;

        var routines = await _childrenService.GetRoutineTasksAsync(personId, today, ct: ct);
        int totalRoutines = routines.Count;
        int completedRoutines = routines.Count(r => r.IsCompletedToday);

        // Next to watch
        string? nextMedia = null;
        string? nextEp = null;

        var seriesList = await _mediaService.GetMediaItemsAsync(type: MediaType.Series, personId: personId, ct: ct);
        foreach (var s in seriesList)
        {
            var next = await _mediaService.GetNextToWatchAsync(s.Id, personId, ct);
            if (next != null)
            {
                nextMedia = s.Title;
                nextEp = $"S{next.SeasonNumber}:E{next.EpisodeNumber} ({next.Title})";
                break;
            }
        }

        var myDues = new List<DashboardDueItemDto>();
        foreach (var hw in homework.Where(h => h.Status != HomeworkStatus.Reviewed))
        {
            var due = new DateTimeOffset(hw.DueOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            myDues.Add(new DashboardDueItemDto(
                hw.Id, "Homework", $"{hw.SubjectName}: {hw.Title}", due, hw.Details, "/kids", "info"));
        }

        return new TeenDashboardDto(
            personId,
            personName,
            activeHw,
            upcomingExams,
            wallet.Balance,
            avgGoalProgress,
            totalRoutines,
            completedRoutines,
            nextMedia,
            nextEp,
            myDues.OrderBy(d => d.DueAt).ToList());
    }

    public async Task<ChildDashboardDto> GetChildDashboardAsync(Guid personId, CancellationToken ct = default)
    {
        if (personId == Guid.Empty)
        {
            var defaultChild = await _db.People.FirstOrDefaultAsync(p => p.Kind == PersonKind.Member, ct);
            if (defaultChild != null)
            {
                personId = defaultChild.Id;
            }
        }

        var person = await _db.People.FindAsync([personId], ct);
        var personName = person?.DisplayName ?? "Child";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var stars = await _childrenService.GetPointBalanceAsync(personId, ct);
        var wallet = await _childrenService.GetKidWalletAsync(personId, ct);

        var routines = (await _childrenService.GetRoutineTasksAsync(personId, today, ct)).ToList();
        int totalRoutines = routines.Count;
        int completedRoutines = routines.Count(r => r.IsCompletedToday);

        var homework = await _childrenService.GetHomeworkAsync(personId, ct: ct);
        int pendingHw = homework.Count(h => h.Status != HomeworkStatus.Reviewed);

        var allRewards = await _childrenService.GetRewardsAsync(activeOnly: true, ct: ct);
        var affordable = allRewards.Where(r => r.Cost <= stars).ToList();

        return new ChildDashboardDto(
            personId,
            personName,
            stars,
            wallet.Balance,
            totalRoutines,
            completedRoutines,
            pendingHw,
            routines,
            affordable);
    }
}
