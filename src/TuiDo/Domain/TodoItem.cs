namespace TuiDo.Domain;

internal sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Code { get; set; }
    public string Title { get; set; } = "";
    public DateTime Date { get; set; }
    public string Notes { get; set; } = "";
    public Status Status { get; set; } = Status.Pending;
    public bool Important { get; set; }
    public int? BlockedBy { get; set; }
    public Guid? ListId { get; set; }
    public int Priority { get; set; }

    /// <summary>Time of day; null means "All Day".</summary>
    public TimeSpan? Time { get; set; }

    public static string AllDay => "All Day";

    public string TimeText => Time is { } t ? t.ToString(@"hh\:mm") : AllDay;

    public bool IsOpen() => StatusCatalog.IsOpen(Status);

    public bool IsOverdue(DateTime today) => Status == Status.Pending && Date.Date < today.Date;
}
