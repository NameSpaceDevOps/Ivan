namespace Ivan.Domain.Enums;

public enum PersonKind
{
    Member = 1,
    Relative = 2,
    Pet = 3,
    External = 4
}

public enum HealthRecordType
{
    Vaccine = 1,
    Medication = 2,
    Allergy = 3,
    Condition = 4,
    Appointment = 5,
    Measurement = 6,
    Test = 7
}

public enum PersonDocumentType
{
    NationalId = 1,
    BirthCertificate = 2,
    Passport = 3,
    DriverLicense = 4,
    Insurance = 5,
    Education = 6,
    Contract = 7,
    Other = 99
}
