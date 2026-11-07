using Ivan.Application.Common;
using Ivan.Application.Services;
using Ivan.Domain.Common;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Data;
using Ivan.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ivan.Tests.Integration;

public class Phase5ChildrenIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;
    private readonly CurrentUserContext _userContext;
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _parentUserId = Guid.NewGuid();
    private readonly Guid _childPersonId = Guid.NewGuid();

    public Phase5ChildrenIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<IvanDbContext>()
            .UseSqlite(_connection)
            .Options;

        _userContext = new CurrentUserContext();
        _userContext.SetContext(_parentUserId, _householdId, MembershipRole.Parent);

        using var init = new IvanDbContext(_options, _userContext);
        init.Database.EnsureCreated();

        // Seed household & membership
        var household = new Household
        {
            Id = _householdId,
            Name = "Children Test Household",
            DefaultCurrency = "IRR"
        };
        var membership = new Membership
        {
            HouseholdId = _householdId,
            UserId = _parentUserId,
            Role = MembershipRole.Parent,
            IsActive = true
        };
        var childPerson = new Person
        {
            Id = _childPersonId,
            HouseholdId = _householdId,
            DisplayName = "Ali",
            BirthDate = new DateOnly(2015, 6, 15)
        };

        init.Households.Add(household);
        init.Memberships.Add(membership);
        init.People.Add(childPerson);
        init.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private (IvanDbContext db, IChildrenService children, ILedgerService ledger, IFinanceService finance) CreateServices()
    {
        var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var ledger = new LedgerService(db, _userContext, audit);
        var reminder = new ReminderService(db);
        var finance = new FinanceService(db, _userContext, ledger, reminder, audit);
        var children = new ChildrenService(db, _userContext, ledger, finance, reminder, audit);
        return (db, children, ledger, finance);
    }

    [Fact]
    public async Task RoutinesAndPoints_CompletionAndApprovalFlow_Works()
    {
        var (db, children, _, _) = CreateServices();
        using (db)
        {
            // 1. Create a routine task
            var taskRequest = new CreateRoutineTaskRequest(
                PersonId: _childPersonId,
                Title: "Brush Teeth Morning",
                Icon: "brush",
                TimeSlot: RoutineTimeSlot.Morning,
                Days: "Sat,Sun,Mon,Tue,Wed,Thu,Fri",
                Points: 20,
                RequiresApproval: true);

            var createdTask = await children.CreateRoutineTaskAsync(taskRequest);
            Assert.NotNull(createdTask);
            Assert.Equal("Brush Teeth Morning", createdTask.Title);
            Assert.Equal(20, createdTask.Points);

            // Initial points balance should be 0
            var initialBal = await children.GetPointBalanceAsync(_childPersonId);
            Assert.Equal(0, initialBal);

            // 2. Child completes the task today
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var completion = await children.CompleteRoutineTaskAsync(createdTask.Id, today);
            Assert.Equal(CompletionStatus.PendingApproval, completion.Status);
            Assert.Equal(0, completion.PointsAwarded);

            // Pending approvals should contain this
            var pending = await children.GetPendingApprovalsAsync();
            Assert.Contains(pending, p => p.Id == completion.Id);

            // Points balance should still be 0 before approval
            var balanceBeforeApproval = await children.GetPointBalanceAsync(_childPersonId);
            Assert.Equal(0, balanceBeforeApproval);

            // 3. Parent approves completion
            var approved = await children.ApproveRoutineCompletionAsync(completion.Id, _parentUserId);
            Assert.Equal(CompletionStatus.Approved, approved.Status);
            Assert.Equal(20, approved.PointsAwarded);

            // Points balance should now be 20
            var balanceAfterApproval = await children.GetPointBalanceAsync(_childPersonId);
            Assert.Equal(20, balanceAfterApproval);

            var history = await children.GetPointHistoryAsync(_childPersonId);
            Assert.Single(history);
            Assert.Equal(20, history[0].Delta);

            // 4. Test Reject flow for yesterday
            var yesterday = today.AddDays(-1);
            var completion2 = await children.CompleteRoutineTaskAsync(createdTask.Id, yesterday);
            var rejected = await children.RejectRoutineCompletionAsync(completion2.Id, _parentUserId);
            Assert.Equal(CompletionStatus.Rejected, rejected.Status);
            Assert.Equal(0, rejected.PointsAwarded);

            // Balance stays 20
            var finalBal = await children.GetPointBalanceAsync(_childPersonId);
            Assert.Equal(20, finalBal);
        }
    }

    [Fact]
    public async Task RewardsAndRedemptions_FlowWithPointDeduction_Works()
    {
        var (db, children, _, _) = CreateServices();
        using (db)
        {
            // Seed 50 points to child
            var task = await children.CreateRoutineTaskAsync(new CreateRoutineTaskRequest(
                _childPersonId, "Clean Room", Points: 50, RequiresApproval: false));
            await children.CompleteRoutineTaskAsync(task.Id, DateOnly.FromDateTime(DateTime.UtcNow));

            var bal = await children.GetPointBalanceAsync(_childPersonId);
            Assert.Equal(50, bal);

            // 1. Create rewards
            var reward1 = await children.CreateRewardAsync(new CreateRewardRequest("1 Hour Video Game", 30));
            var reward2 = await children.CreateRewardAsync(new CreateRewardRequest("Expensive Trip", 100));

            // 2. Child cannot redeem reward exceeding balance
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                children.RedeemRewardAsync(reward2.Id, _childPersonId));

            // 3. Redeem reward 1 (cost 30)
            var redemption = await children.RedeemRewardAsync(reward1.Id, _childPersonId);
            Assert.Equal(RedemptionStatus.Requested, redemption.Status);

            var pendingRedemptions = await children.GetPendingRedemptionsAsync();
            Assert.Contains(pendingRedemptions, r => r.Id == redemption.Id);

            // Points balance before approval is still 50
            Assert.Equal(50, await children.GetPointBalanceAsync(_childPersonId));

            // 4. Parent approves redemption
            var approved = await children.ApproveRewardRedemptionAsync(redemption.Id, _parentUserId);
            Assert.Equal(RedemptionStatus.Approved, approved.Status);

            // Points balance should now be 20 (50 - 30)
            var balAfterRedemption = await children.GetPointBalanceAsync(_childPersonId);
            Assert.Equal(20, balAfterRedemption);
        }
    }

    [Fact]
    public async Task HomeworkAndExams_Workflow_Works()
    {
        var (db, children, _, _) = CreateServices();
        using (db)
        {
            // 1. Subject
            var subject = await children.CreateSubjectAsync(new CreateSubjectRequest(_childPersonId, "Mathematics", "Mr. Moradi"));
            Assert.Equal("Mathematics", subject.Name);

            // 2. Homework
            var dueOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
            var hw = await children.CreateHomeworkAsync(new CreateHomeworkRequest(
                _childPersonId,
                subject.Id,
                "Exercises 1 to 10 on page 42",
                dueOn,
                "Show all working steps"));

            Assert.Equal(HomeworkStatus.Assigned, hw.Status);
            Assert.False(hw.ParentChecked);

            // Check reminders generated
            var reminders = await db.Reminders.Where(r => r.HouseholdId == _householdId).ToListAsync();
            Assert.Contains(reminders, r => r.Title.Contains("Exercises 1 to 10"));

            // Child completes and parent reviews homework
            var completed = await children.UpdateHomeworkStatusAsync(hw.Id, HomeworkStatus.Completed);
            Assert.Equal(HomeworkStatus.Completed, completed.Status);

            // Parent checks off / reviews
            var reviewed = await children.UpdateHomeworkStatusAsync(hw.Id, HomeworkStatus.Reviewed, parentChecked: true);
            Assert.Equal(HomeworkStatus.Reviewed, reviewed.Status);
            Assert.True(reviewed.ParentChecked);

            // 3. Exam
            var examDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
            var exam = await children.CreateExamAsync(new CreateExamEventRequest(
                _childPersonId,
                subject.Id,
                examDate,
                "Midterm: Algebra & Geometry",
                Score: 19.5m));

            Assert.Equal(19.5m, exam.Score);
            var exams = await children.GetExamsAsync(_childPersonId);
            Assert.Single(exams);

            // 4. School Schedule
            var schedule = await children.CreateSchoolScheduleAsync(new CreateSchoolScheduleRequest(
                _childPersonId,
                DayOfWeek.Saturday,
                1,
                subject.Id,
                new TimeOnly(8, 0),
                new TimeOnly(9, 30)));

            Assert.Equal(1, schedule.Period);
            var allSchedules = await children.GetSchoolScheduleAsync(_childPersonId);
            Assert.Single(allSchedules);
        }
    }

    [Fact]
    public async Task KidWallet_AllowanceAndGoalWithdrawal_Workflow()
    {
        var (db, children, ledger, finance) = CreateServices();
        using (db)
        {
            // Seed chart of accounts
            var seeder = new ChartOfAccountsSeeder(db);
            await seeder.SeedAsync(_householdId, ChartPreset.Compact);

            var accounts = await finance.GetAccountsAsync();
            var bankAccount = accounts.First(a => a.Type == AccountType.Asset && a.SubType == AccountSubType.Bank);

            // Deposit initial cash into bank
            var equityAccount = accounts.First(a => a.Type == AccountType.Equity);
            await ledger.PostEntryAsync(new CreateJournalEntryRequest(
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Initial Capital Deposit",
                new List<JournalLineItem>
                {
                    new(bankAccount.Id, 2_000_000, 0),
                    new(equityAccount.Id, 0, 2_000_000)
                }));

            // 1. Setup Kid Wallet
            var wallet = await children.SetupKidWalletAsync(_childPersonId, WalletMode.Virtual);
            Assert.NotNull(wallet);
            Assert.Equal(0, wallet.Balance);

            // 2. Set Allowance Rule
            var rule = await children.SetAllowanceRuleAsync(new SetAllowanceRuleRequest(
                _childPersonId,
                Amount: 200_000,
                RecurrenceId: null,
                FromAccountId: bankAccount.Id));

            Assert.NotNull(rule);
            Assert.Equal(200_000, rule.Amount);

            // Process payout
            await children.ProcessAllowancePayoutAsync(rule.Id);

            // Verify wallet received money
            var updatedWallet = await children.GetKidWalletAsync(_childPersonId);
            Assert.Equal(200_000, updatedWallet.Balance);

            // Bank balance decreased by 200,000
            var bankBal = await ledger.GetAccountBalanceAsync(bankAccount.Id);
            Assert.Equal(1_800_000, bankBal);

            // 3. Savings Goal
            var goal = await children.CreateSavingsGoalAsync(new CreateSavingsGoalRequest(
                _childPersonId,
                "New Bicycle",
                400_000,
                DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2))));

            Assert.Equal(200_000, goal.CurrentSaved);
            Assert.Equal(50.0, goal.ProgressPercent);

            // 4. Wallet Withdrawal Request
            var req = await children.RequestWalletActionAsync(
                _childPersonId,
                WalletRequestKind.Withdrawal,
                50_000,
                "Toy car purchase");

            Assert.Equal(WalletRequestStatus.Pending, req.Status);

            // Parent approves request
            var approvedReq = await children.ApproveWalletRequestAsync(req.Id, _parentUserId);
            Assert.Equal(WalletRequestStatus.Approved, approvedReq.Status);

            // Kid wallet balance should now be 150,000 (200,000 - 50,000)
            var finalWallet = await children.GetKidWalletAsync(_childPersonId);
            Assert.Equal(150_000, finalWallet.Balance);
        }
    }
}
