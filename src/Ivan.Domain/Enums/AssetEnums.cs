namespace Ivan.Domain.Enums;

public enum AssetStatus
{
    InUse = 1,
    Maintenance = 2,
    Stored = 3,
    Disposed = 4
}

public enum ServiceRecordType
{
    Routine = 1,
    Repair = 2,
    Inspection = 3,
    Replacement = 4,
    Other = 99
}

public enum UtilityKind
{
    Electricity = 1,
    Water = 2,
    Gas = 3,
    Internet = 4,
    Phone = 5,
    MaintenanceFee = 6,
    Other = 99
}

public enum ContactCategory
{
    Emergency = 1,
    Service = 2,
    Doctor = 3,
    School = 4,
    Family = 5,
    Neighbor = 6,
    Other = 99
}
