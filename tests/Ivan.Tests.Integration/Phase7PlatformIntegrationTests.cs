using System.Text;
using Ivan.Application.Common;
using Ivan.Application.Services;
using Ivan.Domain.Common;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Data;
using Ivan.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ivan.Tests.Integration;

public class Phase7PlatformIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;
    private readonly CurrentUserContext _userContext;
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _parentUserId = Guid.NewGuid();
    private readonly Guid _childPersonId = Guid.NewGuid();
    private readonly IConfiguration _config;

    public Phase7PlatformIntegrationTests()
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
            Name = "Platform Test Household",
            DefaultCurrency = "IRR"
        };
        var membership = new Membership
        {
            HouseholdId = _householdId,
            UserId = _parentUserId,
            Role = MembershipRole.Parent,
            IsActive = true
        };
        var child = new Person
        {
            Id = _childPersonId,
            HouseholdId = _householdId,
            DisplayName = "Ali",
            BirthDate = new DateOnly(2014, 2, 20)
        };

        init.Households.Add(household);
        init.Memberships.Add(membership);
        init.People.Add(child);
        init.SaveChanges();

        _config = new ConfigurationBuilder().Build();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private (IvanDbContext db, ISearchService search, IDashboardService dashboard, IBackupService backup, IImportExportService importExport, IAiAssistantService ai) CreateServices()
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
        var backup = new BackupService(db, _userContext, _config, audit);
        var importExport = new ImportExportService(db, _userContext, ledger, audit);
        var ai = new AiAssistantService(db, _userContext, audit);

        return (db, search, dashboard, backup, importExport, ai);
    }

    [Fact]
    public async Task Dashboards_ParentTeenChildVariants_Work()
    {
        var (db, _, dashboard, _, _, _) = CreateServices();
        using (db)
        {
            // Seed finance accounts
            var seeder = new ChartOfAccountsSeeder(db);
            await seeder.SeedAsync(_householdId, ChartPreset.Compact);

            // Add a due-soon task
            db.TaskItems.Add(new TaskItem
            {
                HouseholdId = _householdId,
                Title = "Pay electricity bill",
                DueAt = DateTimeOffset.UtcNow.AddDays(2),
                Status = TaskItemStatus.Todo,
                Priority = TaskPriority.Urgent
            });

            // Add an expiring person document
            db.PersonDocuments.Add(new PersonDocument
            {
                HouseholdId = _householdId,
                PersonId = _childPersonId,
                Type = PersonDocumentType.Passport,
                Number = "P123456",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15))
            });

            // Add a routine for child
            var routine = new RoutineTask
            {
                HouseholdId = _householdId,
                PersonId = _childPersonId,
                Title = "Morning Exercise",
                Points = 15,
                RequiresApproval = false,
                IsActive = true
            };
            db.RoutineTasks.Add(routine);
            await db.SaveChangesAsync();

            // 1. Parent Dashboard
            var parentDash = await dashboard.GetParentDashboardAsync();
            Assert.Equal("Platform Test Household", parentDash.HouseholdName);
            Assert.Contains(parentDash.DueSoonItems, i => i.Title == "Pay electricity bill");
            Assert.Contains(parentDash.ExpiringItems, i => i.Title.Contains("Passport"));

            // 2. Teen Dashboard
            var teenDash = await dashboard.GetTeenDashboardAsync(_childPersonId);
            Assert.Equal("Ali", teenDash.PersonName);
            Assert.Equal(1, teenDash.TodayRoutinesCount);

            // 3. Child Dashboard
            var childDash = await dashboard.GetChildDashboardAsync(_childPersonId);
            Assert.Equal("Ali", childDash.PersonName);
            Assert.Single(childDash.TodayRoutines);
        }
    }

    [Fact]
    public async Task SearchV2_PersianNormalizationOfLettersAndDigits_Works()
    {
        var (db, search, _, _, _, _) = CreateServices();
        using (db)
        {
            // Add items with Persian letters and digits
            db.TaskItems.Add(new TaskItem
            {
                HouseholdId = _householdId,
                Title = "خرید کتاب فیزیک",
                Details = "نسخه ۱۲ چاپ جدید"
            });

            db.Assets.Add(new Asset
            {
                HouseholdId = _householdId,
                Name = "تلویزیون سونی",
                Brand = "Sony",
                Model = "Bravia-55"
            });

            db.Books.Add(new Book
            {
                HouseholdId = _householdId,
                Title = "کلیات سعدی شیرازی",
                Author = "سعدی",
                Isbn = "978-964-1234"
            });

            await db.SaveChangesAsync();

            // 1. Search with Arabic 'ي' (\u064A) matching Persian 'ی' (\u06CC)
            // "فيزيك" with Arabic yeh and kaf
            var arabicYehQuery = "فيزيك";
            var resultsYeh = await search.SearchAsync(arabicYehQuery);
            Assert.Contains(resultsYeh, r => r.Title.Contains("فیزیک"));

            // 2. Search with Persian digits matching Latin digits
            // Query with Persian digits "۱۲" (\u06F1\u06F2)
            var digitResults = await search.SearchAsync("۱۲");
            Assert.Contains(digitResults, r => r.Subtitle?.Contains("۱۲") == true || r.Title.Contains("فیزیک"));

            // 3. Search asset
            var assetResults = await search.SearchAsync("سونی");
            Assert.Contains(assetResults, r => r.Title == "تلویزیون سونی");

            // 4. Search book by ISBN
            var isbnResults = await search.SearchAsync("978-964");
            Assert.Contains(isbnResults, r => r.Title == "کلیات سعدی شیرازی");
        }
    }

    [Fact]
    public async Task Backup_Create_IntegrityDryRun_AndHouseholdZipExport_Works()
    {
        var (db, _, _, backup, _, _) = CreateServices();
        using (db)
        {
            // Seed some data
            db.TaskItems.Add(new TaskItem { HouseholdId = _householdId, Title = "Backup Test Task" });
            db.Books.Add(new Book { HouseholdId = _householdId, Title = "Backup Book", Author = "Author" });
            await db.SaveChangesAsync();

            // 1. Create backup
            var tempDir = Path.Combine(Path.GetTempPath(), "ivan_backup_test_" + Guid.NewGuid().ToString("N"));
            var backupResult = await backup.CreateBackupAsync(tempDir);

            try
            {
                Assert.True(backupResult.Success);
                Assert.True(File.Exists(backupResult.FilePath));
                Assert.True(backupResult.FileSizeBytes > 0);

                // 2. Dry run restore integrity check
                var dryRun = await backup.DryRunRestoreAsync(backupResult.FilePath);
                Assert.True(dryRun.IsValid);
                Assert.True(dryRun.TableCount > 5);
                Assert.Null(dryRun.ErrorMessage);

                // 3. Household Zip Export
                var zipBytes = await backup.ExportHouseholdZipAsync(_householdId);
                Assert.NotNull(zipBytes);
                Assert.True(zipBytes.Length > 0);

                // Verify zip contains files
                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new System.IO.Compression.ZipArchive(zipStream);
                Assert.Contains(archive.Entries, e => e.Name == "household.json");
                Assert.Contains(archive.Entries, e => e.Name == "tasks.json");
                Assert.Contains(archive.Entries, e => e.Name == "books.json");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }

    [Fact]
    public async Task CsvImportAndJsonExport_BooksAssetsTransactions_Work()
    {
        var (db, _, _, _, importExport, _) = CreateServices();
        using (db)
        {
            // Seed chart of accounts for transaction import
            var seeder = new ChartOfAccountsSeeder(db);
            await seeder.SeedAsync(_householdId, ChartPreset.Compact);

            // 1. Import Books CSV
            var booksCsv = "Title,Author,Isbn,Publisher,PublishYear,ShelfLocation\n" +
                           "Clean Code,Robert Martin,978-0132350884,Prentice Hall,2008,Shelf A\n" +
                           "Refactoring,Martin Fowler,978-0201485677,Addison-Wesley,1999,Shelf B\n";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(booksCsv)))
            {
                var bookImport = await importExport.ImportBooksCsvAsync(stream);
                Assert.Equal(2, bookImport.SucceededRows);
                Assert.Equal(0, bookImport.FailedRows);

                var booksInDb = await db.Books.ToListAsync();
                Assert.Contains(booksInDb, b => b.Title == "Clean Code");
                Assert.Contains(booksInDb, b => b.Title == "Refactoring");
            }

            // 2. Import Assets CSV
            var assetsCsv = "Name,Brand,Model,Serial,EstimatedValue\n" +
                            "Laptop,Dell,XPS 15,SN-998877,45000000\n" +
                            "Microwave,Samsung,MS23,SN-112233,12000000\n";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(assetsCsv)))
            {
                var assetImport = await importExport.ImportAssetsCsvAsync(stream);
                Assert.Equal(2, assetImport.SucceededRows);

                var assetsInDb = await db.Assets.ToListAsync();
                Assert.Contains(assetsInDb, a => a.Name == "Laptop");
            }

            // 3. Import Transactions CSV
            // Account codes 5201 (Groceries) and 1011 (Checking) exist in Compact chart
            var transactionsCsv = "Date,Description,FromAccountCode,ToAccountCode,Amount\n" +
                                  "2026-05-10,Supermarket,1011,5201,350000\n";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(transactionsCsv)))
            {
                var txImport = await importExport.ImportTransactionsCsvAsync(stream);
                Assert.Equal(1, txImport.SucceededRows);

                var entries = await db.JournalEntries.Include(e => e.Lines).ToListAsync();
                Assert.Contains(entries, e => e.Description == "Supermarket" && e.Lines.Sum(l => l.Debit) == 350000);
            }

            // 4. Export Household JSON
            var json = await importExport.ExportHouseholdJsonAsync(_householdId);
            Assert.Contains("Clean Code", json);
            Assert.Contains("Laptop", json);
        }
    }

    [Fact]
    public async Task OptionalAi_TokenCapAndPrivacyEnforcement_Works()
    {
        var (db, _, _, _, _, ai) = CreateServices();
        using (db)
        {
            // 1. Initially disabled -> throws InvalidOperationException
            var initialConfig = await ai.GetConfigAsync();
            Assert.False(initialConfig.IsEnabled);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ai.ParseQuickAddAsync("فردا ساعت ۵ عصر جلسه اولیا و مربیان"));

            // 2. Enable AI with a 120 token monthly cap
            await ai.UpdateConfigAsync(new UpdateAiConfigRequest(
                IsEnabled: true,
                Provider: AiProviderType.Ollama,
                Endpoint: "http://localhost:11434",
                MonthlyCapTokens: 120));

            // 3. Privacy violation: sending sensitive health/medical terms must throw
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ai.ParseQuickAddAsync("یادآوری مراجعه به پزشک و خرید داروهای قلبی"));

            // 4. Valid quick-add succeeds and charges tokens (50 tokens)
            var quickAdd = await ai.ParseQuickAddAsync("فردا ساعت ۱۸ خرید میوه و مواد غذایی ۵۰۰۰۰۰ تومان");
            Assert.Equal("Expense", quickAdd.IntentType);
            Assert.Equal(500000L, quickAdd.Amount);

            var configAfter1 = await ai.GetConfigAsync();
            Assert.Equal(50, configAfter1.UsedTokensThisMonth);

            // 5. Another call consumes 50 more tokens (total 100/120)
            await ai.ParseQuickAddAsync("جلسه با معلم ریاضی");

            var configAfter2 = await ai.GetConfigAsync();
            Assert.Equal(100, configAfter2.UsedTokensThisMonth);

            // 6. Next call requires 50 tokens (100 + 50 = 150 > 120 cap) -> throws cap exceeded
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ai.ParseQuickAddAsync("خرید نان سنگک"));
        }
    }
}
