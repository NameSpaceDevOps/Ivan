using Ivan.Application.Common;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ivan.Tests.Integration;

public class SoftDeleteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;

    public SoftDeleteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<IvanDbContext>()
            .UseSqlite(_connection)
            .Options;

        var dummy = new CurrentUserContext();
        using var init = new IvanDbContext(_options, dummy);
        init.Database.EnsureCreated();
    }

    [Fact]
    public async Task SoftDelete_SetsDeletedAt_AndExcludesFromQueries()
    {
        var householdId = Guid.NewGuid();
        var context = new CurrentUserContext();
        context.SetContext(Guid.NewGuid(), householdId, MembershipRole.Owner);

        Guid propertyId;
        using (var db = new IvanDbContext(_options, context))
        {
            db.Households.Add(new Household { Id = householdId, Name = "خانه تهران" });
            var prop = new Property { Name = "آپارتمان تهران", Type = PropertyType.Home };
            db.Properties.Add(prop);
            await db.SaveChangesAsync();
            propertyId = prop.Id;
        }

        // Delete the property
        using (var db = new IvanDbContext(_options, context))
        {
            var prop = await db.Properties.FindAsync(propertyId);
            Assert.NotNull(prop);

            db.Properties.Remove(prop);
            await db.SaveChangesAsync();
        }

        // Querying under normal conditions should not return it
        using (var db = new IvanDbContext(_options, context))
        {
            var prop = await db.Properties.FindAsync(propertyId);
            Assert.Null(prop);

            var list = await db.Properties.ToListAsync();
            Assert.DoesNotContain(list, p => p.Id == propertyId);

            // IgnoreQueryFilters should still find it with DeletedAt set (recoverable for 30 days)
            var deletedProp = await db.Properties.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == propertyId);
            Assert.NotNull(deletedProp);
            Assert.NotNull(deletedProp.DeletedAt);
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
