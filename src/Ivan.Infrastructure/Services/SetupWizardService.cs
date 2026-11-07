using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class SetupWizardService : ISetupWizardService
{
    private readonly IIvanDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public SetupWizardService(IIvanDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Households.AnyAsync(cancellationToken);
    }

    public async Task<SetupWizardResult> ExecuteSetupAsync(SetupWizardRequest request, CancellationToken cancellationToken = default)
    {
        // Block second run (Story 1.1 Acceptance Criteria)
        if (await IsSetupCompletedAsync(cancellationToken))
        {
            return SetupWizardResult.Failed("راه‌اندازی اولیه قبلاً انجام شده است و اجرای مجدد مسدود می‌باشد.");
        }

        if (string.IsNullOrWhiteSpace(request.OwnerUserName))
        {
            return SetupWizardResult.Failed("نام کاربری مدیر الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(request.OwnerPassword) || request.OwnerPassword.Length < 6)
        {
            return SetupWizardResult.Failed("رمز عبور باید حداقل ۶ کاراکتر باشد.");
        }

        // 1. Create Household
        var household = new Household
        {
            Name = request.HouseholdName,
            DefaultCulture = request.DefaultCulture,
            DefaultCalendar = request.DefaultCalendar,
            DefaultCurrency = request.DefaultCurrency,
            IsActive = true
        };
        _db.Households.Add(household);
        await _db.SaveChangesAsync(cancellationToken);

        // 2. Create Owner User
        var user = new AppUser(request.OwnerUserName)
        {
            DisplayName = string.IsNullOrWhiteSpace(request.OwnerDisplayName) ? request.OwnerUserName : request.OwnerDisplayName,
            Email = request.OwnerEmail,
            PhoneNumber = request.OwnerPhone,
            Culture = request.DefaultCulture,
            Calendar = request.DefaultCalendar,
            DigitStyle = request.DefaultCulture.StartsWith("fa") ? DigitStyle.Persian : DigitStyle.Latin
        };

        var identityResult = await _userManager.CreateAsync(user, request.OwnerPassword);
        if (!identityResult.Succeeded)
        {
            var errors = string.Join("; ", identityResult.Errors.Select(e => e.Description));
            // Rollback household
            _db.Households.Remove(household);
            await _db.SaveChangesAsync(cancellationToken);
            return SetupWizardResult.Failed($"خطا در ایجاد کاربر مدیر: {errors}");
        }

        // 3. Create Membership (Owner)
        var membership = new Membership
        {
            HouseholdId = household.Id,
            UserId = user.Id,
            Role = MembershipRole.Owner,
            IsActive = true
        };
        _db.Memberships.Add(membership);

        // 4. Create Initial Property
        var property = new Property
        {
            HouseholdId = household.Id,
            Name = string.IsNullOrWhiteSpace(request.InitialPropertyName) ? "خانه اصلی" : request.InitialPropertyName,
            Type = request.InitialPropertyType
        };
        _db.Properties.Add(property);

        // 5. Initial Spaces in property
        var livingRoom = new Space
        {
            HouseholdId = household.Id,
            PropertyId = property.Id,
            Name = "اتاق نشیمن",
            Type = SpaceType.LivingRoom
        };
        var masterBed = new Space
        {
            HouseholdId = household.Id,
            PropertyId = property.Id,
            Name = "اتاق خواب اصلی",
            Type = SpaceType.Room
        };
        _db.Spaces.AddRange(livingRoom, masterBed);

        // 6. Audit log for setup
        var audit = new AuditLog
        {
            HouseholdId = household.Id,
            UserId = user.Id,
            Action = "Setup",
            ResourceType = "Household",
            ResourceId = household.Id.ToString(),
            Summary = $"راه‌اندازی اولیه خانه {household.Name} توسط {user.DisplayName}"
        };
        _db.AuditLogs.Add(audit);

        await _db.SaveChangesAsync(cancellationToken);

        return SetupWizardResult.Succeeded(household.Id, user.Id);
    }
}
