using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

// Routines & Points
public record RoutineTaskDto(
    Guid Id,
    Guid PersonId,
    string Title,
    string Icon,
    RoutineTimeSlot TimeSlot,
    string Days,
    int Points,
    bool RequiresApproval,
    bool IsActive,
    bool IsCompletedToday,
    CompletionStatus? TodayStatus);

public record CreateRoutineTaskRequest(
    Guid PersonId,
    string Title,
    string Icon = "star",
    RoutineTimeSlot TimeSlot = RoutineTimeSlot.Morning,
    string Days = "Sat,Sun,Mon,Tue,Wed,Thu,Fri",
    int Points = 10,
    bool RequiresApproval = true);

public record RoutineCompletionDto(
    Guid Id,
    Guid RoutineTaskId,
    string TaskTitle,
    DateOnly Date,
    DateTimeOffset CompletedAt,
    Guid? ApprovedBy,
    int PointsAwarded,
    CompletionStatus Status);

public record PointLedgerDto(
    Guid Id,
    Guid PersonId,
    int Delta,
    string Reason,
    string? SourceType,
    Guid? SourceId,
    DateTimeOffset At);

// Rewards
public record RewardDto(
    Guid Id,
    string Title,
    int Cost,
    string Icon,
    bool IsActive);

public record CreateRewardRequest(
    string Title,
    int Cost,
    string Icon = "card_giftcard");

public record RewardRedemptionDto(
    Guid Id,
    Guid RewardId,
    string RewardTitle,
    int Cost,
    string Icon,
    Guid PersonId,
    string PersonName,
    RedemptionStatus Status,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt);

// Homework, Subjects & Exams
public record SubjectDto(
    Guid Id,
    Guid PersonId,
    string Name,
    string? Teacher);

public record CreateSubjectRequest(
    Guid PersonId,
    string Name,
    string? Teacher = null);

public record HomeworkItemDto(
    Guid Id,
    Guid PersonId,
    Guid SubjectId,
    string SubjectName,
    string Title,
    string? Details,
    DateOnly AssignedOn,
    DateOnly DueOn,
    HomeworkStatus Status,
    bool ParentChecked);

public record CreateHomeworkRequest(
    Guid PersonId,
    Guid SubjectId,
    string Title,
    DateOnly DueOn,
    string? Details = null,
    DateOnly? AssignedOn = null);

public record ExamEventDto(
    Guid Id,
    Guid PersonId,
    Guid SubjectId,
    string SubjectName,
    DateOnly Date,
    string Topics,
    decimal? Score);

public record CreateExamEventRequest(
    Guid PersonId,
    Guid SubjectId,
    DateOnly Date,
    string Topics,
    decimal? Score = null);

public record SchoolScheduleDto(
    Guid Id,
    Guid PersonId,
    DayOfWeek DayOfWeek,
    int Period,
    Guid SubjectId,
    string SubjectName,
    TimeOnly StartTime,
    TimeOnly EndTime);

public record CreateSchoolScheduleRequest(
    Guid PersonId,
    DayOfWeek DayOfWeek,
    int Period,
    Guid SubjectId,
    TimeOnly StartTime,
    TimeOnly EndTime);

// Wallet & Allowance
public record KidWalletDto(
    Guid PersonId,
    Guid WalletAccountId,
    string AccountCode,
    string AccountName,
    long Balance,
    WalletMode Mode,
    Guid? BankAccountId,
    Guid? CustodianPersonId);

public record AllowanceRuleDto(
    Guid Id,
    Guid PersonId,
    long Amount,
    Guid? RecurrenceId,
    Guid FromAccountId,
    Guid WalletAccountId,
    bool Active);

public record SetAllowanceRuleRequest(
    Guid PersonId,
    long Amount,
    Guid? RecurrenceId = null,
    Guid FromAccountId = default);

public record SavingsGoalDto(
    Guid Id,
    Guid PersonId,
    string Title,
    long TargetAmount,
    long CurrentSaved,
    double ProgressPercent,
    DateOnly? TargetDate,
    GoalStatus Status);

public record CreateSavingsGoalRequest(
    Guid PersonId,
    string Title,
    long TargetAmount,
    DateOnly? TargetDate = null);

public record WalletRequestDto(
    Guid Id,
    Guid PersonId,
    string PersonName,
    WalletRequestKind Kind,
    long Amount,
    string Reason,
    WalletRequestStatus Status,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt);

