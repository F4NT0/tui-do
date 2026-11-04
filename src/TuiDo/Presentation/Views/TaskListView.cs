using Spectre.Console;
using Spectre.Console.Rendering;
using TuiDo.Domain;
using Status = TuiDo.Domain.Status;

namespace TuiDo.Presentation.Views;

internal static class TaskListView
{
    public static IRenderable Render(AppState state, IReadOnlyList<TodoItem> list, bool ranked, DateTime today)
    {
        if (list.Count == 0) return new Markup("[grey]No tasks. Press [bold]n[/] to create one.[/]");

        var rows = new List<IRenderable>();
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            var selected = i == state.Selected && state.FocusRight;

            var title = Markup.Escape(item.Title);
            if (!item.IsOpen()) title = $"[strikethrough grey]{title}[/]";
            else if (selected) title = $"[bold green]{title}[/]";

            var rank = ranked ? $"[grey]{i + 1,2}[/] " : "";
            var marker = selected && state.Reorder ? "[orange1]↕[/]" : selected ? "[orange1]>[/]" : " ";
            var blocked = item.Status == Status.Blocked && item.BlockedBy != null ? $"  [grey]blocked by #{item.BlockedBy}[/]" : "";
            var notes = string.IsNullOrWhiteSpace(item.Notes) ? "" : "  \uf249 notes";

            rows.Add(new Markup($"{marker} {rank}{Theme.ItemIcon(item, today)}  [grey]#{item.Code}[/] {title}  {Theme.Star(item.Important)}"));
            rows.Add(new Markup($"     {(ranked ? "   " : "")}[grey]{item.Date:dd/MM/yyyy}{notes}[/]{blocked}"));
        }

        return new Rows(rows);
    }
}
