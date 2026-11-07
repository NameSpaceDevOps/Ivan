using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class PersonService : IPersonService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IReminderService _reminderService;
    private readonly IAuditService _auditService;

    public PersonService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IReminderService reminderService,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _reminderService = reminderService;
        _auditService = auditService;
    }

    public async Task<List<PersonDto>> GetPeopleAsync(PersonKind? kind = null, CancellationToken ct = default)
    {
        var query = _db.People.AsQueryable();

        if (kind.HasValue)
            query = query.Where(p => p.Kind == kind.Value);

        var list = await query.ToListAsync(ct);
        return list
            .OrderBy(p => p.DisplayName)
            .Select(MapToPersonDto)
            .ToList();
    }

    public async Task<PersonDto?> GetPersonByIdAsync(Guid id, CancellationToken ct = default)
    {
        var person = await _db.People.FindAsync([id], ct);
        return person == null ? null : MapToPersonDto(person);
    }

    public async Task<PersonDto> CreatePersonAsync(CreatePersonRequest request, CancellationToken ct = default)
    {
        var person = new Person
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Kind = request.Kind,
            DisplayName = request.DisplayName,
            FullName = request.FullName,
            BirthDate = request.BirthDate,
            Photo = request.Photo,
            Relationship = request.Relationship,
            UserId = request.UserId,
            Visibility = request.Visibility,
            ExtraJson = request.ExtraJson
        };

        _db.People.Add(person);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreatePerson", "Person", person.Id.ToString(), $"Created person {person.DisplayName} ({person.Kind})", ct);

        // If birth date is provided, create an ImportantDate for birthday
        if (person.BirthDate.HasValue)
        {
            var birthday = new ImportantDate
            {
                HouseholdId = person.HouseholdId,
                PersonId = person.Id,
                Title = $"{person.DisplayName}'s Birthday",
                Date = person.BirthDate.Value,
                RepeatsYearly = true,
                ReminderOffsets = "0,1,7"
            };
            _db.ImportantDates.Add(birthday);
            await _db.SaveChangesAsync(ct);
        }

        // Timeline event
        var timeline = new TimelineEvent
        {
            HouseholdId = person.HouseholdId,
            SubjectType = "Person",
            SubjectId = person.Id,
            EventDate = person.BirthDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Kind = "ProfileCreated",
            Title = $"Profile created for {person.DisplayName}"
        };
        _db.TimelineEvents.Add(timeline);
        await _db.SaveChangesAsync(ct);

        return MapToPersonDto(person);
    }

    public async Task DeletePersonAsync(Guid id, CancellationToken ct = default)
    {
        var person = await _db.People.FindAsync([id], ct);
        if (person != null)
        {
            _db.People.Remove(person);
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("DeletePerson", "Person", id.ToString(), $"Deleted person {person.DisplayName}", ct);
        }
    }

    public async Task AddGuardianAsync(Guid personId, Guid guardianPersonId, CancellationToken ct = default)
    {
        var existing = await _db.PersonGuardians
            .FirstOrDefaultAsync(g => g.PersonId == personId && g.GuardianPersonId == guardianPersonId, ct);

        if (existing == null)
        {
            var link = new PersonGuardian
            {
                HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
                PersonId = personId,
                GuardianPersonId = guardianPersonId
            };
            _db.PersonGuardians.Add(link);
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("AddGuardian", "PersonGuardian", link.Id.ToString(), $"Linked guardian {guardianPersonId} to person {personId}", ct);
        }
    }

    public async Task RemoveGuardianAsync(Guid personId, Guid guardianPersonId, CancellationToken ct = default)
    {
        var link = await _db.PersonGuardians
            .FirstOrDefaultAsync(g => g.PersonId == personId && g.GuardianPersonId == guardianPersonId, ct);

        if (link != null)
        {
            _db.PersonGuardians.Remove(link);
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("RemoveGuardian", "PersonGuardian", link.Id.ToString(), $"Removed guardian {guardianPersonId} from person {personId}", ct);
        }
    }

    public async Task<List<PersonDto>> GetGuardiansAsync(Guid personId, CancellationToken ct = default)
    {
        var guardianLinks = await _db.PersonGuardians
            .Include(g => g.GuardianPerson)
            .Where(g => g.PersonId == personId)
            .ToListAsync(ct);

        return guardianLinks
            .Where(g => g.GuardianPerson != null)
            .Select(g => MapToPersonDto(g.GuardianPerson!))
            .ToList();
    }

    public async Task<bool> IsUserGuardianOrPrivilegedAsync(Guid personId, CancellationToken ct = default)
    {
        var userId = _userContext.UserId;
        if (!userId.HasValue)
            return false;

        // Check if user is Owner or Parent
        var membership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.UserId == userId.Value, ct);

        if (membership != null && (membership.Role == MembershipRole.Owner || membership.Role == MembershipRole.Parent))
            return true;

        // Check if user is linked to a Person
        var currentPerson = await _db.People
            .FirstOrDefaultAsync(p => p.UserId == userId.Value, ct);

        if (currentPerson == null)
            return false;

        if (currentPerson.Id == personId)
            return true;

        // Check if currentPerson is a guardian of personId
        var isGuardian = await _db.PersonGuardians
            .AnyAsync(g => g.PersonId == personId && g.GuardianPersonId == currentPerson.Id, ct);

        return isGuardian;
    }

    public async Task<List<HealthRecordDto>> GetHealthRecordsAsync(Guid personId, CancellationToken ct = default)
    {
        var isPrivileged = await IsUserGuardianOrPrivilegedAsync(personId, ct);

        var query = _db.HealthRecords.Where(h => h.PersonId == personId);

        if (!isPrivileged)
        {
            // Non-guardians can only view household-visible records
            query = query.Where(h => h.Visibility == ResourceVisibility.Household);
        }

        var list = await query.ToListAsync(ct);
        return list
            .OrderByDescending(h => h.Date)
            .Select(h => new HealthRecordDto(
                h.Id,
                h.PersonId,
                h.Type,
                h.Title,
                h.Date,
                h.NextDueDate,
                h.DetailsJson,
                h.Visibility))
            .ToList();
    }

    public async Task<HealthRecordDto> CreateHealthRecordAsync(CreateHealthRecordRequest request, CancellationToken ct = default)
    {
        var record = new HealthRecord
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            Type = request.Type,
            Title = request.Title,
            Date = request.Date,
            NextDueDate = request.NextDueDate,
            DetailsJson = request.DetailsJson,
            Visibility = request.Visibility
        };

        _db.HealthRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateHealthRecord", "HealthRecord", record.Id.ToString(), $"Created {record.Type} health record: {record.Title}", ct);

        // If next due date, schedule reminder
        if (record.NextDueDate.HasValue)
        {
            var dueDateTime = new DateTimeOffset(record.NextDueDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var reminderTime = dueDateTime.AddDays(-1);
            if (reminderTime > DateTimeOffset.UtcNow)
            {
                await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                    "HealthRecord",
                    record.Id,
                    $"Health due: {record.Title}",
                    reminderTime,
                    $"health-{record.Id}"), ct);
            }
        }

        // Timeline
        var timeline = new TimelineEvent
        {
            HouseholdId = record.HouseholdId,
            SubjectType = "Person",
            SubjectId = record.PersonId,
            EventDate = record.Date,
            Kind = record.Type.ToString(),
            Title = $"Health record: {record.Title}",
            Body = record.DetailsJson
        };
        _db.TimelineEvents.Add(timeline);
        await _db.SaveChangesAsync(ct);

        return new HealthRecordDto(
            record.Id,
            record.PersonId,
            record.Type,
            record.Title,
            record.Date,
            record.NextDueDate,
            record.DetailsJson,
            record.Visibility);
    }

    public async Task<List<EducationRecordDto>> GetEducationRecordsAsync(Guid personId, CancellationToken ct = default)
    {
        var list = await _db.EducationRecords
            .Where(e => e.PersonId == personId)
            .ToListAsync(ct);

        return list
            .OrderByDescending(e => e.StartYear)
            .Select(e => new EducationRecordDto(
                e.Id,
                e.PersonId,
                e.Institution,
                e.Level,
                e.StartYear,
                e.EndYear,
                e.Notes))
            .ToList();
    }

    public async Task<EducationRecordDto> CreateEducationRecordAsync(CreateEducationRecordRequest request, CancellationToken ct = default)
    {
        var record = new EducationRecord
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            Institution = request.Institution,
            Level = request.Level,
            StartYear = request.StartYear,
            EndYear = request.EndYear,
            Notes = request.Notes
        };

        _db.EducationRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateEducationRecord", "EducationRecord", record.Id.ToString(), $"Created education record for {record.Institution}", ct);

        return new EducationRecordDto(
            record.Id,
            record.PersonId,
            record.Institution,
            record.Level,
            record.StartYear,
            record.EndYear,
            record.Notes);
    }

    public async Task<List<PersonDocumentDto>> GetDocumentsAsync(Guid personId, CancellationToken ct = default)
    {
        var isPrivileged = await IsUserGuardianOrPrivilegedAsync(personId, ct);

        var query = _db.PersonDocuments.Where(d => d.PersonId == personId);

        if (!isPrivileged)
        {
            query = query.Where(d => d.Visibility == ResourceVisibility.Household);
        }

        var list = await query.ToListAsync(ct);
        return list
            .OrderByDescending(d => d.IssueDate)
            .Select(d => new PersonDocumentDto(
                d.Id,
                d.PersonId,
                d.Type,
                d.Number,
                d.IssueDate,
                d.ExpiryDate,
                d.Visibility))
            .ToList();
    }

    public async Task<PersonDocumentDto> CreateDocumentAsync(CreatePersonDocumentRequest request, CancellationToken ct = default)
    {
        var doc = new PersonDocument
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            Type = request.Type,
            Number = request.Number,
            IssueDate = request.IssueDate,
            ExpiryDate = request.ExpiryDate,
            Visibility = request.Visibility
        };

        _db.PersonDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreatePersonDocument", "PersonDocument", doc.Id.ToString(), $"Created document {doc.Type}", ct);

        // If expiry date, schedule document-expiry reminder
        if (doc.ExpiryDate.HasValue)
        {
            var expiryDateTime = new DateTimeOffset(doc.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var reminderTime = expiryDateTime.AddDays(-30);
            if (reminderTime > DateTimeOffset.UtcNow)
            {
                await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                    "PersonDocumentExpiry",
                    doc.Id,
                    $"Document expiring: {doc.Type}",
                    reminderTime,
                    $"doc-expiry-{doc.Id}"), ct);
            }
        }

        return new PersonDocumentDto(
            doc.Id,
            doc.PersonId,
            doc.Type,
            doc.Number,
            doc.IssueDate,
            doc.ExpiryDate,
            doc.Visibility);
    }

    public async Task<List<ImportantDateDto>> GetImportantDatesAsync(Guid? personId = null, CancellationToken ct = default)
    {
        var query = _db.ImportantDates
            .Include(d => d.Person)
            .AsQueryable();

        if (personId.HasValue)
            query = query.Where(d => d.PersonId == personId.Value);

        var list = await query.ToListAsync(ct);
        return list
            .OrderBy(d => d.Date)
            .Select(d => new ImportantDateDto(
                d.Id,
                d.PersonId,
                d.Person?.DisplayName,
                d.Title,
                d.Date,
                d.RepeatsYearly,
                d.ReminderOffsets))
            .ToList();
    }

    public async Task<ImportantDateDto> CreateImportantDateAsync(CreateImportantDateRequest request, CancellationToken ct = default)
    {
        var item = new ImportantDate
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PersonId = request.PersonId,
            Title = request.Title,
            Date = request.Date,
            RepeatsYearly = request.RepeatsYearly,
            ReminderOffsets = request.ReminderOffsets
        };

        _db.ImportantDates.Add(item);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateImportantDate", "ImportantDate", item.Id.ToString(), $"Created important date: {item.Title}", ct);

        var person = request.PersonId.HasValue ? await _db.People.FindAsync([request.PersonId.Value], ct) : null;
        return new ImportantDateDto(
            item.Id,
            item.PersonId,
            person?.DisplayName,
            item.Title,
            item.Date,
            item.RepeatsYearly,
            item.ReminderOffsets);
    }

    public async Task<List<TimelineEventDto>> GetTimelineEventsAsync(string subjectType, Guid subjectId, CancellationToken ct = default)
    {
        var list = await _db.TimelineEvents
            .Where(t => t.SubjectType == subjectType && t.SubjectId == subjectId)
            .ToListAsync(ct);

        return list
            .OrderByDescending(t => t.EventDate)
            .Select(t => new TimelineEventDto(
                t.Id,
                t.SubjectType,
                t.SubjectId,
                t.EventDate,
                t.Kind,
                t.Title,
                t.Body))
            .ToList();
    }

    public async Task<TimelineEventDto> CreateTimelineEventAsync(CreateTimelineEventRequest request, CancellationToken ct = default)
    {
        var ev = new TimelineEvent
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            EventDate = request.EventDate,
            Kind = request.Kind,
            Title = request.Title,
            Body = request.Body
        };

        _db.TimelineEvents.Add(ev);
        await _db.SaveChangesAsync(ct);

        return new TimelineEventDto(ev.Id, ev.SubjectType, ev.SubjectId, ev.EventDate, ev.Kind, ev.Title, ev.Body);
    }

    private static PersonDto MapToPersonDto(Person p) => new(
        p.Id,
        p.Kind,
        p.DisplayName,
        p.FullName,
        p.BirthDate,
        p.Photo,
        p.Relationship,
        p.UserId,
        p.Visibility,
        p.ExtraJson);
}
