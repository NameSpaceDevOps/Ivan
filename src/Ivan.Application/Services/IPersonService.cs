using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record PersonDto(
    Guid Id,
    PersonKind Kind,
    string DisplayName,
    string? FullName,
    DateOnly? BirthDate,
    string? Photo,
    string? Relationship,
    Guid? UserId,
    ResourceVisibility Visibility,
    string? ExtraJson);

public record CreatePersonRequest(
    PersonKind Kind,
    string DisplayName,
    string? FullName = null,
    DateOnly? BirthDate = null,
    string? Photo = null,
    string? Relationship = null,
    Guid? UserId = null,
    ResourceVisibility Visibility = ResourceVisibility.Household,
    string? ExtraJson = null);

public record HealthRecordDto(
    Guid Id,
    Guid PersonId,
    HealthRecordType Type,
    string Title,
    DateOnly Date,
    DateOnly? NextDueDate,
    string? DetailsJson,
    ResourceVisibility Visibility);

public record CreateHealthRecordRequest(
    Guid PersonId,
    HealthRecordType Type,
    string Title,
    DateOnly Date,
    DateOnly? NextDueDate = null,
    string? DetailsJson = null,
    ResourceVisibility Visibility = ResourceVisibility.Guardians);

public record EducationRecordDto(
    Guid Id,
    Guid PersonId,
    string Institution,
    string Level,
    int StartYear,
    int? EndYear,
    string? Notes);

public record CreateEducationRecordRequest(
    Guid PersonId,
    string Institution,
    string Level,
    int StartYear,
    int? EndYear = null,
    string? Notes = null);

public record PersonDocumentDto(
    Guid Id,
    Guid PersonId,
    PersonDocumentType Type,
    string? Number,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    ResourceVisibility Visibility);

public record CreatePersonDocumentRequest(
    Guid PersonId,
    PersonDocumentType Type,
    string? Number = null,
    DateOnly? IssueDate = null,
    DateOnly? ExpiryDate = null,
    ResourceVisibility Visibility = ResourceVisibility.Guardians);

public record ImportantDateDto(
    Guid Id,
    Guid? PersonId,
    string? PersonName,
    string Title,
    DateOnly Date,
    bool RepeatsYearly,
    string ReminderOffsets);

public record CreateImportantDateRequest(
    Guid? PersonId,
    string Title,
    DateOnly Date,
    bool RepeatsYearly = true,
    string ReminderOffsets = "0,1,7");

public record TimelineEventDto(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    DateOnly EventDate,
    string Kind,
    string Title,
    string? Body);

public record CreateTimelineEventRequest(
    string SubjectType,
    Guid SubjectId,
    DateOnly EventDate,
    string Kind,
    string Title,
    string? Body = null);

public interface IPersonService
{
    // Person
    Task<List<PersonDto>> GetPeopleAsync(PersonKind? kind = null, CancellationToken ct = default);
    Task<PersonDto?> GetPersonByIdAsync(Guid id, CancellationToken ct = default);
    Task<PersonDto> CreatePersonAsync(CreatePersonRequest request, CancellationToken ct = default);
    Task DeletePersonAsync(Guid id, CancellationToken ct = default);

    // Guardians
    Task AddGuardianAsync(Guid personId, Guid guardianPersonId, CancellationToken ct = default);
    Task RemoveGuardianAsync(Guid personId, Guid guardianPersonId, CancellationToken ct = default);
    Task<List<PersonDto>> GetGuardiansAsync(Guid personId, CancellationToken ct = default);
    Task<bool> IsUserGuardianOrPrivilegedAsync(Guid personId, CancellationToken ct = default);

    // Dossier - Health
    Task<List<HealthRecordDto>> GetHealthRecordsAsync(Guid personId, CancellationToken ct = default);
    Task<HealthRecordDto> CreateHealthRecordAsync(CreateHealthRecordRequest request, CancellationToken ct = default);

    // Dossier - Education
    Task<List<EducationRecordDto>> GetEducationRecordsAsync(Guid personId, CancellationToken ct = default);
    Task<EducationRecordDto> CreateEducationRecordAsync(CreateEducationRecordRequest request, CancellationToken ct = default);

    // Dossier - Documents
    Task<List<PersonDocumentDto>> GetDocumentsAsync(Guid personId, CancellationToken ct = default);
    Task<PersonDocumentDto> CreateDocumentAsync(CreatePersonDocumentRequest request, CancellationToken ct = default);

    // Important Dates & Birthdays
    Task<List<ImportantDateDto>> GetImportantDatesAsync(Guid? personId = null, CancellationToken ct = default);
    Task<ImportantDateDto> CreateImportantDateAsync(CreateImportantDateRequest request, CancellationToken ct = default);

    // Timeline
    Task<List<TimelineEventDto>> GetTimelineEventsAsync(string subjectType, Guid subjectId, CancellationToken ct = default);
    Task<TimelineEventDto> CreateTimelineEventAsync(CreateTimelineEventRequest request, CancellationToken ct = default);
}
