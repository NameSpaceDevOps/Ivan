using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record SetupWizardRequest
{
    public string HouseholdName { get; init; } = "خانواده من";
    public string OwnerUserName { get; init; } = "admin";
    public string OwnerDisplayName { get; init; } = "مدیر خانه";
    public string OwnerPassword { get; init; } = string.Empty;
    public string? OwnerEmail { get; init; }
    public string? OwnerPhone { get; init; }
    public string DefaultCulture { get; init; } = "fa-IR";
    public CalendarType DefaultCalendar { get; init; } = CalendarType.Jalali;
    public string DefaultCurrency { get; init; } = "IRR";
    public string InitialPropertyName { get; init; } = "خانه اصلی";
    public PropertyType InitialPropertyType { get; init; } = PropertyType.Home;
}

public record SetupWizardResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? HouseholdId { get; init; }
    public Guid? OwnerUserId { get; init; }

    public static SetupWizardResult Failed(string error) => new() { Success = false, ErrorMessage = error };
    public static SetupWizardResult Succeeded(Guid householdId, Guid ownerUserId) => new()
    {
        Success = true,
        HouseholdId = householdId,
        OwnerUserId = ownerUserId
    };
}

public interface ISetupWizardService
{
    Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default);
    Task<SetupWizardResult> ExecuteSetupAsync(SetupWizardRequest request, CancellationToken cancellationToken = default);
}
