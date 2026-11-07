using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Person : BaseTenantEntity
{
    public PersonKind Kind { get; set; } = PersonKind.Member;
    public string DisplayName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Photo { get; set; }
    public string? Relationship { get; set; }
    public Guid? UserId { get; set; }
    public ResourceVisibility Visibility { get; set; } = ResourceVisibility.Household;
    public string? ExtraJson { get; set; }

    // Navigation
    public ICollection<HealthRecord> HealthRecords { get; set; } = new List<HealthRecord>();
    public ICollection<EducationRecord> EducationRecords { get; set; } = new List<EducationRecord>();
    public ICollection<PersonDocument> Documents { get; set; } = new List<PersonDocument>();
    public ICollection<ImportantDate> ImportantDates { get; set; } = new List<ImportantDate>();
    public ICollection<PersonGuardian> Guardians { get; set; } = new List<PersonGuardian>();
    public ICollection<PersonGuardian> WardOf { get; set; } = new List<PersonGuardian>();
}
