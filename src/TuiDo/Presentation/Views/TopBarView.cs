using Spectre.Console;
using Spectre.Console.Rendering;
using TuiDo.Abstractions;
using TuiDo.Services;

namespace TuiDo.Presentation.Views;

internal static class TopBarView
{
    public const int Height = 3;
    private const int ClockWidth = 25;

    public static IRenderable Render(IClock clock, NotificationLog log, int width, string? directory = null)
    {
        var clockPanel = new Panel(new Markup($"[bold green]{clock.Now:dd/MM/yyyy HH:mm:ss}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("green"),
            Expand = true,
        };

        if (string.IsNullOrEmpty(directory))
            return new Layout("top").SplitColumns(
                new Layout("clock", clockPanel).Size(ClockWidth),
                new Layout("notes", NotificationPanel(log.Active, width)));

        var name = $"\uf07b {directory}";
        var dirWidth = Math.Clamp(name.Length + 4, 16, Math.Max(16, width / 3));
        if (name.Length + 4 > dirWidth) name = name[..Math.Max(1, dirWidth - 5)] + "…";
        var dirPanel = new Panel(new Markup($"[bold {Theme.Accent}]{Markup.Escape(name)}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(Theme.Accent),
            Expand = true,
        };

        return new Layout("top").SplitColumns(
            new Layout("clock", clockPanel).Size(ClockWidth),
            new Layout("gap", new Text(" ")).Size(1),
            new Layout("dir", dirPanel).Size(dirWidth),
            new Layout("notes", NotificationPanel(log.Active, width - dirWidth - 1)));
    }

    private static Panel NotificationPanel(Notification? note, int width)
    {
        if (note == null)
            return new Panel(new Markup(" ")) { Border = BoxBorder.Rounded, BorderStyle = Style.Parse("grey"), Expand = true };

        var max = Math.Max(10, width - ClockWidth - 6);
        var message = note.Message.Replace('\n', ' ');
        if (message.Length > max) message = message[..(max - 1)] + "…";

        var (color, icon) = note.Kind switch
        {
            NotificationKind.Warning => ("yellow", "\uea6c"),
            NotificationKind.Error => ("red", "\uf52f"),
            _ => ("green", "\uf05d"),
        };

        return new Panel(new Markup($"[{color}]{icon} {Markup.Escape(message)}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(color),
            Expand = true,
        };
    }
}
