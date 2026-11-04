using Spectre.Console;
using Spectre.Console.Rendering;
using TuiDo.Abstractions;
using TuiDo.Services;

namespace TuiDo.Presentation.Views;

/// <summary>Turns the state + data into one full-screen renderable. Never mutates anything but the scroll view cache.</summary>
internal sealed class AppView(AppState state, BrowseModel browse, TodoService service, IEditorLauncher editors, IClock clock, NotificationLog log)
{
    /// <param name="width">Terminal width in cells.</param>
    /// <param name="height">Rows available for the whole layout (footer included).</param>
    public IRenderable Render(IAnsiConsole console, int width, int height)
    {
        var list = browse.Visible();
        var today = clock.Today;

        var title = state.Query != null ? $"Search: {state.Query}" : service.MenuName(state.Menu);
        IRenderable content = TaskListView.Render(state, list, browse.Ranked, today);
        if (state.Detail && list.Count > 0)
        {
            var item = list[state.Selected];
            title = TaskDetailView.Title(item);
            content = TaskDetailView.Render(console, state, item, today, width, height);
        }

        var modal = CurrentModal(list.Count, today);
        if (modal != null) content = Align.Center(modal, VerticalAlignment.Middle);

        var modalOpen = modal != null;
        var rightPanel = new Panel(content)
        {
            Header = Theme.Header(title),
            Border = BoxBorder.Rounded,
            Expand = true,
            BorderStyle = Style.Parse(state.FocusRight && !modalOpen ? Theme.Accent : "grey"),
        };

        var left = new Layout("left").SplitRows(
            new Layout("lists", SidebarView.Lists(state, service, modalOpen)).Size(SidebarView.ListsPanelHeight),
            new Layout("custom", SidebarView.Custom(state, service, modalOpen))).Size(SidebarView.Width);

        return new Layout("root").SplitRows(
            new Layout("topbar", TopBarView.Render(clock, log, width)).Size(TopBarView.Height),
            new Layout("body").SplitColumns(left, new Layout("right", rightPanel)),
            new Layout("footer", new Markup($"[grey]{Markup.Escape(FooterView.Text(state))}[/]")).Size(1));
    }

    private IRenderable? CurrentModal(int visibleCount, DateTime today)
    {
        if (state.EditorPick is { } editor) return Modals.Editors(editors.Editors, editor);
        if (state.Calendar is { } day) return Modals.Calendar(service, day, today);
        if (state.ShowPath) return Modals.DataLocation(service.ItemsPath, service.ListsPath);
        if (state.HelpTab is { } tab) return Modals.Help(tab);
        if (state.TaskForm is { } taskForm) return Modals.TaskEditor(taskForm);
        if (state.ListForm is { } listForm) return Modals.NewList(listForm);
        if (state.MovePick is { } move) return Modals.MoveToList(service.Lists, move);
        if (state.StatusPick is { } status) return Modals.StatusPicker(status);
        if (state.PriorityInput is { } input) return Modals.Priority(input, visibleCount);
        if (state.BlockPick is { } block) return Modals.BlockedBy(state.BlockerCandidates, block, today);
        return null;
    }
}
