using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public class SetupWizardRequest
{
    public string HouseholdName { get; set; } = "خانواده من";
    public string OwnerUserName { get; set; } = "admin";
    public string OwnerDisplayName { get; set; } = "مدیر خانه";
    public string OwnerPassword { get; set; } = string.Empty;
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
    public string DefaultCulture { get; set; } = "fa-IR";
    public CalendarType DefaultCalendar { get; set; } = CalendarType.Jalali;
    public string DefaultCurrency { get; set; } = "IRR";
    public string InitialPropertyName { get; set; } = "خانه اصلی";
    public PropertyType InitialPropertyType { get; set; } = PropertyType.Home;
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
