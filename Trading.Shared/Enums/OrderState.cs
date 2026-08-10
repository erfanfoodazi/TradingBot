namespace Trading.Shared.Enums;

public enum OrderState
{
    Requested = 0,
    Placed = 1,
    Accepted = 2,
    Started = 3,
    Filled = 4,
    Canceled = 5,
    Rejected = 6,
    Expired = 7,
    Unknown = 8,
}