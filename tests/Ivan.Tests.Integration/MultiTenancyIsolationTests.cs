using System.Reflection;
using Ivan.Application.Common;
using Ivan.Domain.Common;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ivan.Tests.Integration;

public class MultiTenancyIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;

    public MultiTenancyIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<IvanDbContext>()
            .UseSqlite(_connection)
            .Options;

        var dummyContext = new CurrentUserContext();
        using var initContext = new IvanDbContext(_options, dummyContext);
        initContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task AutomaticStamping_SetsHouseholdId_WhenEmpty()
    {
        var householdId = Guid.NewGuid();
        var userContext = new CurrentUserContext();
        userContext.SetContext(Guid.NewGuid(), householdId, MembershipRole.Owner);

        using (var db = new IvanDbContext(_options, userContext))
        {
            db.Households.Add(new Household { Id = householdId, Name = "خانه اصلی" });
            await db.SaveChangesAsync();

            var property = new Property
            {
                Name = "ویلای شمال",
                Type = PropertyType.Villa
            };

            db.Properties.Add(property);
            await db.SaveChangesAsync();

            Assert.Equal(householdId, property.HouseholdId);
            Assert.True(property.CreatedAt <= DateTimeOffset.UtcNow);
        }
    }

    [Fact]
    public async Task CrossHouseholdQuery_NeverReturnsRowsFromAnotherHousehold()
    {
        var householdA = Guid.NewGuid();
        var householdB = Guid.NewGuid();

        var contextA = new CurrentUserContext();
        contextA.SetContext(Guid.NewGuid(), householdA, MembershipRole.Owner);

        using (var dbA = new IvanDbContext(_options, contextA))
        {
            dbA.Households.Add(new Household { Id = householdA, Name = "خانه الف" });
            dbA.Properties.Add(new Property { Name = "ملک خانواده الف", Type = PropertyType.Home });
            dbA.Memberships.Add(new Membership { UserId = Guid.NewGuid(), Role = MembershipRole.Parent });
            dbA.AccessGrants.Add(new AccessGrant { GranteeUserId = Guid.NewGuid(), ResourceType = "Property" });
            dbA.AuditLogs.Add(new AuditLog { Action = "Create", ResourceType = "Property", Summary = "تست الف" });
            await dbA.SaveChangesAsync();
        }

        var contextB = new CurrentUserContext();
        contextB.SetContext(Guid.NewGuid(), householdB, MembershipRole.Owner);

        using (var dbB = new IvanDbContext(_options, contextB))
        {
            dbB.Households.Add(new Household { Id = householdB, Name = "خانه ب" });
            dbB.Properties.Add(new Property { Name = "ملک خانواده ب", Type = PropertyType.Home });
            dbB.Memberships.Add(new Membership { UserId = Guid.NewGuid(), Role = MembershipRole.Parent });
            dbB.AccessGrants.Add(new AccessGrant { GranteeUserId = Guid.NewGuid(), ResourceType = "Property" });
            dbB.AuditLogs.Add(new AuditLog { Action = "Create", ResourceType = "Property", Summary = "تست ب" });
            await dbB.SaveChangesAsync();
        }

        using (var queryContext = new IvanDbContext(_options, contextA))
        {
            var props = await queryContext.Properties.ToListAsync();
            Assert.All(props, p => Assert.Equal(householdA, p.HouseholdId));
            Assert.Contains(props, p => p.Name == "ملک خانواده الف");
            Assert.DoesNotContain(props, p => p.Name == "ملک خانواده ب");

            var members = await queryContext.Memberships.ToListAsync();
            Assert.All(members, m => Assert.Equal(householdA, m.HouseholdId));

            var grants = await queryContext.AccessGrants.ToListAsync();
            Assert.All(grants, g => Assert.Equal(householdA, g.HouseholdId));

            var logs = await queryContext.AuditLogs.ToListAsync();
            Assert.All(logs, l => Assert.Equal(householdA, l.HouseholdId));
            Assert.Contains(logs, l => l.Summary == "تست الف");
            Assert.DoesNotContain(logs, l => l.Summary == "تست ب");
        }
    }

    [Fact]
    public void ReflectionTest_AssertsAllTenantEntities_ImplementITenantEntity()
    {
        var domainAssembly = typeof(ITenantEntity).Assembly;
        var entityTypes = domainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested && !t.Name.Contains('<') && t.Namespace != null && t.Namespace.Contains("Entities"))
            .ToList();

        // Every entity except Household itself must implement ITenantEntity
        foreach (var type in entityTypes)
        {
            if (type == typeof(Household))
                continue;

            Assert.True(
                typeof(ITenantEntity).IsAssignableFrom(type),
                $"Entity '{type.Name}' in Domain.Entities must implement ITenantEntity for multi-tenancy isolation.");
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
