using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record AssetCategoryDto(
    Guid Id,
    string Name,
    string Icon,
    int AssetCount);

public record CreateAssetCategoryRequest(
    string Name,
    string Icon = "inventory_2");

public record AssetDto(
    Guid Id,
    Guid PropertyId,
    Guid? SpaceId,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Brand,
    string? Model,
    string? SerialNo,
    DateOnly? PurchaseDate,
    long? PurchasePrice,
    string? Vendor,
    DateOnly? WarrantyEnd,
    AssetStatus Status,
    string? ExtraJson);

public record CreateAssetRequest(
    Guid PropertyId,
    Guid? SpaceId,
    Guid CategoryId,
    string Name,
    string? Brand = null,
    string? Model = null,
    string? SerialNo = null,
    DateOnly? PurchaseDate = null,
    long? PurchasePrice = null,
    string? Vendor = null,
    DateOnly? WarrantyEnd = null,
    AssetStatus Status = AssetStatus.InUse,
    string? ExtraJson = null);

public record MaintenancePlanDto(
    Guid Id,
    Guid AssetId,
    string AssetName,
    string Title,
    Guid RecurrenceId,
    DateTimeOffset? LastDoneAt,
    DateTimeOffset? NextDueAt,
    long? EstimatedCost);

public record CreateMaintenancePlanRequest(
    Guid AssetId,
    string Title,
    Recurrence Recurrence,
    long? EstimatedCost = null);

public record ServiceRecordDto(
    Guid Id,
    Guid AssetId,
    DateOnly Date,
    ServiceRecordType Type,
    Guid? ProviderContactId,
    string? ProviderName,
    long? Cost,
    string? Notes,
    Guid? JournalEntryId);

public record CreateServiceRecordRequest(
    Guid AssetId,
    DateOnly Date,
    ServiceRecordType Type,
    Guid? ProviderContactId = null,
    long? Cost = null,
    string? Notes = null);

public record CompleteMaintenancePlanResult(
    ServiceRecordDto ServiceRecord,
    DateTimeOffset? NextDueAt);

public interface IAssetService
{
    // Categories
    Task<List<AssetCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<AssetCategoryDto> CreateCategoryAsync(CreateAssetCategoryRequest request, CancellationToken ct = default);

    // Assets
    Task<List<AssetDto>> GetAssetsAsync(Guid? propertyId = null, Guid? categoryId = null, CancellationToken ct = default);
    Task<AssetDto?> GetAssetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AssetDto> CreateAssetAsync(CreateAssetRequest request, CancellationToken ct = default);
    Task DeleteAssetAsync(Guid id, CancellationToken ct = default);

    // Maintenance Plans
    Task<List<MaintenancePlanDto>> GetMaintenancePlansAsync(Guid? assetId = null, CancellationToken ct = default);
    Task<MaintenancePlanDto> CreateMaintenancePlanAsync(CreateMaintenancePlanRequest request, CancellationToken ct = default);
    Task<CompleteMaintenancePlanResult> CompleteMaintenancePlanAsync(
        Guid planId,
        DateOnly completionDate,
        long? cost = null,
        string? notes = null,
        Guid? providerContactId = null,
        CancellationToken ct = default);

    // Service Records
    Task<List<ServiceRecordDto>> GetServiceRecordsAsync(Guid assetId, CancellationToken ct = default);
    Task<ServiceRecordDto> CreateServiceRecordAsync(CreateServiceRecordRequest request, CancellationToken ct = default);
}