public interface IChildrenService
{
    // Routines & Points
    Task<IReadOnlyList<RoutineTaskDto>> GetRoutineTasksAsync(Guid personId, DateOnly? asOfDate = null, CancellationToken ct = default);
    Task<RoutineTaskDto> CreateRoutineTaskAsync(CreateRoutineTaskRequest request, CancellationToken ct = default);
    Task<RoutineCompletionDto> CompleteRoutineTaskAsync(Guid routineTaskId, DateOnly? date = null, CancellationToken ct = default);
    Task<RoutineCompletionDto> ApproveRoutineCompletionAsync(Guid completionId, Guid parentUserId, CancellationToken ct = default);
    Task<RoutineCompletionDto> RejectRoutineCompletionAsync(Guid completionId, Guid parentUserId, CancellationToken ct = default);
    Task<IReadOnlyList<RoutineCompletionDto>> GetPendingApprovalsAsync(CancellationToken ct = default);
    Task<int> GetPointBalanceAsync(Guid personId, CancellationToken ct = default);
    Task<IReadOnlyList<PointLedgerDto>> GetPointHistoryAsync(Guid personId, CancellationToken ct = default);

    // Rewards & Redemptions
    Task<IReadOnlyList<RewardDto>> GetRewardsAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<RewardDto> CreateRewardAsync(CreateRewardRequest request, CancellationToken ct = default);
    Task<RewardRedemptionDto> RedeemRewardAsync(Guid rewardId, Guid personId, CancellationToken ct = default);
    Task<RewardRedemptionDto> ApproveRewardRedemptionAsync(Guid redemptionId, Guid parentUserId, CancellationToken ct = default);
    Task<RewardRedemptionDto> RejectRewardRedemptionAsync(Guid redemptionId, Guid parentUserId, CancellationToken ct = default);
    Task<IReadOnlyList<RewardRedemptionDto>> GetPendingRedemptionsAsync(CancellationToken ct = default);

    // Homework & Subjects
    Task<IReadOnlyList<SubjectDto>> GetSubjectsAsync(Guid personId, CancellationToken ct = default);
    Task<SubjectDto> CreateSubjectAsync(CreateSubjectRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<HomeworkItemDto>> GetHomeworkAsync(Guid personId, HomeworkStatus? status = null, CancellationToken ct = default);
    Task<HomeworkItemDto> CreateHomeworkAsync(CreateHomeworkRequest request, CancellationToken ct = default);
    Task<HomeworkItemDto> UpdateHomeworkStatusAsync(Guid homeworkId, HomeworkStatus status, bool? parentChecked = null, CancellationToken ct = default);

    // Exams & Schedule
    Task<IReadOnlyList<ExamEventDto>> GetExamsAsync(Guid personId, CancellationToken ct = default);
    Task<ExamEventDto> CreateExamAsync(CreateExamEventRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SchoolScheduleDto>> GetSchoolScheduleAsync(Guid personId, CancellationToken ct = default);
    Task<SchoolScheduleDto> CreateSchoolScheduleAsync(CreateSchoolScheduleRequest request, CancellationToken ct = default);

    // Kid Wallet & Allowance
    Task<KidWalletDto> GetKidWalletAsync(Guid personId, CancellationToken ct = default);
    Task<KidWalletDto> SetupKidWalletAsync(Guid personId, WalletMode mode = WalletMode.Virtual, Guid? bankAccountId = null, Guid? custodianPersonId = null, CancellationToken ct = default);
    Task<AllowanceRuleDto?> GetAllowanceRuleAsync(Guid personId, CancellationToken ct = default);
    Task<AllowanceRuleDto> SetAllowanceRuleAsync(SetAllowanceRuleRequest request, CancellationToken ct = default);
    Task ProcessAllowancePayoutAsync(Guid allowanceRuleId, CancellationToken ct = default);

    // Goals & Requests
    Task<IReadOnlyList<SavingsGoalDto>> GetSavingsGoalsAsync(Guid personId, CancellationToken ct = default);
    Task<SavingsGoalDto> CreateSavingsGoalAsync(CreateSavingsGoalRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<WalletRequestDto>> GetWalletRequestsAsync(Guid? personId = null, WalletRequestStatus? status = null, CancellationToken ct = default);
    Task<WalletRequestDto> RequestWalletActionAsync(Guid personId, WalletRequestKind kind, long amount, string reason, CancellationToken ct = default);
    Task<WalletRequestDto> ApproveWalletRequestAsync(Guid requestId, Guid parentUserId, CancellationToken ct = default);
    Task<WalletRequestDto> RejectWalletRequestAsync(Guid requestId, Guid parentUserId, CancellationToken ct = default);
}
