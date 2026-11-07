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

public class Phase3IntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;
    private readonly CurrentUserContext _userContext;
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public Phase3IntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<IvanDbContext>()
            .UseSqlite(_connection)
            .Options;

        _userContext = new CurrentUserContext();
        _userContext.SetContext(_userId, _householdId, MembershipRole.Parent);

        using var init = new IvanDbContext(_options, _userContext);
        init.Database.EnsureCreated();

        // Seed household & membership (Parent role)
        var household = new Household
        {
            Id = _householdId,
            Name = "Integration Test Household"
        };
        var membership = new Membership
        {
            HouseholdId = _householdId,
            UserId = _userId,
            Role = MembershipRole.Parent,
            IsActive = true
        };
        init.Households.Add(household);
        init.Memberships.Add(membership);
        init.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task HomeInfo_UtilityAndMeterReadings_WorkCorrectly()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var homeInfo = new HomeInfoService(db, _userContext, audit);

        var property = new Property
        {
            HouseholdId = _householdId,
            Name = "Tehran Main Flat",
            Type = PropertyType.Home
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        // Create utility account
        var account = await homeInfo.CreateUtilityAccountAsync(new CreateUtilityAccountRequest(
            property.Id,
            UtilityKind.Electricity,
            "Tavanir",
            "BILL-12345",
            "Main breaker in corridor"));

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("Tavanir", account.Provider);

        // Record meter readings
        var reading1 = await homeInfo.RecordMeterReadingAsync(new RecordMeterReadingRequest(
            account.Id,
            new DateOnly(2026, 1, 1),
            10500.5m,
            "kWh"));

        var reading2 = await homeInfo.RecordMeterReadingAsync(new RecordMeterReadingRequest(
            account.Id,
            new DateOnly(2026, 2, 1),
            11200.0m,
            "kWh"));

        var accounts = await homeInfo.GetUtilityAccountsAsync(property.Id);
        Assert.Single(accounts);
        Assert.Equal(2, accounts[0].ReadingCount);
        Assert.Equal(11200.0m, accounts[0].LatestReadingValue);
        Assert.Equal(new DateOnly(2026, 2, 1), accounts[0].LatestReadingDate);

        // Contacts
        var contact = await homeInfo.CreateContactAsync(new CreateContactRequest(
            "Emergency Electrician",
            ContactCategory.Emergency,
            "09120000000",
            "elec@example.com",
            "Available 24/7",
            property.Id));

        var contacts = await homeInfo.GetContactsAsync(property.Id, ContactCategory.Emergency);
        Assert.Single(contacts);
        Assert.Equal("Emergency Electrician", contacts[0].Name);
    }

    [Fact]
    public async Task AssetAndMaintenance_Completion_AdvancesNextDueAndCreatesServiceRecord()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var recurrenceEngine = new RecurrenceEngine(new CalendarService());
        var reminderService = new ReminderService(db);
        var assetService = new AssetService(db, _userContext, recurrenceEngine, reminderService, audit);

        var property = new Property
        {
            HouseholdId = _householdId,
            Name = "Villa",
            Type = PropertyType.Villa
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var category = await assetService.CreateCategoryAsync(new CreateAssetCategoryRequest("HVAC", "ac_unit"));

        // Create asset with warranty end
        var asset = await assetService.CreateAssetAsync(new CreateAssetRequest(
            property.Id,
            null,
            category.Id,
            "Chiller Package",
            "LG",
            "Multi V 5",
            "SN-998877",
            new DateOnly(2025, 1, 1),
            500_000_000,
            "LG Goldiran",
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            AssetStatus.InUse));

        Assert.NotNull(asset);

        // Verify warranty reminder was queued
        var reminders = await db.Reminders.Where(r => r.SubjectType == "AssetWarranty").ToListAsync();
        Assert.Single(reminders);

        // Create monthly maintenance plan
        var recurrence = new Recurrence
        {
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1,
            CalendarMode = CalendarType.Gregorian,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
        };

        var plan = await assetService.CreateMaintenancePlanAsync(new CreateMaintenancePlanRequest(
            asset.Id,
            "Filter inspection & cleaning",
            recurrence,
            15_000_000));

        Assert.NotNull(plan.NextDueAt);

        // Complete the maintenance plan
        var completionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var completionResult = await assetService.CompleteMaintenancePlanAsync(
            plan.Id,
            completionDate,
            12_000_000,
            "Replaced primary mesh filter");

        Assert.NotNull(completionResult.ServiceRecord);
        Assert.Equal(12_000_000, completionResult.ServiceRecord.Cost);
        Assert.NotNull(completionResult.NextDueAt);
        Assert.True(completionResult.NextDueAt.Value > DateTimeOffset.UtcNow);

        // Verify service record exists in list
        var records = await assetService.GetServiceRecordsAsync(asset.Id);
        Assert.Single(records);
        Assert.Equal("Replaced primary mesh filter", records[0].Notes);

        // Verify timeline events recorded
        var timeline = await db.TimelineEvents.Where(t => t.SubjectId == asset.Id).ToListAsync();
        Assert.True(timeline.Count >= 2); // Purchase + Maintenance
    }

    [Fact]
    public async Task PeopleAndDossier_GuardianAccessControlAndReminders_Work()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var reminderService = new ReminderService(db);
        var personService = new PersonService(db, _userContext, reminderService, audit);

        // 1. Create a parent person linked to current user
        var parentPerson = await personService.CreatePersonAsync(new CreatePersonRequest(
            PersonKind.Member,
            "Parent Bob",
            "Bob Smith",
            new DateOnly(1985, 5, 20),
            UserId: _userId));

        // 2. Create child person
        var childPerson = await personService.CreatePersonAsync(new CreatePersonRequest(
            PersonKind.Member,
            "Child Alice",
            "Alice Smith",
            new DateOnly(2018, 3, 15)));

        // Link parent as guardian
        await personService.AddGuardianAsync(childPerson.Id, parentPerson.Id);
        var guardians = await personService.GetGuardiansAsync(childPerson.Id);
        Assert.Single(guardians);
        Assert.Equal(parentPerson.Id, guardians[0].Id);

        // Create health record with Guardians visibility
        var healthRecord = await personService.CreateHealthRecordAsync(new CreateHealthRecordRequest(
            childPerson.Id,
            HealthRecordType.Vaccine,
            "MMR Booster",
            new DateOnly(2025, 4, 1),
            NextDueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Visibility: ResourceVisibility.Guardians));

        // Verify health reminder scheduled
        var reminders = await db.Reminders.Where(r => r.SubjectType == "HealthRecord").ToListAsync();
        Assert.Single(reminders);

        // As parent / guardian, verify health record is visible
        var recordsForGuardian = await personService.GetHealthRecordsAsync(childPerson.Id);
        Assert.Single(recordsForGuardian);

        // Create document with ExpiryDate
        var doc = await personService.CreateDocumentAsync(new CreatePersonDocumentRequest(
            childPerson.Id,
            PersonDocumentType.Passport,
            "P12345678",
            new DateOnly(2023, 1, 1),
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            Visibility: ResourceVisibility.Guardians));

        // Verify document-expiry reminder created
        var docReminders = await db.Reminders.Where(r => r.SubjectType == "PersonDocumentExpiry").ToListAsync();
        Assert.Single(docReminders);

        // Now test restricted view for an unprivileged Teen / non-guardian user
        var teenUserId = Guid.NewGuid();
        var teenUserContext = new CurrentUserContext();
        teenUserContext.SetContext(teenUserId, _householdId, MembershipRole.Teen);

        var teenMembership = new Membership
        {
            HouseholdId = _householdId,
            UserId = teenUserId,
            Role = MembershipRole.Teen,
            IsActive = true
        };
        db.Memberships.Add(teenMembership);
        await db.SaveChangesAsync();

        var teenPersonService = new PersonService(db, teenUserContext, reminderService, audit);
        var recordsForTeen = await teenPersonService.GetHealthRecordsAsync(childPerson.Id);
        Assert.Empty(recordsForTeen); // Filtered out because Visibility is Guardians!

        var docsForTeen = await teenPersonService.GetDocumentsAsync(childPerson.Id);
        Assert.Empty(docsForTeen); // Filtered out!
    }

    [Fact]
    public async Task CalendarView_CombinesAllModuleEventsInChronologicalOrder()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var reminderService = new ReminderService(db);
        var recurrenceEngine = new RecurrenceEngine(new CalendarService());
        var homeInfo = new HomeInfoService(db, _userContext, audit);
        var assetService = new AssetService(db, _userContext, recurrenceEngine, reminderService, audit);
        var personService = new PersonService(db, _userContext, reminderService, audit);
        var calendarView = new CalendarViewService(db, _userContext);

        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-1);
        var end = now.AddDays(30);

        // 1. Task
        var task = new TaskItem
        {
            HouseholdId = _householdId,
            Title = "Fix boiler pressure",
            DueAt = now.AddDays(3),
            Status = TaskItemStatus.Todo,
            Priority = TaskPriority.Medium
        };
        db.TaskItems.Add(task);

        // 2. Property & Asset
        var property = new Property
        {
            HouseholdId = _householdId,
            Name = "Main Residence"
        };
        db.Properties.Add(property);

        var category = new AssetCategory
        {
            HouseholdId = _householdId,
            Name = "Appliances"
        };
        db.AssetCategories.Add(category);
        await db.SaveChangesAsync();

        var asset = new Asset
        {
            HouseholdId = _householdId,
            PropertyId = property.Id,
            CategoryId = category.Id,
            Name = "Refrigerator",
            WarrantyEnd = DateOnly.FromDateTime(now.AddDays(15).UtcDateTime)
        };
        db.Assets.Add(asset);

        // 3. Maintenance Plan
        var recurrence = new Recurrence
        {
            HouseholdId = _householdId,
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1,
            CalendarMode = CalendarType.Gregorian,
            StartDate = DateOnly.FromDateTime(now.UtcDateTime)
        };
        db.Recurrences.Add(recurrence);
        await db.SaveChangesAsync();

        var maintPlan = new MaintenancePlan
        {
            HouseholdId = _householdId,
            AssetId = asset.Id,
            Title = "Clean condenser coils",
            RecurrenceId = recurrence.Id,
            NextDueAt = now.AddDays(7)
        };
        db.MaintenancePlans.Add(maintPlan);

        // 4. Important Date (Repeats yearly)
        var importantDate = new ImportantDate
        {
            HouseholdId = _householdId,
            Title = "Wedding Anniversary",
            Date = DateOnly.FromDateTime(now.AddDays(10).UtcDateTime),
            RepeatsYearly = true
        };
        db.ImportantDates.Add(importantDate);

        // 5. Person & Health Due Date
        var person = new Person
        {
            HouseholdId = _householdId,
            DisplayName = "Leo",
            Kind = PersonKind.Pet
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var healthRecord = new HealthRecord
        {
            HouseholdId = _householdId,
            PersonId = person.Id,
            Type = HealthRecordType.Vaccine,
            Title = "Rabies Booster",
            Date = DateOnly.FromDateTime(now.AddDays(-300).UtcDateTime),
            NextDueDate = DateOnly.FromDateTime(now.AddDays(5).UtcDateTime),
            Visibility = ResourceVisibility.Household
        };
        db.HealthRecords.Add(healthRecord);
        await db.SaveChangesAsync();

        // Query unified calendar
        var calendarEvents = await calendarView.GetCalendarEventsAsync(new CalendarViewRequest(start, end));

        Assert.Equal(5, calendarEvents.Count);
        // Verify types present
        Assert.Contains(calendarEvents, e => e.Type == CalendarItemType.Task);
        Assert.Contains(calendarEvents, e => e.Type == CalendarItemType.Maintenance);
        Assert.Contains(calendarEvents, e => e.Type == CalendarItemType.ImportantDate);
        Assert.Contains(calendarEvents, e => e.Type == CalendarItemType.WarrantyExpiry);
        Assert.Contains(calendarEvents, e => e.Type == CalendarItemType.HealthDue);

        // Verify ordered by DueAt ascending
        for (int i = 0; i < calendarEvents.Count - 1; i++)
        {
            Assert.True(calendarEvents[i].DueAt <= calendarEvents[i + 1].DueAt);
        }
    }
}
