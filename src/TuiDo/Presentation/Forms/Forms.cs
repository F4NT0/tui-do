using System.Globalization;
using TuiDo.Domain;

namespace TuiDo.Presentation.Forms;

internal sealed class TaskForm
{
    public const string DateFormat = "dd/MM/yyyy";
    private static readonly string[] AcceptedDateFormats = [DateFormat, "yyyy-MM-dd", "d/M/yyyy"];

    private TaskForm(TodoItem? existing, string title, string date, string time, string notes, Guid? listId)
    {
        Existing = existing;
        Title = title;
        Date = date;
        Time = time;
        Notes = notes;
        ListId = listId;
    }

    public const int TitleField = 0;
    public const int DateField = 1;
    public const int TimeField = 2;
    public const int NotesField = 3;
    public const int FieldCount = 4;
    private static readonly string[] AcceptedTimeFormats = ["HH:mm", "H:mm"];

    public TodoItem? Existing { get; }

    public Guid? ListId { get; }

    public bool Editing => Existing != null;

    public string Title { get; set; }

    public string Date { get; set; }

    public string Time { get; set; }

    public string Notes { get; set; }

    public int Field { get; set; }

    public bool LastWasCarriageReturn { get; set; }

    public string Error { get; set; } = "";

    public string Value
    {
        get => Field switch { TitleField => Title, DateField => Date, TimeField => Time, _ => Notes };
        set
        {
            switch (Field)
            {
                case TitleField: Title = value; break;
                case DateField: Date = value; break;
                case TimeField: Time = value; break;
                default: Notes = value; break;
            }
        }
    }

    public static TaskForm ForNew(DateTime today, Guid? listId) => new(null, "", today.ToString(DateFormat, CultureInfo.InvariantCulture), TodoItem.AllDay, "", listId);

    public static TaskForm ForEdit(TodoItem item) =>
        new(item, item.Title, item.Date.ToString(DateFormat, CultureInfo.InvariantCulture), item.TimeText, item.Notes, item.ListId);

    public bool IsAllDay => Time.Trim().Length == 0 || Time.Trim().Equals(TodoItem.AllDay, StringComparison.OrdinalIgnoreCase);

    /// <summary>Parses the time field; <paramref name="time"/> is null for "All Day".</summary>
    public bool TryParseTime(out TimeSpan? time)
    {
        time = null;
        if (IsAllDay) return true;
        if (!DateTime.TryParseExact(Time.Trim(), AcceptedTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        time = parsed.TimeOfDay;
        return true;
    }

    public bool TryParseDate(out DateTime date) =>
        DateTime.TryParseExact(Date.Trim(), AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

internal sealed class ListForm
{
    public const int NameField = 0;
    public const int IconField = 1;

    public string Name { get; set; } = "";

    public string Icon { get; set; } = "";

    /// <summary>Index of the highlighted icon while the icon picker is open; null when closed.</summary>
    public int? IconPick { get; set; }

    public static readonly string[] Icons =
    [
        "\uf510", "\uf511", "\uf513", "\udb82\ude12", "\udb83\udecd", "\udb83\udcc7",
        "\udb84\udfdb", "\udb83\udcca", "\udb86\udcbd", "\udb83\udccb", "\udb83\udccc", "\udb86\udc29",
        "\udb86\udcbf", "\udb84\udc5e", "\udb84\udd3c", "\udb86\udd9b", "\udb83\udd34", "\udb84\udda1",
    ];

    public const int IconColumns = 6;

    public int Field { get; set; }

    public string Error { get; set; } = "";

    public string Value
    {
        get => Field == NameField ? Name : Icon;
        set
        {
            if (Field == NameField) Name = value;
            else Icon = value;
        }
    }
}
