using System.Globalization;
using TuiDo.Domain;

namespace TuiDo.Presentation.Forms;

internal sealed class TaskForm
{
    public const string DateFormat = "dd/MM/yyyy";
    private static readonly string[] AcceptedDateFormats = [DateFormat, "yyyy-MM-dd", "d/M/yyyy"];

    private TaskForm(TodoItem? existing, string title, string date, string notes, Guid? listId)
    {
        Existing = existing;
        Title = title;
        Date = date;
        Notes = notes;
        ListId = listId;
    }

    public const int TitleField = 0;
    public const int DateField = 1;
    public const int NotesField = 2;
    public const int FieldCount = 3;

    public TodoItem? Existing { get; }

    public Guid? ListId { get; }

    public bool Editing => Existing != null;

    public string Title { get; set; }

    public string Date { get; set; }

    public string Notes { get; set; }

    public int Field { get; set; }

    public bool LastWasCarriageReturn { get; set; }

    public string Error { get; set; } = "";

    public string Value
    {
        get => Field switch { TitleField => Title, DateField => Date, _ => Notes };
        set
        {
            switch (Field)
            {
                case TitleField: Title = value; break;
                case DateField: Date = value; break;
                default: Notes = value; break;
            }
        }
    }

    public static TaskForm ForNew(DateTime today, Guid? listId) => new(null, "", today.ToString(DateFormat, CultureInfo.InvariantCulture), "", listId);

    public static TaskForm ForEdit(TodoItem item) =>
        new(item, item.Title, item.Date.ToString(DateFormat, CultureInfo.InvariantCulture), item.Notes, item.ListId);

    public bool TryParseDate(out DateTime date) =>
        DateTime.TryParseExact(Date.Trim(), AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

internal sealed class ListForm
{
    public const int NameField = 0;
    public const int IconField = 1;

    public string Name { get; set; } = "";

    public string Icon { get; set; } = "";

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
