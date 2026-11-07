using System.Text;
using Ivan.Application.Common;
using Ivan.Application.Common.Interfaces;
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

public class EngineIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;
    private readonly CurrentUserContext _userContext;
    private readonly string _tempStorageDir;

    public EngineIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<IvanDbContext>()
            .UseSqlite(_connection)
            .Options;

        _userContext = new CurrentUserContext();
        using var init = new IvanDbContext(_options, _userContext);
        init.Database.EnsureCreated();

        _tempStorageDir = Path.Combine(Path.GetTempPath(), "ivan_test_storage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempStorageDir);
    }

    [Fact]
    public async Task AttachmentStorage_DeduplicatesIdenticalFiles()
    {
        var householdId = Guid.NewGuid();
        _userContext.SetContext(Guid.NewGuid(), householdId, MembershipRole.Owner);

        using var db = new IvanDbContext(_options, _userContext);
        db.Households.Add(new Household { Id = householdId, Name = "خانه تست" });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Root"] = _tempStorageDir
            })
            .Build();

        var storage = new AttachmentStorage(db, _userContext, config);

        var fileContent = "This is a sample document for Ivan family manager attachment testing.";
        var bytes = Encoding.UTF8.GetBytes(fileContent);

        // Upload first file
        using var stream1 = new MemoryStream(bytes);
        var att1 = await storage.StoreAsync(new StoreAttachmentRequest(
            "receipt.pdf", "application/pdf", "Expense", Guid.NewGuid(), stream1));

        Assert.NotNull(att1);
        Assert.NotEmpty(att1.Sha256);
        Assert.Equal(bytes.Length, att1.Size);

        // Upload identical file under different owner/name
        using var stream2 = new MemoryStream(bytes);
        var att2 = await storage.StoreAsync(new StoreAttachmentRequest(
            "contract.pdf", "application/pdf", "Property", Guid.NewGuid(), stream2));

        Assert.NotNull(att2);
        // SHA-256 and physical storage path must be identical (deduplication!)
        Assert.Equal(att1.Sha256, att2.Sha256);
        Assert.Equal(att1.StoragePath, att2.StoragePath);

        // Verify content can be read back cleanly
        using var readStream = await storage.OpenReadAsync(att1.Id);
        Assert.NotNull(readStream);
        using var reader = new StreamReader(readStream);
        var readText = await reader.ReadToEndAsync();
        Assert.Equal(fileContent, readText);
    }

    [Fact]
    public async Task ChecklistService_ClonesFromTemplate_AndCalculatesProgress()
    {
        var householdId = Guid.NewGuid();
        _userContext.SetContext(Guid.NewGuid(), householdId, MembershipRole.Owner);

        using var db = new IvanDbContext(_options, _userContext);
        db.Households.Add(new Household { Id = householdId, Name = "خانه تست" });
        await db.SaveChangesAsync();

        var checklistService = new ChecklistService(db, _userContext);

        // 1. Create Template Checklist
        var template = await checklistService.CreateChecklistAsync(new CreateChecklistRequest(
            "چک‌لیست نظافت هفتگی", IsTemplate: true));

        var item1 = await checklistService.AddItemAsync(new AddChecklistItemRequest(template.Id, "جارو زدن پذیرایی"));
        var item2 = await checklistService.AddItemAsync(new AddChecklistItemRequest(template.Id, "گردگیری قفسه‌ها"));

        // 2. Clone from template (Story 2.3)
        var cloned = await checklistService.CloneFromTemplateAsync(template.Id, "نظافت هفته اول آبان");
        Assert.NotNull(cloned);
        Assert.False(cloned.IsTemplate);
        Assert.Equal(template.Id, cloned.TemplateId);
        Assert.Equal(2, cloned.Items.Count);
        Assert.All(cloned.Items, i => Assert.False(i.Done));
        Assert.Equal(0, cloned.ProgressPercentage);

        // 3. Mark one item done
        var firstClonedItem = cloned.Items.First();
        var toggled = await checklistService.ToggleItemDoneAsync(firstClonedItem.Id, true);
        Assert.True(toggled);

        var reloaded = await checklistService.GetChecklistByIdAsync(cloned.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(1, reloaded.DoneCount);
        Assert.Equal(50.0, reloaded.ProgressPercentage);
    }

    [Fact]
    public async Task TaskService_CreateAndFilterByStatus()
    {
        var householdId = Guid.NewGuid();
        _userContext.SetContext(Guid.NewGuid(), householdId, MembershipRole.Owner);

        using var db = new IvanDbContext(_options, _userContext);
        db.Households.Add(new Household { Id = householdId, Name = "خانه تست" });
        await db.SaveChangesAsync();

        var taskService = new TaskService(db, _userContext);

        var task1 = await taskService.CreateTaskAsync(new CreateTaskRequest(
            "خرید روغن موتور برای خودرو", Priority: TaskPriority.High));
        var task2 = await taskService.CreateTaskAsync(new CreateTaskRequest(
            "بررسی شیر آب حیاط", Priority: TaskPriority.Low));

        // Mark task1 in progress then done
        await taskService.UpdateStatusAsync(task1.Id, TaskItemStatus.Done);

        var pendingTasks = await taskService.GetTasksAsync(status: TaskItemStatus.Todo);
        Assert.Single(pendingTasks);
        Assert.Equal(task2.Id, pendingTasks[0].Id);

        var doneTasks = await taskService.GetTasksAsync(status: TaskItemStatus.Done);
        Assert.Single(doneTasks);
        Assert.Equal(task1.Id, doneTasks[0].Id);
    }

    [Fact]
    public async Task ReminderService_ScheduleIsIdempotent_WithDedupKey()
    {
        var householdId = Guid.NewGuid();
        _userContext.SetContext(Guid.NewGuid(), householdId, MembershipRole.Owner);

        using var db = new IvanDbContext(_options, _userContext);
        db.Households.Add(new Household { Id = householdId, Name = "خانه تست" });
        await db.SaveChangesAsync();

        var reminderService = new ReminderService(db);

        var taskId = Guid.NewGuid();
        var fireAt = DateTimeOffset.UtcNow.AddHours(2);
        var dedupKey = $"Task:{taskId}:DueReminder";

        // First schedule
        var r1 = await reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
            "Task", taskId, "یادآوری انجام وظیفه", fireAt, dedupKey));

        Assert.NotNull(r1);

        // Second schedule with identical DedupKey (Story 2.5: No duplicate reminders)
        var r2 = await reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
            "Task", taskId, "یادآوری انجام وظیفه", fireAt, dedupKey));

        Assert.Equal(r1.Id, r2.Id);

        var allReminders = await db.Reminders.ToListAsync();
        Assert.Single(allReminders);
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_tempStorageDir))
        {
            try { Directory.Delete(_tempStorageDir, true); } catch { }
        }
    }
}
