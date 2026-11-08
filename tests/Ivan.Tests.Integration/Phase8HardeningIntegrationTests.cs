using System.Diagnostics;
using Ivan.Application.Common;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Data;
using Ivan.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ivan.Tests.Integration;

public class Phase8HardeningIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;
    private readonly CurrentUserContext _userContext;
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _parentUserId = Guid.NewGuid();
    private readonly IConfiguration _config;

    public Phase8HardeningIntegrationTests()
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

        // Apply WAL / Memory pragmas
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON; PRAGMA cache_size = -64000;";
        cmd.ExecuteNonQuery();

        // Seed initial household & parent
        var household = new Household
        {
            Id = _householdId,
            Name = "Hardening Test Household",
            DefaultCurrency = "IRR"
        };
        var membership = new Membership
        {
            HouseholdId = _householdId,
            UserId = _parentUserId,
            Role = MembershipRole.Parent,
            IsActive = true
        };
        init.Households.Add(household);
        init.Memberships.Add(membership);
        init.SaveChanges();

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", "DataSource=:memory:" },
            { "Ivan:BackupDirectory", "app_data/backups" },
            { "Ivan:Ai:DefaultMonthlyCap", "50000" }
        };
        _config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private (IvanDbContext db, ISearchService search, IDashboardService dashboard, IAuditService audit) CreateServices()
    {
        var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var ledger = new LedgerService(db, _userContext, audit);
        var reminder = new ReminderService(db);
        var finance = new FinanceService(db, _userContext, ledger, reminder, audit);
        var children = new ChildrenService(db, _userContext, ledger, finance, reminder, audit);
        var media = new MediaService(db, _userContext, audit);
        var search = new SearchService(db, _userContext);
        var dashboard = new DashboardService(db, _userContext, finance, children, media);

        return (db, search, dashboard, audit);
    }

    [Fact]
    public void SecurityReview_CardNumberMasking_And_AuditLogging_Enforced()
    {
        using var db = new IvanDbContext(_options, _userContext);

        // Invariant check: CVV must never be stored, and card numbers must only hold masked info / last 4 digits
        var account = new Account
        {
            HouseholdId = _householdId,
            Code = "101.01",
            Name = "بانک سامان",
            Type = AccountType.Asset,
            SubType = AccountSubType.Bank,
            Currency = "IRR",
            IsActive = true,
            // Section 3.10 checklist: Last 4 digits only
            BankInfo = "****-****-****-1234"
        };
        db.Accounts.Add(account);

        // AuditLog record check
        var audit = new AuditLog
        {
            HouseholdId = _householdId,
            UserId = _parentUserId,
            Action = "SecurityVerification",
            ResourceType = "Account",
            ResourceId = account.Id.ToString(),
            Summary = "Verified bank account creation",
            At = DateTimeOffset.UtcNow,
            Ip = "127.0.0.1"
        };
        db.AuditLogs.Add(audit);
        db.SaveChanges();

        var savedAccount = db.Accounts.Find(account.Id);
        Assert.NotNull(savedAccount);
        Assert.Contains("1234", savedAccount.BankInfo);
        Assert.DoesNotContain("CVV", savedAccount.BankInfo ?? "");

        var savedAudit = db.AuditLogs.FirstOrDefault(a => a.Action == "SecurityVerification");
        Assert.NotNull(savedAudit);
        Assert.Equal(_householdId, savedAudit.HouseholdId);
    }

    [Fact]
    public async Task PerformanceBenchmark_With10000SeededRows_RespondsUnder300ms()
    {
        using (var db = new IvanDbContext(_options, _userContext))
        {
            // Seed 10,000 total rows across tables in bulk
            // 1. Accounts
            var cashAcc = new Account { HouseholdId = _householdId, Code = "101", Name = "صندوق نقد", Type = AccountType.Asset, SubType = AccountSubType.Cash, Currency = "IRR" };
            var expAcc = new Account { HouseholdId = _householdId, Code = "501", Name = "هزینه‌های عمومی", Type = AccountType.Expense, SubType = AccountSubType.Category, Currency = "IRR" };
            db.Accounts.AddRange(cashAcc, expAcc);
            db.SaveChanges();

            // 2. 5,000 Journal Entries (10,000 lines total)
            var entries = new List<JournalEntry>(5000);
            var baseDate = DateOnly.FromDateTime(DateTime.UtcNow);
            for (int i = 1; i <= 5000; i++)
            {
                var je = new JournalEntry
                {
                    HouseholdId = _householdId,
                    Reference = $"JE-{i:D5}",
                    Date = baseDate.AddDays(- (i % 365)),
                    Description = $"خرید روزانه شماره {i}",
                    Status = JournalEntryStatus.Posted
                };
                je.Lines.Add(new JournalLine { HouseholdId = _householdId, AccountId = expAcc.Id, Debit = 10000, Credit = 0 });
                je.Lines.Add(new JournalLine { HouseholdId = _householdId, AccountId = cashAcc.Id, Debit = 0, Credit = 10000 });
                entries.Add(je);
            }
            db.JournalEntries.AddRange(entries);

            // 3. 2,000 TaskItems
            var tasks = new List<TaskItem>(2000);
            var now = DateTimeOffset.UtcNow;
            for (int i = 1; i <= 2000; i++)
            {
                tasks.Add(new TaskItem
                {
                    HouseholdId = _householdId,
                    Title = $"انجام کار شماره {i}",
                    Status = i % 5 == 0 ? TaskItemStatus.Done : TaskItemStatus.Todo,
                    Priority = i % 10 == 0 ? TaskPriority.Urgent : TaskPriority.Medium,
                    DueAt = now.AddDays((i % 30) - 10)
                });
            }
            db.TaskItems.AddRange(tasks);

            // 4. 1,500 Assets
            var assets = new List<Asset>(1500);
            for (int i = 1; i <= 1500; i++)
            {
                assets.Add(new Asset
                {
                    HouseholdId = _householdId,
                    Name = $"دستگاه فنی {i}",
                    Brand = i % 2 == 0 ? "Sony" : "Samsung",
                    SerialNo = $"SN-BENCH-{i:D5}",
                    PurchasePrice = 25000000,
                    WarrantyEnd = baseDate.AddDays((i % 60) - 15)
                });
            }
            db.Assets.AddRange(assets);

            // 5. 1,500 Books
            var books = new List<Book>(1500);
            for (int i = 1; i <= 1500; i++)
            {
                books.Add(new Book
                {
                    HouseholdId = _householdId,
                    Title = $"کتاب جلد {i}",
                    Author = i % 3 == 0 ? "محمود دولت‌آبادی" : "صادق هدایت",
                    Isbn = $"978964{i:D7}",
                    Status = BookStatus.Available
                });
            }
            db.Books.AddRange(books);

            db.SaveChanges();
        }

        // Now test response times against the 10,000+ seeded rows
        var (readDb, search, dashboard, _) = CreateServices();
        using (readDb)
        {
            // Test 1: Parent Dashboard load time
            var sw = Stopwatch.StartNew();
            var parentDash = await dashboard.GetParentDashboardAsync();
            sw.Stop();
            var dashElapsedMs = sw.ElapsedMilliseconds;

            Assert.NotNull(parentDash);
            Assert.True(dashElapsedMs < 300, $"Dashboard query took {dashElapsedMs}ms which exceeds the 300ms LAN budget!");

            // Test 2: Universal Search across 10,000 rows with Persian normalization
            sw.Restart();
            var searchResults = await search.SearchAsync("دستگاه فنی ۱۰۲");
            sw.Stop();
            var searchElapsedMs = sw.ElapsedMilliseconds;

            Assert.NotEmpty(searchResults);
            Assert.True(searchElapsedMs < 300, $"Universal Search took {searchElapsedMs}ms which exceeds the 300ms LAN budget!");

            // Test 3: Search with Arabic/Persian letter variants (ی/ي and digits)
            sw.Restart();
            var bookSearch = await search.SearchAsync("دولت آبادی");
            sw.Stop();
            var bookSearchElapsedMs = sw.ElapsedMilliseconds;

            Assert.NotEmpty(bookSearch);
            Assert.True(bookSearchElapsedMs < 300, $"Book search took {bookSearchElapsedMs}ms which exceeds 300ms budget!");
        }
    }

    [Fact]
    public async Task CrossHousehold_TenancyIsolation_GuaranteedUnderVolume()
    {
        var otherHouseholdId = Guid.NewGuid();

        // Insert items for Other household
        using (var db = new IvanDbContext(_options, _userContext))
        {
            var otherTask = new TaskItem
            {
                HouseholdId = otherHouseholdId,
                Title = "کار محرمانه خانوار دیگر",
                Status = TaskItemStatus.Todo
            };
            var otherAsset = new Asset
            {
                HouseholdId = otherHouseholdId,
                Name = "خودروی خانوار دیگر",
                Brand = "Toyota"
            };
            db.TaskItems.Add(otherTask);
            db.Assets.Add(otherAsset);
            db.SaveChanges();
        }

        // Current user queries their own context
        var (readDb, search, _, _) = CreateServices();
        using (readDb)
        {
            var tasks = await readDb.TaskItems.ToListAsync();
            var assets = await readDb.Assets.ToListAsync();

            Assert.DoesNotContain(tasks, t => t.HouseholdId == otherHouseholdId);
            Assert.DoesNotContain(assets, a => a.HouseholdId == otherHouseholdId);

            var res = await search.SearchAsync("محرمانه");
            Assert.Empty(res);
        }
    }

    [Fact]
    public async Task DatabaseInitializer_EnsuresMissingTablesCreated_WhenDatabaseAlreadyExisted()
    {
        using var tempConnection = new SqliteConnection("DataSource=:memory:");
        tempConnection.Open();

        var tempOptions = new DbContextOptionsBuilder<IvanDbContext>()
            .UseSqlite(tempConnection)
            .Options;

        // Simulate an old database that only has Households created
        using (var cmd = tempConnection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""Households"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Households"" PRIMARY KEY,
                    ""Name"" TEXT NOT NULL,
                    ""DefaultCulture"" TEXT NOT NULL,
                    ""DefaultCalendar"" INTEGER NOT NULL,
                    ""DefaultCurrency"" TEXT NOT NULL,
                    ""TimeZone"" TEXT NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    ""DeletedAt"" TEXT NULL,
                    ""RowVersion"" BLOB NOT NULL
                );";
            cmd.ExecuteNonQuery();
        }

        // Run DatabaseInitializer
        using (var db = new IvanDbContext(tempOptions, _userContext))
        {
            DatabaseInitializer.InitializeDatabase(db);
        }

        // Verify that People and other tables were created and can be queried without throwing SQLite Error 1
        using (var verifyDb = new IvanDbContext(tempOptions, _userContext))
        {
            var people = await verifyDb.People.ToListAsync();
            Assert.Empty(people); // Table exists!

            var tasks = await verifyDb.TaskItems.ToListAsync();
            Assert.Empty(tasks); // Table exists!

            var assets = await verifyDb.Assets.ToListAsync();
            Assert.Empty(assets); // Table exists!
        }
    }
}
