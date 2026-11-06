using Spectre.Console;
using TuiDo.Domain;
using Status = TuiDo.Domain.Status;

namespace TuiDo.Presentation;

internal static class Theme
{
    public const string Accent = "deepskyblue1";
    public const string Orange = "#e8912d";
    public const string Muted = "#6e7681";
    public const string Cyan = "#8be9fd";

    public static readonly string[] MenuIcons =
    [
        "[yellow]\uf522[/]", "[#c4a7e7]\uf412[/]", "[orange1]\udb81\udcce[/]", "[green]\uf05d[/]", "[red]\uf52f[/]",
    ];

    public static PanelHeader Header(string text) => new("\u2800" + text + "\u2800");

    public static string Star(bool on) => on ? "[orange1]\udb81\udcce[/]" : "[grey]\uf41e[/]";

    public static string StatusColor(Status status) => status switch
    {
        Status.Done => "green",
        Status.Cancelled => "red",
        Status.Waiting => "hotpink",
        Status.Event => "#B027F5",
        Status.Doing => "orange1",
        Status.Blocked => "grey",
        _ => "blue",
    };

    public static string StatusGlyph(Status status) => status switch
    {
        Status.Done => "\uf05d",
        Status.Cancelled => "\uf52f",
        Status.Waiting => "\uf29c",
        Status.Event => "\uf2be",
        Status.Doing => "\uf192",
        Status.Blocked => "\udb80\udcad",
        _ => "\uf4aa",
    };

    public static string StatusIcon(Status status) => $"[{StatusColor(status)}]{StatusGlyph(status)}[/]";

    public static string ItemIcon(TodoItem item, DateTime today) =>
        item.IsOverdue(today) ? "[yellow]\uea6c[/]" : StatusIcon(item.Status);

    public static string ListIcon(CustomList list) => $"[{Cyan}]{Markup.Escape(list.Icon)}[/]";
}
