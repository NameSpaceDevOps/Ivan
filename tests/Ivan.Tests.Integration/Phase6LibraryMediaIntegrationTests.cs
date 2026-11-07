using Ivan.Application.Common;
using Ivan.Application.Services;
using Ivan.Domain.Common;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Data;
using Ivan.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ivan.Tests.Integration;

public class Phase6LibraryMediaIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;
    private readonly CurrentUserContext _userContext;
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _personId = Guid.NewGuid();

    public Phase6LibraryMediaIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<IvanDbContext>()
            .UseSqlite(_connection)
            .Options;

        _userContext = new CurrentUserContext();
        _userContext.SetContext(_userId, _householdId, MembershipRole.Parent);

        using var init = new IvanDbContext(_options, _userContext);
        init.Database.EnsureCreated();

        // Seed household, membership & person
        var household = new Household
        {
            Id = _householdId,
            Name = "Library Test Household",
            DefaultCurrency = "IRR"
        };
        var membership = new Membership
        {
            HouseholdId = _householdId,
            UserId = _userId,
            Role = MembershipRole.Parent,
            IsActive = true
        };
        var person = new Person
        {
            Id = _personId,
            HouseholdId = _householdId,
            DisplayName = "Sara",
            BirthDate = new DateOnly(1995, 4, 10)
        };

        init.Households.Add(household);
        init.Memberships.Add(membership);
        init.People.Add(person);
        init.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private (IvanDbContext db, ILibraryService library, IMediaService media) CreateServices()
    {
        var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var reminder = new ReminderService(db);
        var library = new LibraryService(db, _userContext, reminder, audit);
        var media = new MediaService(db, _userContext, audit);
        return (db, library, media);
    }

    [Fact]
    public async Task Books_Crud_Location_AndPerPersonReadStatus_Works()
    {
        var (db, library, _) = CreateServices();
        using (db)
        {
            // 1. Space
            var property = new Property { HouseholdId = _householdId, Name = "Main House" };
            db.Properties.Add(property);
            await db.SaveChangesAsync();

            var space = new Space { HouseholdId = _householdId, PropertyId = property.Id, Name = "Study Room" };
            db.Spaces.Add(space);
            await db.SaveChangesAsync();

            // 2. Create Book
            var createRequest = new CreateBookRequest(
                Title: "Shahnameh",
                Author: "Ferdowsi",
                OriginalTitle: "شاهنامه فردوسی",
                Publisher: "Amirkabir",
                PublishYear: 1395,
                Isbn: "978-964-00-1234-5",
                PageCount: 1250,
                SpaceId: space.Id,
                ShelfLocation: "Shelf 2, Row 3",
                Status: BookStatus.Available,
                Rating: 5);

            var book = await library.CreateBookAsync(createRequest);
            Assert.NotNull(book);
            Assert.Equal("Shahnameh", book.Title);
            Assert.Equal("Ferdowsi", book.Author);
            Assert.Equal("Study Room", book.SpaceName);
            Assert.Equal("Shelf 2, Row 3", book.ShelfLocation);

            // 3. Search and filtering
            var searchResults = await library.GetBooksAsync(search: "Ferdowsi");
            Assert.Single(searchResults);

            var isbnResults = await library.GetBooksAsync(search: "978-964");
            Assert.Single(isbnResults);

            // 4. Per-person read status progression
            // Step A: Want to read
            var progress1 = await library.SetReadProgressAsync(new SetReadProgressRequest(
                book.Id, _personId, ReadStatus.WantToRead));
            Assert.Equal(ReadStatus.WantToRead, progress1.Status);

            // Step B: Reading with page tracking
            var progress2 = await library.SetReadProgressAsync(new SetReadProgressRequest(
                book.Id, _personId, ReadStatus.Reading, CurrentPage: 350));
            Assert.Equal(ReadStatus.Reading, progress2.Status);
            Assert.Equal(350, progress2.CurrentPage);
            Assert.NotNull(progress2.StartedAt);

            // Step C: Finished with review and rating
            var progress3 = await library.SetReadProgressAsync(new SetReadProgressRequest(
                book.Id, _personId, ReadStatus.Finished, CurrentPage: 1250, Rating: 5, Review: "Timeless Persian masterpiece."));
            Assert.Equal(ReadStatus.Finished, progress3.Status);
            Assert.Equal(5, progress3.Rating);
            Assert.Equal("Timeless Persian masterpiece.", progress3.Review);
            Assert.NotNull(progress3.FinishedAt);

            // Verify in GetBookById
            var bookWithProgress = await library.GetBookByIdAsync(book.Id);
            Assert.NotNull(bookWithProgress);
            Assert.Equal(1, bookWithProgress.ReadersCount);
            Assert.Single(bookWithProgress.ReadProgresses);
            Assert.Equal(ReadStatus.Finished, bookWithProgress.ReadProgresses[0].Status);
        }
    }

    [Fact]
    public async Task BookLending_ReminderAndReturn_WhoHasWhat_Works()
    {
        var (db, library, _) = CreateServices();
        using (db)
        {
            // 1. Create a book
            var book = await library.CreateBookAsync(new CreateBookRequest("Clean Architecture", "Robert C. Martin"));

            // 2. Loan book to a friend with due date in future
            var dueAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
            var loan = await library.LoanBookAsync(new CreateBookLoanRequest(
                BookId: book.Id,
                BorrowerName: "Ali Rezaei",
                BorrowerContact: "09121112233",
                DueAt: dueAt,
                Notes: "Borrowed for software study"));

            Assert.Equal(LoanStatus.Active, loan.Status);
            Assert.Equal("Ali Rezaei", loan.BorrowerName);
            Assert.False(loan.IsOverdue);

            // Check book status is now Loaned
            var updatedBook = await library.GetBookByIdAsync(book.Id);
            Assert.Equal(BookStatus.Loaned, updatedBook!.Status);

            // Check reminder was created
            var reminders = await db.Reminders.Where(r => r.HouseholdId == _householdId).ToListAsync();
            Assert.Contains(reminders, r => r.Title.Contains("Clean Architecture") && r.Title.Contains("Ali Rezaei"));

            // 3. Loaning already loaned book should throw
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                library.LoanBookAsync(new CreateBookLoanRequest(book.Id, "Another Person", dueAt)));

            // 4. Who-has-what view
            var whoHasWhat = await library.GetWhoHasWhatAsync();
            Assert.Single(whoHasWhat);
            Assert.Equal("Ali Rezaei", whoHasWhat[0].BorrowerName);
            Assert.Equal("Clean Architecture", whoHasWhat[0].BookTitle);

            // 5. Return book
            var returned = await library.ReturnBookAsync(loan.Id);
            Assert.Equal(LoanStatus.Returned, returned.Status);
            Assert.NotNull(returned.ReturnedAt);

            // Book is available again
            var finalBook = await library.GetBookByIdAsync(book.Id);
            Assert.Equal(BookStatus.Available, finalBook!.Status);

            // Who-has-what should now be empty
            var emptyWhoHasWhat = await library.GetWhoHasWhatAsync();
            Assert.Empty(emptyWhoHasWhat);
        }
    }

    [Fact]
    public async Task Media_MoviesAndSeries_WatchStatusAndRatings_Works()
    {
        var (db, _, media) = CreateServices();
        using (db)
        {
            // 1. Create movie & series
            var movie = await media.CreateMediaItemAsync(new CreateMediaItemRequest(
                Title: "Inception",
                Type: MediaType.Movie,
                Director: "Christopher Nolan",
                ReleaseYear: 2010,
                Overview: "A thief who steals corporate secrets through dream-sharing technology."));

            var series = await media.CreateMediaItemAsync(new CreateMediaItemRequest(
                Title: "Stranger Things",
                Type: MediaType.Series,
                Director: "Duffer Brothers",
                ReleaseYear: 2016));

            Assert.Equal(MediaType.Movie, movie.Type);
            Assert.Equal(MediaType.Series, series.Type);

            // 2. Set watch status for movie
            var progress = await media.SetPersonProgressAsync(new SetMediaProgressRequest(
                MediaItemId: movie.Id,
                PersonId: _personId,
                WatchStatus: WatchStatus.Completed,
                Rating: 9,
                Review: "Brilliant concept and execution."));

            Assert.Equal(WatchStatus.Completed, progress.WatchStatus);
            Assert.Equal(9, progress.Rating);
            Assert.Equal("Brilliant concept and execution.", progress.Review);

            // 3. Filter by watch status
            var completedMedia = await media.GetMediaItemsAsync(status: WatchStatus.Completed, personId: _personId);
            Assert.Single(completedMedia);
            Assert.Equal("Inception", completedMedia[0].Title);

            var planToWatchMedia = await media.GetMediaItemsAsync(status: WatchStatus.PlanToWatch, personId: _personId);
            Assert.Empty(planToWatchMedia);
        }
    }

    [Fact]
    public async Task Series_SeasonsAndEpisodes_NextToWatch_Works()
    {
        var (db, _, media) = CreateServices();
        using (db)
        {
            // 1. Create series
            var series = await media.CreateMediaItemAsync(new CreateMediaItemRequest(
                Title: "Breaking Bad",
                Type: MediaType.Series,
                Director: "Vince Gilligan",
                ReleaseYear: 2008));

            // 2. Generate 2 seasons with 3 episodes each (total 6 episodes)
            var generatedCount = await media.GenerateEpisodesAsync(new GenerateEpisodesRequest(
                MediaItemId: series.Id,
                SeasonCount: 2,
                EpisodesPerSeason: 3));

            Assert.Equal(6, generatedCount);

            var episodes = await media.GetEpisodesAsync(series.Id);
            Assert.Equal(6, episodes.Count);

            // 3. Next to watch should initially be Season 1 Episode 1
            var next1 = await media.GetNextToWatchAsync(series.Id, _personId);
            Assert.NotNull(next1);
            Assert.Equal(1, next1.SeasonNumber);
            Assert.Equal(1, next1.EpisodeNumber);

            // 4. Mark S01E01 as watched
            var ep1 = episodes.First(e => e.SeasonNumber == 1 && e.EpisodeNumber == 1);
            await media.SetEpisodeWatchedAsync(new SetEpisodeWatchedRequest(ep1.Id, _personId, true));

            // Next to watch should now be Season 1 Episode 2
            var next2 = await media.GetNextToWatchAsync(series.Id, _personId);
            Assert.NotNull(next2);
            Assert.Equal(1, next2.SeasonNumber);
            Assert.Equal(2, next2.EpisodeNumber);

            // 5. Mark remaining episodes of Season 1 as watched (S01E02, S01E03)
            var ep2 = episodes.First(e => e.SeasonNumber == 1 && e.EpisodeNumber == 2);
            var ep3 = episodes.First(e => e.SeasonNumber == 1 && e.EpisodeNumber == 3);
            await media.SetEpisodeWatchedAsync(new SetEpisodeWatchedRequest(ep2.Id, _personId, true));
            await media.SetEpisodeWatchedAsync(new SetEpisodeWatchedRequest(ep3.Id, _personId, true));

            // Next to watch should advance to Season 2 Episode 1
            var nextSeason = await media.GetNextToWatchAsync(series.Id, _personId);
            Assert.NotNull(nextSeason);
            Assert.Equal(2, nextSeason.SeasonNumber);
            Assert.Equal(1, nextSeason.EpisodeNumber);

            // Verify watched episodes count in GetMediaItemById
            var seriesDetails = await media.GetMediaItemByIdAsync(series.Id, _personId);
            Assert.NotNull(seriesDetails);
            Assert.Equal(3, seriesDetails.WatchedEpisodesCount);
            Assert.Equal(2, seriesDetails.NextToWatch?.SeasonNumber);
            Assert.Equal(1, seriesDetails.NextToWatch?.EpisodeNumber);
        }
    }
}
