using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class ChildrenService : IChildrenService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly ILedgerService _ledgerService;
    private readonly IFinanceService _financeService;
    private readonly IReminderService _reminderService;
    private readonly IAuditService _auditService;

    public ChildrenService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        ILedgerService ledgerService,
        IFinanceService financeService,
        IReminderService reminderService,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _ledgerService = ledgerService;
        _financeService = financeService;
        _reminderService = reminderService;
        _auditService = auditService;
    }

    // ==========================================
    // 1. ROUTINES & POINTS
    // ==========================================

    public async Task<IReadOnlyList<RoutineTaskDto>> GetRoutineTasksAsync(Guid personId, DateOnly? asOfDate = null, CancellationToken ct = default)
    {
        var targetDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var tasks = await _db.RoutineTasks
            .Where(t => t.PersonId == personId && t.IsActive)
            .ToListAsync(ct);

        var completions = await _db.RoutineCompletions
            .Where(c => c.Date == targetDate && tasks.Select(t => t.Id).Contains(c.RoutineTaskId))
            .ToListAsync(ct);

        return tasks.Select(t =>
        {
            var comp = completions.FirstOrDefault(c => c.RoutineTaskId == t.Id);
            return new RoutineTaskDto(
                t.Id,
                t.PersonId,
                t.Title,
                t.Icon,
                t.TimeSlot,
                t.Days,
                t.Points,
                t.RequiresApproval,
                t.IsActive,
                comp != null,
                comp?.Status);
        }).ToList();
    }

    public async Task<RoutineTaskDto> CreateRoutineTaskAsync(CreateRoutineTaskRequest request, CancellationToken ct = default)
    {
        var task = new RoutineTask
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            Title = request.Title,
            Icon = request.Icon,
            TimeSlot = request.TimeSlot,
            Days = request.Days,
            Points = request.Points,
            RequiresApproval = request.RequiresApproval,
            IsActive = true
        };

        _db.RoutineTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateRoutineTask", "RoutineTask", task.Id.ToString(), $"Created routine task {task.Title} ({task.Points} pts)", ct);

        return new RoutineTaskDto(
            task.Id,
            task.PersonId,
            task.Title,
            task.Icon,
            task.TimeSlot,
            task.Days,
            task.Points,
            task.RequiresApproval,
            task.IsActive,
            false,
            null);
    }

    public async Task<RoutineCompletionDto> CompleteRoutineTaskAsync(Guid routineTaskId, DateOnly? date = null, CancellationToken ct = default)
    {
        var task = await _db.RoutineTasks.FindAsync([routineTaskId], ct)
            ?? throw new KeyNotFoundException($"Routine task {routineTaskId} not found.");

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _db.RoutineCompletions
            .FirstOrDefaultAsync(c => c.RoutineTaskId == routineTaskId && c.Date == targetDate, ct);

        if (existing != null)
            throw new InvalidOperationException("Routine task is already completed for this date.");

        var status = task.RequiresApproval ? CompletionStatus.PendingApproval : CompletionStatus.Approved;
        var pointsAwarded = task.RequiresApproval ? 0 : task.Points;

        var completion = new RoutineCompletion
        {
            HouseholdId = task.HouseholdId,
            RoutineTaskId = routineTaskId,
            Date = targetDate,
            CompletedAt = DateTimeOffset.UtcNow,
            PointsAwarded = pointsAwarded,
            Status = status
        };

        _db.RoutineCompletions.Add(completion);

        if (!task.RequiresApproval)
        {
            // Award points immediately
            _db.PointLedgers.Add(new PointLedger
            {
                HouseholdId = task.HouseholdId,
                PersonId = task.PersonId,
                Delta = task.Points,
                Reason = $"Completed routine: {task.Title}",
                SourceType = "Routine",
                SourceId = completion.Id,
                At = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CompleteRoutineTask", "RoutineCompletion", completion.Id.ToString(), $"Completed routine {task.Title} (Status: {status})", ct);

        return new RoutineCompletionDto(
            completion.Id,
            completion.RoutineTaskId,
            task.Title,
            completion.Date,
            completion.CompletedAt,
            completion.ApprovedBy,
            completion.PointsAwarded,
            completion.Status);
    }

    public async Task<RoutineCompletionDto> ApproveRoutineCompletionAsync(Guid completionId, Guid parentUserId, CancellationToken ct = default)
    {
        var comp = await _db.RoutineCompletions
            .Include(c => c.RoutineTask)
            .FirstOrDefaultAsync(c => c.Id == completionId, ct)
            ?? throw new KeyNotFoundException($"Routine completion {completionId} not found.");

        if (comp.Status == CompletionStatus.Approved)
            throw new InvalidOperationException("Completion is already approved.");

        comp.Status = CompletionStatus.Approved;
        comp.ApprovedBy = parentUserId;
        comp.PointsAwarded = comp.RoutineTask?.Points ?? 0;

        // Credit points
        _db.PointLedgers.Add(new PointLedger
        {
            HouseholdId = comp.HouseholdId,
            PersonId = comp.RoutineTask!.PersonId,
            Delta = comp.PointsAwarded,
            Reason = $"Approved routine: {comp.RoutineTask.Title}",
            SourceType = "Routine",
            SourceId = comp.Id,
            At = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("ApproveRoutineCompletion", "RoutineCompletion", comp.Id.ToString(), $"Approved routine completion {comp.RoutineTask.Title}", ct);

        return new RoutineCompletionDto(
            comp.Id,
            comp.RoutineTaskId,
            comp.RoutineTask.Title,
            comp.Date,
            comp.CompletedAt,
            comp.ApprovedBy,
            comp.PointsAwarded,
            comp.Status);
    }

    public async Task<RoutineCompletionDto> RejectRoutineCompletionAsync(Guid completionId, Guid parentUserId, CancellationToken ct = default)
    {
        var comp = await _db.RoutineCompletions
            .Include(c => c.RoutineTask)
            .FirstOrDefaultAsync(c => c.Id == completionId, ct)
            ?? throw new KeyNotFoundException($"Routine completion {completionId} not found.");

        comp.Status = CompletionStatus.Rejected;
        comp.ApprovedBy = parentUserId;
        comp.PointsAwarded = 0;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("RejectRoutineCompletion", "RoutineCompletion", comp.Id.ToString(), $"Rejected routine completion {comp.RoutineTask?.Title}", ct);

        return new RoutineCompletionDto(
            comp.Id,
            comp.RoutineTaskId,
            comp.RoutineTask?.Title ?? "",
            comp.Date,
            comp.CompletedAt,
            comp.ApprovedBy,
            comp.PointsAwarded,
            comp.Status);
    }

    public async Task<IReadOnlyList<RoutineCompletionDto>> GetPendingApprovalsAsync(CancellationToken ct = default)
    {
        var list = await _db.RoutineCompletions
            .Include(c => c.RoutineTask)
            .Where(c => c.Status == CompletionStatus.PendingApproval)
            .ToListAsync(ct);

        return list.OrderByDescending(c => c.CompletedAt).Select(c => new RoutineCompletionDto(
            c.Id,
            c.RoutineTaskId,
            c.RoutineTask?.Title ?? "",
            c.Date,
            c.CompletedAt,
            c.ApprovedBy,
            c.PointsAwarded,
            c.Status)).ToList();
    }

    public async Task<int> GetPointBalanceAsync(Guid personId, CancellationToken ct = default)
    {
        var deltas = await _db.PointLedgers
            .Where(p => p.PersonId == personId)
            .Select(p => p.Delta)
            .ToListAsync(ct);

        return deltas.Sum();
    }

    public async Task<IReadOnlyList<PointLedgerDto>> GetPointHistoryAsync(Guid personId, CancellationToken ct = default)
    {
        var list = await _db.PointLedgers
            .Where(p => p.PersonId == personId)
            .ToListAsync(ct);

        return list.OrderByDescending(p => p.At).Select(p => new PointLedgerDto(
            p.Id,
            p.PersonId,
            p.Delta,
            p.Reason,
            p.SourceType,
            p.SourceId,
            p.At)).ToList();
    }

    // ==========================================
    // 2. REWARDS & REDEMPTIONS
    // ==========================================

    public async Task<IReadOnlyList<RewardDto>> GetRewardsAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _db.Rewards.AsQueryable();
        if (activeOnly)
            query = query.Where(r => r.IsActive);

        var list = await query.OrderBy(r => r.Cost).ToListAsync(ct);
        return list.Select(r => new RewardDto(r.Id, r.Title, r.Cost, r.Icon, r.IsActive)).ToList();
    }

    public async Task<RewardDto> CreateRewardAsync(CreateRewardRequest request, CancellationToken ct = default)
    {
        var reward = new Reward
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Title = request.Title,
            Cost = request.Cost,
            Icon = request.Icon,
            IsActive = true
        };

        _db.Rewards.Add(reward);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateReward", "Reward", reward.Id.ToString(), $"Created reward {reward.Title} ({reward.Cost} pts)", ct);

        return new RewardDto(reward.Id, reward.Title, reward.Cost, reward.Icon, reward.IsActive);
    }

    public async Task<RewardRedemptionDto> RedeemRewardAsync(Guid rewardId, Guid personId, CancellationToken ct = default)
    {
        var reward = await _db.Rewards.FindAsync([rewardId], ct)
            ?? throw new KeyNotFoundException($"Reward {rewardId} not found.");

        int currentBalance = await GetPointBalanceAsync(personId, ct);
        if (currentBalance < reward.Cost)
            throw new InvalidOperationException($"Insufficient points. Required: {reward.Cost}, Available: {currentBalance}");

        var redemption = new RewardRedemption
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            RewardId = rewardId,
            PersonId = personId,
            Status = RedemptionStatus.Requested
        };

        _db.RewardRedemptions.Add(redemption);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("RedeemReward", "RewardRedemption", redemption.Id.ToString(), $"Requested reward {reward.Title}", ct);

        var person = await _db.People.FindAsync([personId], ct);
        return new RewardRedemptionDto(
            redemption.Id,
            reward.Id,
            reward.Title,
            reward.Cost,
            reward.Icon,
            personId,
            person?.DisplayName ?? "",
            redemption.Status,
            null,
            null);
    }

    public async Task<RewardRedemptionDto> ApproveRewardRedemptionAsync(Guid redemptionId, Guid parentUserId, CancellationToken ct = default)
    {
        var red = await _db.RewardRedemptions
            .Include(r => r.Reward)
            .Include(r => r.Person)
            .FirstOrDefaultAsync(r => r.Id == redemptionId, ct)
            ?? throw new KeyNotFoundException($"Reward redemption {redemptionId} not found.");

        if (red.Status == RedemptionStatus.Approved)
            throw new InvalidOperationException("Redemption is already approved.");

        int currentBalance = await GetPointBalanceAsync(red.PersonId, ct);
        if (currentBalance < red.Reward!.Cost)
            throw new InvalidOperationException($"Insufficient points to approve redemption. Required: {red.Reward.Cost}, Available: {currentBalance}");

        red.Status = RedemptionStatus.Approved;
        red.DecidedBy = parentUserId;
        red.DecidedAt = DateTimeOffset.UtcNow;

        // Deduct points
        _db.PointLedgers.Add(new PointLedger
        {
            HouseholdId = red.HouseholdId,
            PersonId = red.PersonId,
            Delta = -red.Reward.Cost,
            Reason = $"Redeemed reward: {red.Reward.Title}",
            SourceType = "RewardRedemption",
            SourceId = red.Id,
            At = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("ApproveRewardRedemption", "RewardRedemption", red.Id.ToString(), $"Approved redemption of {red.Reward.Title}", ct);

        return new RewardRedemptionDto(
            red.Id,
            red.RewardId,
            red.Reward.Title,
            red.Reward.Cost,
            red.Reward.Icon,
            red.PersonId,
            red.Person?.DisplayName ?? "",
            red.Status,
            red.DecidedBy,
            red.DecidedAt);
    }

    public async Task<RewardRedemptionDto> RejectRewardRedemptionAsync(Guid redemptionId, Guid parentUserId, CancellationToken ct = default)
    {
        var red = await _db.RewardRedemptions
            .Include(r => r.Reward)
            .Include(r => r.Person)
            .FirstOrDefaultAsync(r => r.Id == redemptionId, ct)
            ?? throw new KeyNotFoundException($"Reward redemption {redemptionId} not found.");

        red.Status = RedemptionStatus.Rejected;
        red.DecidedBy = parentUserId;
        red.DecidedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("RejectRewardRedemption", "RewardRedemption", red.Id.ToString(), $"Rejected redemption of {red.Reward?.Title}", ct);

        return new RewardRedemptionDto(
            red.Id,
            red.RewardId,
            red.Reward?.Title ?? "",
            red.Reward?.Cost ?? 0,
            red.Reward?.Icon ?? "",
            red.PersonId,
            red.Person?.DisplayName ?? "",
            red.Status,
            red.DecidedBy,
            red.DecidedAt);
    }

    public async Task<IReadOnlyList<RewardRedemptionDto>> GetPendingRedemptionsAsync(CancellationToken ct = default)
    {
        var list = await _db.RewardRedemptions
            .Include(r => r.Reward)
            .Include(r => r.Person)
            .Where(r => r.Status == RedemptionStatus.Requested)
            .ToListAsync(ct);

        return list.OrderByDescending(r => r.CreatedAt).Select(r => new RewardRedemptionDto(
            r.Id,
            r.RewardId,
            r.Reward?.Title ?? "",
            r.Reward?.Cost ?? 0,
            r.Reward?.Icon ?? "",
            r.PersonId,
            r.Person?.DisplayName ?? "",
            r.Status,
            r.DecidedBy,
            r.DecidedAt)).ToList();
    }

    // ==========================================
    // 3. HOMEWORK, SUBJECTS & EXAMS
    // ==========================================

    public async Task<IReadOnlyList<SubjectDto>> GetSubjectsAsync(Guid personId, CancellationToken ct = default)
    {
        var list = await _db.Subjects
            .Where(s => s.PersonId == personId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return list.Select(s => new SubjectDto(s.Id, s.PersonId, s.Name, s.Teacher)).ToList();
    }

    public async Task<SubjectDto> CreateSubjectAsync(CreateSubjectRequest request, CancellationToken ct = default)
    {
        var subject = new Subject
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            Name = request.Name,
            Teacher = request.Teacher
        };

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateSubject", "Subject", subject.Id.ToString(), $"Created subject {subject.Name}", ct);

        return new SubjectDto(subject.Id, subject.PersonId, subject.Name, subject.Teacher);
    }

    public async Task<IReadOnlyList<HomeworkItemDto>> GetHomeworkAsync(Guid personId, HomeworkStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.HomeworkItems
            .Include(h => h.Subject)
            .Where(h => h.PersonId == personId);

        if (status.HasValue)
            query = query.Where(h => h.Status == status.Value);

        var list = await query.OrderBy(h => h.DueOn).ToListAsync(ct);
        return list.Select(h => new HomeworkItemDto(
            h.Id,
            h.PersonId,
            h.SubjectId,
            h.Subject?.Name ?? "",
            h.Title,
            h.Details,
            h.AssignedOn,
            h.DueOn,
            h.Status,
            h.ParentChecked)).ToList();
    }

    public async Task<HomeworkItemDto> CreateHomeworkAsync(CreateHomeworkRequest request, CancellationToken ct = default)
    {
        var homework = new HomeworkItem
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            SubjectId = request.SubjectId,
            Title = request.Title,
            Details = request.Details,
            AssignedOn = request.AssignedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DueOn = request.DueOn,
            Status = HomeworkStatus.Assigned,
            ParentChecked = false
        };

        _db.HomeworkItems.Add(homework);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateHomework", "HomeworkItem", homework.Id.ToString(), $"Created homework {homework.Title} due {homework.DueOn}", ct);

        // Schedule reminder for due date
        var dueDateTime = new DateTimeOffset(homework.DueOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var reminderTime = dueDateTime.AddHours(-14); // Evening before
        if (reminderTime > DateTimeOffset.UtcNow)
        {
            await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                "HomeworkDue",
                homework.Id,
                $"Homework due: {homework.Title}",
                reminderTime,
                $"homework-{homework.Id}"), ct);
        }

        var subject = await _db.Subjects.FindAsync([homework.SubjectId], ct);
        return new HomeworkItemDto(
            homework.Id,
            homework.PersonId,
            homework.SubjectId,
            subject?.Name ?? "",
            homework.Title,
            homework.Details,
            homework.AssignedOn,
            homework.DueOn,
            homework.Status,
            homework.ParentChecked);
    }

    public async Task<HomeworkItemDto> UpdateHomeworkStatusAsync(Guid homeworkId, HomeworkStatus status, bool? parentChecked = null, CancellationToken ct = default)
    {
        var homework = await _db.HomeworkItems
            .Include(h => h.Subject)
            .FirstOrDefaultAsync(h => h.Id == homeworkId, ct)
            ?? throw new KeyNotFoundException($"Homework item {homeworkId} not found.");

        homework.Status = status;
        if (parentChecked.HasValue)
            homework.ParentChecked = parentChecked.Value;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("UpdateHomeworkStatus", "HomeworkItem", homework.Id.ToString(), $"Updated homework {homework.Title} status to {status}", ct);

        return new HomeworkItemDto(
            homework.Id,
            homework.PersonId,
            homework.SubjectId,
            homework.Subject?.Name ?? "",
            homework.Title,
            homework.Details,
            homework.AssignedOn,
            homework.DueOn,
            homework.Status,
            homework.ParentChecked);
    }

    public async Task<IReadOnlyList<ExamEventDto>> GetExamsAsync(Guid personId, CancellationToken ct = default)
    {
        var list = await _db.ExamEvents
            .Include(e => e.Subject)
            .Where(e => e.PersonId == personId)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

        return list.Select(e => new ExamEventDto(
            e.Id,
            e.PersonId,
            e.SubjectId,
            e.Subject?.Name ?? "",
            e.Date,
            e.Topics,
            e.Score)).ToList();
    }

    public async Task<ExamEventDto> CreateExamAsync(CreateExamEventRequest request, CancellationToken ct = default)
    {
        var exam = new ExamEvent
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            SubjectId = request.SubjectId,
            Date = request.Date,
            Topics = request.Topics,
            Score = request.Score
        };

        _db.ExamEvents.Add(exam);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateExam", "ExamEvent", exam.Id.ToString(), $"Created exam on {exam.Date}", ct);

        // Schedule exam countdown reminder (2 days before)
        var examDateTime = new DateTimeOffset(exam.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var reminderTime = examDateTime.AddDays(-2);
        if (reminderTime > DateTimeOffset.UtcNow)
        {
            await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                "ExamDue",
                exam.Id,
                $"Upcoming exam on {exam.Date}: {exam.Topics}",
                reminderTime,
                $"exam-{exam.Id}"), ct);
        }

        var subject = await _db.Subjects.FindAsync([exam.SubjectId], ct);
        return new ExamEventDto(
            exam.Id,
            exam.PersonId,
            exam.SubjectId,
            subject?.Name ?? "",
            exam.Date,
            exam.Topics,
            exam.Score);
    }

    public async Task<IReadOnlyList<SchoolScheduleDto>> GetSchoolScheduleAsync(Guid personId, CancellationToken ct = default)
    {
        var list = await _db.SchoolSchedules
            .Include(s => s.Subject)
            .Where(s => s.PersonId == personId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.Period)
            .ToListAsync(ct);

        return list.Select(s => new SchoolScheduleDto(
            s.Id,
            s.PersonId,
            s.DayOfWeek,
            s.Period,
            s.SubjectId,
            s.Subject?.Name ?? "",
            s.StartTime,
            s.EndTime)).ToList();
    }

    public async Task<SchoolScheduleDto> CreateSchoolScheduleAsync(CreateSchoolScheduleRequest request, CancellationToken ct = default)
    {
        var schedule = new SchoolSchedule
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            DayOfWeek = request.DayOfWeek,
            Period = request.Period,
            SubjectId = request.SubjectId,
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };

        _db.SchoolSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        var subject = await _db.Subjects.FindAsync([schedule.SubjectId], ct);
        return new SchoolScheduleDto(
            schedule.Id,
            schedule.PersonId,
            schedule.DayOfWeek,
            schedule.Period,
            schedule.SubjectId,
            subject?.Name ?? "",
            schedule.StartTime,
            schedule.EndTime);
    }

    // ==========================================
    // 4. KID WALLET & ALLOWANCE
    // ==========================================

    public async Task<KidWalletDto> SetupKidWalletAsync(Guid personId, WalletMode mode = WalletMode.Virtual, Guid? bankAccountId = null, Guid? custodianPersonId = null, CancellationToken ct = default)
    {
        var existing = await _db.WalletLinks
            .Include(w => w.WalletAccount)
            .FirstOrDefaultAsync(w => w.PersonId == personId, ct);

        if (existing != null)
        {
            long bal = await _ledgerService.GetAccountBalanceAsync(existing.WalletAccountId, null, ct);
            return new KidWalletDto(
                existing.PersonId,
                existing.WalletAccountId,
                existing.WalletAccount?.Code ?? "",
                existing.WalletAccount?.PersianName ?? existing.WalletAccount?.Name ?? "",
                bal,
                existing.Mode,
                existing.BankAccountId,
                existing.CustodianPersonId);
        }

        var person = await _db.People.FindAsync([personId], ct)
            ?? throw new KeyNotFoundException($"Person {personId} not found.");

        // Find or create wallet account under 1030
        var parentKidGroup = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "1030", ct);

        var code = $"103{new Random().Next(10, 99)}";
        var walletAccount = new Account
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Code = code,
            Name = $"{person.DisplayName}'s Wallet",
            PersianName = $"کیف پول {person.DisplayName}",
            Type = AccountType.Asset,
            SubType = AccountSubType.Wallet,
            ParentAccountId = parentKidGroup?.Id,
            OwnerPersonId = personId,
            IsActive = true
        };
        _db.Accounts.Add(walletAccount);
        await _db.SaveChangesAsync(ct);

        var walletLink = new WalletLink
        {
            HouseholdId = walletAccount.HouseholdId,
            PersonId = personId,
            WalletAccountId = walletAccount.Id,
            Mode = mode,
            BankAccountId = bankAccountId,
            CustodianPersonId = custodianPersonId
        };
        _db.WalletLinks.Add(walletLink);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("SetupKidWallet", "WalletLink", walletLink.Id.ToString(), $"Setup wallet for {person.DisplayName}", ct);

        return new KidWalletDto(
            personId,
            walletAccount.Id,
            walletAccount.Code,
            walletAccount.PersianName,
            0,
            mode,
            bankAccountId,
            custodianPersonId);
    }

    public async Task<KidWalletDto> GetKidWalletAsync(Guid personId, CancellationToken ct = default)
    {
        var link = await _db.WalletLinks
            .Include(w => w.WalletAccount)
            .FirstOrDefaultAsync(w => w.PersonId == personId, ct);

        if (link == null)
        {
            // Auto setup
            return await SetupKidWalletAsync(personId, WalletMode.Virtual, null, null, ct);
        }

        long balance = await _ledgerService.GetAccountBalanceAsync(link.WalletAccountId, null, ct);
        return new KidWalletDto(
            link.PersonId,
            link.WalletAccountId,
            link.WalletAccount?.Code ?? "",
            link.WalletAccount?.PersianName ?? link.WalletAccount?.Name ?? "",
            balance,
            link.Mode,
            link.BankAccountId,
            link.CustodianPersonId);
    }

    public async Task<AllowanceRuleDto?> GetAllowanceRuleAsync(Guid personId, CancellationToken ct = default)
    {
        var rule = await _db.AllowanceRules
            .FirstOrDefaultAsync(r => r.PersonId == personId && r.Active, ct);

        return rule == null ? null : new AllowanceRuleDto(
            rule.Id,
            rule.PersonId,
            rule.Amount,
            rule.RecurrenceId,
            rule.FromAccountId,
            rule.WalletAccountId,
            rule.Active);
    }

    public async Task<AllowanceRuleDto> SetAllowanceRuleAsync(SetAllowanceRuleRequest request, CancellationToken ct = default)
    {
        var wallet = await GetKidWalletAsync(request.PersonId, ct);

        var rule = await _db.AllowanceRules
            .FirstOrDefaultAsync(r => r.PersonId == request.PersonId, ct);

        if (rule == null)
        {
            rule = new AllowanceRule
            {
                HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
                PersonId = request.PersonId,
                Amount = request.Amount,
                RecurrenceId = request.RecurrenceId,
                FromAccountId = request.FromAccountId,
                WalletAccountId = wallet.WalletAccountId,
                Active = true
            };
            _db.AllowanceRules.Add(rule);
        }
        else
        {
            rule.Amount = request.Amount;
            rule.RecurrenceId = request.RecurrenceId;
            rule.FromAccountId = request.FromAccountId;
            rule.WalletAccountId = wallet.WalletAccountId;
            rule.Active = true;
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("SetAllowanceRule", "AllowanceRule", rule.Id.ToString(), $"Set allowance {rule.Amount:N0} for person {rule.PersonId}", ct);

        return new AllowanceRuleDto(
            rule.Id,
            rule.PersonId,
            rule.Amount,
            rule.RecurrenceId,
            rule.FromAccountId,
            rule.WalletAccountId,
            rule.Active);
    }

    public async Task ProcessAllowancePayoutAsync(Guid allowanceRuleId, CancellationToken ct = default)
    {
        var rule = await _db.AllowanceRules
            .Include(r => r.Person)
            .FirstOrDefaultAsync(r => r.Id == allowanceRuleId, ct)
            ?? throw new KeyNotFoundException($"Allowance rule {allowanceRuleId} not found.");

        if (!rule.Active) return;

        // Post transfer entry from FromAccountId to WalletAccountId
        await _financeService.RecordTransferAsync(new RecordTransferRequest(
            DateOnly.FromDateTime(DateTime.UtcNow),
            rule.Amount,
            rule.FromAccountId,
            rule.WalletAccountId,
            $"Allowance payout for {rule.Person?.DisplayName}"), ct);

        await _auditService.LogAsync("ProcessAllowance", "AllowanceRule", rule.Id.ToString(), $"Transferred allowance {rule.Amount:N0} to wallet", ct);
    }

    // ==========================================
    // 5. GOALS & REQUESTS
    // ==========================================

    public async Task<IReadOnlyList<SavingsGoalDto>> GetSavingsGoalsAsync(Guid personId, CancellationToken ct = default)
    {
        var wallet = await GetKidWalletAsync(personId, ct);
        var goals = await _db.SavingsGoals
            .Where(g => g.PersonId == personId)
            .ToListAsync(ct);

        return goals.OrderByDescending(g => g.CreatedAt).Select(g =>
        {
            long currentSaved = Math.Min(wallet.Balance, g.TargetAmount);
            double pct = g.TargetAmount > 0 ? Math.Round((double)Math.Max(0, currentSaved) / g.TargetAmount * 100, 1) : 0;
            return new SavingsGoalDto(
                g.Id,
                g.PersonId,
                g.Title,
                g.TargetAmount,
                currentSaved,
                pct,
                g.TargetDate,
                g.Status);
        }).ToList();
    }

    public async Task<SavingsGoalDto> CreateSavingsGoalAsync(CreateSavingsGoalRequest request, CancellationToken ct = default)
    {
        var wallet = await GetKidWalletAsync(request.PersonId, ct);

        var goal = new SavingsGoal
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            WalletAccountId = wallet.WalletAccountId,
            Title = request.Title,
            TargetAmount = request.TargetAmount,
            TargetDate = request.TargetDate,
            Status = GoalStatus.InProgress
        };

        _db.SavingsGoals.Add(goal);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateSavingsGoal", "SavingsGoal", goal.Id.ToString(), $"Created savings goal {goal.Title} ({goal.TargetAmount:N0})", ct);

        long currentSaved = Math.Min(wallet.Balance, goal.TargetAmount);
        double pct = goal.TargetAmount > 0 ? Math.Round((double)Math.Max(0, currentSaved) / goal.TargetAmount * 100, 1) : 0;

        return new SavingsGoalDto(
            goal.Id,
            goal.PersonId,
            goal.Title,
            goal.TargetAmount,
            currentSaved,
            pct,
            goal.TargetDate,
            goal.Status);
    }

    public async Task<IReadOnlyList<WalletRequestDto>> GetWalletRequestsAsync(Guid? personId = null, WalletRequestStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.WalletRequests
            .Include(r => r.Person)
            .AsQueryable();

        if (personId.HasValue)
            query = query.Where(r => r.PersonId == personId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var list = await query.ToListAsync(ct);
        return list.OrderByDescending(r => r.CreatedAt).Select(r => new WalletRequestDto(
            r.Id,
            r.PersonId,
            r.Person?.DisplayName ?? "",
            r.Kind,
            r.Amount,
            r.Reason,
            r.Status,
            r.DecidedBy,
            r.DecidedAt)).ToList();
    }

    public async Task<WalletRequestDto> RequestWalletActionAsync(Guid personId, WalletRequestKind kind, long amount, string reason, CancellationToken ct = default)
    {
        var wallet = await GetKidWalletAsync(personId, ct);
        if (amount > wallet.Balance)
            throw new InvalidOperationException($"Requested amount ({amount:N0}) exceeds current wallet balance ({wallet.Balance:N0}).");

        var req = new WalletRequest
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = personId,
            Kind = kind,
            Amount = amount,
            Reason = reason,
            Status = WalletRequestStatus.Pending
        };

        _db.WalletRequests.Add(req);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("RequestWalletAction", "WalletRequest", req.Id.ToString(), $"Requested {kind} of {amount:N0}: {reason}", ct);

        var person = await _db.People.FindAsync([personId], ct);
        return new WalletRequestDto(
            req.Id,
            req.PersonId,
            person?.DisplayName ?? "",
            req.Kind,
            req.Amount,
            req.Reason,
            req.Status,
            null,
            null);
    }

    public async Task<WalletRequestDto> ApproveWalletRequestAsync(Guid requestId, Guid parentUserId, CancellationToken ct = default)
    {
        var req = await _db.WalletRequests
            .Include(r => r.Person)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new KeyNotFoundException($"Wallet request {requestId} not found.");

        if (req.Status == WalletRequestStatus.Approved)
            throw new InvalidOperationException("Request is already approved.");

        var wallet = await GetKidWalletAsync(req.PersonId, ct);
        if (req.Amount > wallet.Balance)
            throw new InvalidOperationException("Insufficient wallet balance to approve request.");

        req.Status = WalletRequestStatus.Approved;
        req.DecidedBy = parentUserId;
        req.DecidedAt = DateTimeOffset.UtcNow;

        // Post expense from wallet account
        var uncategorized = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "6990" || a.Code == "5503", ct);
        if (uncategorized != null)
        {
            await _financeService.RecordExpenseAsync(new RecordExpenseRequest(
                DateOnly.FromDateTime(DateTime.UtcNow),
                req.Amount,
                uncategorized.Id,
                wallet.WalletAccountId,
                $"Kid wallet {req.Kind}: {req.Reason}",
                PersonId: req.PersonId), ct);
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("ApproveWalletRequest", "WalletRequest", req.Id.ToString(), $"Approved {req.Kind} of {req.Amount:N0}", ct);

        return new WalletRequestDto(
            req.Id,
            req.PersonId,
            req.Person?.DisplayName ?? "",
            req.Kind,
            req.Amount,
            req.Reason,
            req.Status,
            req.DecidedBy,
            req.DecidedAt);
    }

    public async Task<WalletRequestDto> RejectWalletRequestAsync(Guid requestId, Guid parentUserId, CancellationToken ct = default)
    {
        var req = await _db.WalletRequests
            .Include(r => r.Person)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new KeyNotFoundException($"Wallet request {requestId} not found.");

        req.Status = WalletRequestStatus.Rejected;
        req.DecidedBy = parentUserId;
        req.DecidedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("RejectWalletRequest", "WalletRequest", req.Id.ToString(), $"Rejected {req.Kind} of {req.Amount:N0}", ct);

        return new WalletRequestDto(
            req.Id,
            req.PersonId,
            req.Person?.DisplayName ?? "",
            req.Kind,
            req.Amount,
            req.Reason,
            req.Status,
            req.DecidedBy,
            req.DecidedAt);
    }
}
