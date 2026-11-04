using Spectre.Console;
using Spectre.Console.Rendering;
using TuiDo.Domain;
using Status = TuiDo.Domain.Status;
using TuiDo.Presentation.Documents;

namespace TuiDo.Presentation.Views;

internal static class TaskDetailView
{
    /// <summary>Rows consumed around the scrolling markdown: top bar, footer, panel borders and the 3 header lines.</summary>
    public const int ChromeHeight = TopBarView.Height + 1 + 2 + 3;

    public static string Title(TodoItem item) => $"#{item.Code} {item.Title}";

    public static IRenderable Render(IAnsiConsole console, AppState state, TodoItem item, DateTime today, int width, int height)
    {
        var statusName = item.IsOverdue(today) ? "Overdue" : StatusCatalog.Name(item.Status);
        var waiting = item.Status == Status.Blocked && item.BlockedBy != null ? $"  ·  waiting for [bold]#{item.BlockedBy}[/]" : "";
        var notes = string.IsNullOrWhiteSpace(item.Notes) ? "*No notes.*" : item.Notes;

        var view = new ScrollView(MarkdownRenderer.Render(notes), Math.Max(3, height - ChromeHeight), state.Scroll);
        view.Prepare(console, Math.Max(10, width - SidebarView.Width - 4));
        state.View = view;
        var percent = view.Total <= view.Visible ? "" : $"  ·  {(int)(100.0 * (state.Scroll + view.Visible) / Math.Max(1, view.Total))}%";

        return new Rows(
            new Markup($"{Theme.ItemIcon(item, today)}  [bold]{Markup.Escape(item.Title)}[/]  {Theme.Star(item.Important)}"),
            new Markup($"[grey]#{item.Code}  ·  {item.Date:dd/MM/yyyy}  ·  {statusName}{waiting}{percent}[/]"),
            new Text(""),
            view);
    }
}
