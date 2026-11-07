using Ivan.Application.Common;
using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Data;
using Ivan.Infrastructure.Identity;
using Ivan.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ivan.Tests.Integration;

public class SetupWizardTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    public SetupWizardTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<IvanDbContext>(opts => opts.UseSqlite(_connection));
        services.AddScoped<IIvanDbContext>(sp => sp.GetRequiredService<IvanDbContext>());
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<IvanDbContext>();

        services.AddScoped<ISetupWizardService, SetupWizardService>();

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvanDbContext>();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task FirstRun_CreatesOwnerHouseholdAndProperty_SecondRunIsBlocked()
    {
        using var scope = _serviceProvider.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<ISetupWizardService>();
        var db = scope.ServiceProvider.GetRequiredService<IIvanDbContext>();

        // Assert setup is initially not completed
        bool initialStatus = await wizard.IsSetupCompletedAsync();
        Assert.False(initialStatus);

        // Run setup wizard
        var request = new SetupWizardRequest
        {
            HouseholdName = "خانواده بهار",
            OwnerUserName = "babak",
            OwnerDisplayName = "بابک",
            OwnerPassword = "Password123!",
            OwnerEmail = "babak@example.com",
            InitialPropertyName = "آپارتمان اصلی"
        };

        var result = await wizard.ExecuteSetupAsync(request);
        Assert.True(result.Success);
        Assert.NotNull(result.HouseholdId);
        Assert.NotNull(result.OwnerUserId);

        // Assert database state
        var household = await db.Households.FirstOrDefaultAsync(h => h.Id == result.HouseholdId);
        Assert.NotNull(household);
        Assert.Equal("خانواده بهار", household.Name);

        var membership = await db.Memberships.FirstOrDefaultAsync(m => m.HouseholdId == result.HouseholdId);
        Assert.NotNull(membership);
        Assert.Equal(MembershipRole.Owner, membership.Role);
        Assert.Equal(result.OwnerUserId, membership.UserId);

        var property = await db.Properties.FirstOrDefaultAsync(p => p.HouseholdId == result.HouseholdId);
        Assert.NotNull(property);
        Assert.Equal("آپارتمان اصلی", property.Name);

        // Assert setup is now completed
        bool afterStatus = await wizard.IsSetupCompletedAsync();
        Assert.True(afterStatus);

        // Second run must be blocked (Story 1.1 Acceptance Criteria)
        var secondRun = await wizard.ExecuteSetupAsync(request);
        Assert.False(secondRun.Success);
        Assert.Contains("مسدود", secondRun.ErrorMessage);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }
}
