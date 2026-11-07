using Ivan.Application.Common;
using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Infrastructure.Data;
using Ivan.Infrastructure.Identity;
using Ivan.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ivan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=app_data/ivan.db;Cache=Shared";

        // Current User Context (Scoped)
        services.AddScoped<CurrentUserContext>();
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());

        // EF Core SQLite DbContext
        services.AddDbContext<IvanDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddScoped<IIvanDbContext>(sp => sp.GetRequiredService<IvanDbContext>());

        // ASP.NET Core Identity (Story 1.2)
        services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<IvanDbContext>();

        // Domain & Application Services
        services.AddScoped<ISetupWizardService, SetupWizardService>();
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<IAccessGrantService, AccessGrantService>();
        services.AddScoped<IAuditService, AuditService>();

        // Phase 2 Engine Services
        services.AddSingleton<IRecurrenceEngine, RecurrenceEngine>();
        services.AddScoped<IAttachmentStorage, AttachmentStorage>();
        services.AddScoped<IChecklistService, ChecklistService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IReminderService, ReminderService>();

        // Phase 3 Home, Assets, People
        services.AddScoped<IHomeInfoService, HomeInfoService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IPersonService, PersonService>();
        services.AddScoped<ICalendarViewService, CalendarViewService>();

        // Phase 4 Finance
        services.AddScoped<IChartOfAccountsSeeder, ChartOfAccountsSeeder>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IFinanceService, FinanceService>();

        // Phase 5 Children, Wallet, Homework
        services.AddScoped<IChildrenService, ChildrenService>();

        return services;
    }
}
