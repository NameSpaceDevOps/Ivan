namespace Ivan.Domain.Enums;

public enum MembershipRole
{
    Owner = 1,
    Parent = 2,
    Teen = 3,
    Child = 4,
    External = 5
}

public enum GrantPermission
{
    View = 1,
    Edit = 2,
    Manage = 3
}

public enum ResourceVisibility
{
    Private = 1,     // Owner only
    Guardians = 2,   // Guardians of the person / child
    Selected = 3,    // Explicit AccessGrants
    Household = 4    // Entire household
}

public enum PropertyType
{
    Home = 1,
    Villa = 2,
    Garden = 3,
    Warehouse = 4,
    Office = 5,
    Other = 6
}

public enum SpaceType
{
    Room = 1,
    LivingRoom = 2,
    Kitchen = 3,
    Bathroom = 4,
    Storage = 5,
    Garage = 6,
    Yard = 7,
    Balcony = 8,
    Attic = 9,
    Basement = 10,
    Other = 99
}
