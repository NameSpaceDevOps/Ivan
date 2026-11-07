using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class AssetService : IAssetService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IRecurrenceEngine _recurrenceEngine;
    private readonly IReminderService _reminderService;
    private readonly IAuditService _auditService;

    public AssetService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IRecurrenceEngine recurrenceEngine,
        IReminderService reminderService,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _recurrenceEngine = recurrenceEngine;
        _reminderService = reminderService;
        _auditService = auditService;
    }

    public async Task<List<AssetCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await _db.AssetCategories
            .Include(c => c.Assets)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return categories
            .Select(c => new AssetCategoryDto(c.Id, c.Name, c.Icon, c.Assets.Count))
            .ToList();
    }

    public async Task<AssetCategoryDto> CreateCategoryAsync(CreateAssetCategoryRequest request, CancellationToken ct = default)
    {
        var category = new AssetCategory
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Name = request.Name,
            Icon = request.Icon
        };

        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateCategory", "AssetCategory", category.Id.ToString(), $"Created asset category {category.Name}", ct);

        return new AssetCategoryDto(category.Id, category.Name, category.Icon, 0);
    }

    public async Task<List<AssetDto>> GetAssetsAsync(Guid? propertyId = null, Guid? categoryId = null, CancellationToken ct = default)
    {
        var query = _db.Assets
            .Include(a => a.Category)
            .AsQueryable();

        if (propertyId.HasValue)
            query = query.Where(a => a.PropertyId == propertyId.Value);

        if (categoryId.HasValue)
            query = query.Where(a => a.CategoryId == categoryId.Value);

        var list = await query.ToListAsync(ct);
        return list
            .OrderBy(a => a.Name)
            .Select(MapToAssetDto)
            .ToList();
    }

    public async Task<AssetDto?> GetAssetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await _db.Assets
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return asset == null ? null : MapToAssetDto(asset);
    }

    public async Task<AssetDto> CreateAssetAsync(CreateAssetRequest request, CancellationToken ct = default)
    {
        var asset = new Asset
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PropertyId = request.PropertyId,
            SpaceId = request.SpaceId,
            CategoryId = request.CategoryId,
            Name = request.Name,
            Brand = request.Brand,
            Model = request.Model,
            SerialNo = request.SerialNo,
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
            Vendor = request.Vendor,
            WarrantyEnd = request.WarrantyEnd,
            Status = request.Status,
            ExtraJson = request.ExtraJson
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateAsset", "Asset", asset.Id.ToString(), $"Created asset {asset.Name}", ct);

        // If warranty end date is specified, schedule a warranty expiration reminder (e.g., 7 days before)
        if (asset.WarrantyEnd.HasValue)
        {
            var expiryDateTime = new DateTimeOffset(asset.WarrantyEnd.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var reminderTime = expiryDateTime.AddDays(-7);
            if (reminderTime > DateTimeOffset.UtcNow)
            {
                await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                    "AssetWarranty",
                    asset.Id,
                    $"Warranty expiring for {asset.Name}",
                    reminderTime,
                    $"warranty-{asset.Id}"), ct);
            }
        }

        // Timeline event
        var timeline = new TimelineEvent
        {
            HouseholdId = asset.HouseholdId,
            SubjectType = "Asset",
            SubjectId = asset.Id,
            EventDate = asset.PurchaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Kind = "Purchased",
            Title = $"Asset {asset.Name} acquired",
            Body = asset.Vendor != null ? $"Purchased from {asset.Vendor}" : null
        };
        _db.TimelineEvents.Add(timeline);
        await _db.SaveChangesAsync(ct);

        var loaded = await _db.Assets
            .Include(a => a.Category)
            .FirstAsync(a => a.Id == asset.Id, ct);

        return MapToAssetDto(loaded);
    }

    public async Task DeleteAssetAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await _db.Assets.FindAsync([id], ct);
        if (asset != null)
        {
            _db.Assets.Remove(asset);
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("DeleteAsset", "Asset", id.ToString(), $"Deleted asset {asset.Name}", ct);
        }
    }

    public async Task<List<MaintenancePlanDto>> GetMaintenancePlansAsync(Guid? assetId = null, CancellationToken ct = default)
    {
        var query = _db.MaintenancePlans
            .Include(m => m.Asset)
            .AsQueryable();

        if (assetId.HasValue)
            query = query.Where(m => m.AssetId == assetId.Value);

        var list = await query.ToListAsync(ct);
        return list
            .OrderBy(m => m.NextDueAt)
            .Select(m => new MaintenancePlanDto(
                m.Id,
                m.AssetId,
                m.Asset?.Name ?? string.Empty,
                m.Title,
                m.RecurrenceId,
                m.LastDoneAt,
                m.NextDueAt,
                m.EstimatedCost))
            .ToList();
    }

    public async Task<MaintenancePlanDto> CreateMaintenancePlanAsync(CreateMaintenancePlanRequest request, CancellationToken ct = default)
    {
        var recurrence = request.Recurrence;
        recurrence.HouseholdId = _userContext.HouseholdId ?? Guid.Empty;
        _db.Recurrences.Add(recurrence);
        await _db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nextDate = _recurrenceEngine.GetNextOccurrence(recurrence, today.AddDays(-1));
        var nextDue = nextDate.HasValue
            ? new DateTimeOffset(nextDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : (DateTimeOffset?)null;

        var plan = new MaintenancePlan
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            AssetId = request.AssetId,
            Title = request.Title,
            RecurrenceId = recurrence.Id,
            EstimatedCost = request.EstimatedCost,
            NextDueAt = nextDue
        };

        _db.MaintenancePlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        if (plan.NextDueAt.HasValue && plan.NextDueAt.Value > DateTimeOffset.UtcNow)
        {
            await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                "MaintenancePlan",
                plan.Id,
                $"Maintenance due: {plan.Title}",
                plan.NextDueAt.Value,
                $"maint-{plan.Id}-{plan.NextDueAt.Value.Ticks}"), ct);
        }

        var asset = await _db.Assets.FindAsync([plan.AssetId], ct);
        return new MaintenancePlanDto(
            plan.Id,
            plan.AssetId,
            asset?.Name ?? string.Empty,
            plan.Title,
            plan.RecurrenceId,
            plan.LastDoneAt,
            plan.NextDueAt,
            plan.EstimatedCost);
    }

    public async Task<CompleteMaintenancePlanResult> CompleteMaintenancePlanAsync(
        Guid planId,
        DateOnly completionDate,
        long? cost = null,
        string? notes = null,
        Guid? providerContactId = null,
        CancellationToken ct = default)
    {
        var plan = await _db.MaintenancePlans
            .Include(m => m.Recurrence)
            .Include(m => m.Asset)
            .FirstOrDefaultAsync(m => m.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Maintenance plan {planId} not found.");

        var completionDateTime = new DateTimeOffset(completionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        // Create service record
        var serviceRecord = new ServiceRecord
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            AssetId = plan.AssetId,
            Date = completionDate,
            Type = ServiceRecordType.Routine,
            Cost = cost ?? plan.EstimatedCost,
            Notes = notes ?? $"Completed maintenance plan: {plan.Title}",
            ProviderContactId = providerContactId
        };
        _db.ServiceRecords.Add(serviceRecord);

        // Update plan last done and next due
        plan.LastDoneAt = completionDateTime;
        if (plan.Recurrence != null)
        {
            var nextDate = _recurrenceEngine.GetNextOccurrence(plan.Recurrence, completionDate);
            plan.NextDueAt = nextDate.HasValue
                ? new DateTimeOffset(nextDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
                : null;
        }

        await _db.SaveChangesAsync(ct);

        // Timeline event
        var timeline = new TimelineEvent
        {
            HouseholdId = plan.HouseholdId,
            SubjectType = "Asset",
            SubjectId = plan.AssetId,
            EventDate = completionDate,
            Kind = "Maintenance",
            Title = $"Maintenance completed: {plan.Title}",
            Body = notes
        };
        _db.TimelineEvents.Add(timeline);
        await _db.SaveChangesAsync(ct);

        // Schedule next reminder if applicable
        if (plan.NextDueAt.HasValue && plan.NextDueAt.Value > DateTimeOffset.UtcNow)
        {
            await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                "MaintenancePlan",
                plan.Id,
                $"Maintenance due: {plan.Title}",
                plan.NextDueAt.Value,
                $"maint-{plan.Id}-{plan.NextDueAt.Value.Ticks}"), ct);
        }

        var contact = providerContactId.HasValue ? await _db.Contacts.FindAsync([providerContactId.Value], ct) : null;
        var serviceRecordDto = new ServiceRecordDto(
            serviceRecord.Id,
            serviceRecord.AssetId,
            serviceRecord.Date,
            serviceRecord.Type,
            serviceRecord.ProviderContactId,
            contact?.Name,
            serviceRecord.Cost,
            serviceRecord.Notes,
            serviceRecord.JournalEntryId);

        return new CompleteMaintenancePlanResult(serviceRecordDto, plan.NextDueAt);
    }

    public async Task<List<ServiceRecordDto>> GetServiceRecordsAsync(Guid assetId, CancellationToken ct = default)
    {
        var records = await _db.ServiceRecords
            .Include(r => r.ProviderContact)
            .Where(r => r.AssetId == assetId)
            .ToListAsync(ct);

        return records
            .OrderByDescending(r => r.Date)
            .Select(r => new ServiceRecordDto(
                r.Id,
                r.AssetId,
                r.Date,
                r.Type,
                r.ProviderContactId,
                r.ProviderContact?.Name,
                r.Cost,
                r.Notes,
                r.JournalEntryId))
            .ToList();
    }

    public async Task<ServiceRecordDto> CreateServiceRecordAsync(CreateServiceRecordRequest request, CancellationToken ct = default)
    {
        var record = new ServiceRecord
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            AssetId = request.AssetId,
            Date = request.Date,
            Type = request.Type,
            ProviderContactId = request.ProviderContactId,
            Cost = request.Cost,
            Notes = request.Notes
        };

        _db.ServiceRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        // Timeline event
        var timeline = new TimelineEvent
        {
            HouseholdId = record.HouseholdId,
            SubjectType = "Asset",
            SubjectId = record.AssetId,
            EventDate = record.Date,
            Kind = record.Type.ToString(),
            Title = $"Service performed: {record.Type}",
            Body = record.Notes
        };
        _db.TimelineEvents.Add(timeline);
        await _db.SaveChangesAsync(ct);

        var contact = request.ProviderContactId.HasValue ? await _db.Contacts.FindAsync([request.ProviderContactId.Value], ct) : null;
        return new ServiceRecordDto(
            record.Id,
            record.AssetId,
            record.Date,
            record.Type,
            record.ProviderContactId,
            contact?.Name,
            record.Cost,
            record.Notes,
            record.JournalEntryId);
    }

    private static AssetDto MapToAssetDto(Asset a) => new(
        a.Id,
        a.PropertyId ?? Guid.Empty,
        a.SpaceId,
        a.CategoryId ?? Guid.Empty,
        a.Category?.Name ?? string.Empty,
        a.Name,
        a.Brand,
        a.Model,
        a.SerialNo,
        a.PurchaseDate,
        a.PurchasePrice,
        a.Vendor,
        a.WarrantyEnd,
        a.Status,
        a.ExtraJson);
}
