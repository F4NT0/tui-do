namespace TuiDo.Domain;

internal enum Status
{
    Pending = 0,
    Done = 2,
    Cancelled = 3,
    Waiting = 4,
    Blocked = 5,
    Doing = 6,
}

internal static class StatusCatalog
{
    public static IReadOnlyList<Status> Ordered { get; } =
        [Status.Pending, Status.Doing, Status.Waiting, Status.Blocked, Status.Done, Status.Cancelled];

    public static string Name(Status status) => status switch
    {
        Status.Pending => "Not started",
        Status.Doing => "Doing",
        Status.Waiting => "Need more details",
        Status.Blocked => "Blocked",
        Status.Done => "Completed",
        Status.Cancelled => "Cancelled",
        _ => status.ToString(),
    };

    public static bool IsOpen(Status status) => status is not (Status.Done or Status.Cancelled);
}
