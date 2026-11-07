namespace Ivan.Domain.Enums;

public enum BookStatus
{
    Available = 1,
    Loaned = 2,
    Lost = 3,
    Wishlist = 4
}

public enum ReadStatus
{
    WantToRead = 1,
    Reading = 2,
    Finished = 3,
    Abandoned = 4
}

public enum LoanStatus
{
    Active = 1,
    Returned = 2,
    Overdue = 3
}

public enum MediaType
{
    Movie = 1,
    Series = 2,
    Anime = 3,
    Documentary = 4
}

public enum WatchStatus
{
    PlanToWatch = 1,
    Watching = 2,
    Completed = 3,
    Dropped = 4,
    OnHold = 5
}
