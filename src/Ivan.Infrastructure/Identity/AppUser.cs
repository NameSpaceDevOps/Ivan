using Ivan.Domain.Common;
using Ivan.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Ivan.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>, IEntity, IAuditable, ISoftDeletable
{
    public string DisplayName { get; set; } = string.Empty;
    public string Culture { get; set; } = "fa-IR";
    public CalendarType Calendar { get; set; } = CalendarType.Jalali;
    public DigitStyle DigitStyle { get; set; } = DigitStyle.Persian;
    public ThemePreference Theme { get; set; } = ThemePreference.Light;

    // PIN hash for child quick-login on trusted devices (Section 3.3 / Story 1.7)
    public string? PinHash { get; set; }
    public int PinFailedAttempts { get; set; }
    public DateTimeOffset? PinLockoutEnd { get; set; }

    // Optional link to Person record in the dossier
    public Guid? PersonId { get; set; }

    // Audit and soft delete
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }

    public AppUser(string userName) : this()
    {
        UserName = userName;
    }
}
