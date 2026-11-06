using TuiDo.Abstractions;
using TuiDo.Domain;
using TuiDo.Presentation;
using TuiDo.Presentation.Forms;
using TuiDo.Services;

namespace TuiDo.Input;

/// <summary>Default handler: moving between panels and lists and launching every action.</summary>
internal sealed class MainKeyHandler(AppState state, BrowseModel browse, TodoService service, IClock clock, GlobalShortcuts shortcuts, bool canSwitchDirectory = false) : IKeyHandler
{
    public bool IsActive => true;

    public void Handle(ConsoleKeyInfo key)
    {
        var list = browse.Visible();
        var item = browse.SelectedItem(list);

        if (canSwitchDirectory && !state.FocusRight && key.KeyChar == 'd') { state.DirPick = new DirectoryBrowser(service.DataDirectory); return; }

        if (shortcuts.TryHandle(key)) return;
        if (key.KeyChar == 'P' && state.FocusRight && browse.Ranked && item != null) { state.PriorityInput = ""; return; }
        if (key.Key == ConsoleKey.P && state.FocusRight && browse.Ranked && item != null) { state.Reorder = true; return; }

        switch (key.Key)
        {
            case ConsoleKey.Q: state.Quit = true; break;
            case ConsoleKey.N: state.TaskForm = TaskForm.ForNew(clock.Today, CurrentCustomListId()); break;
            case ConsoleKey.L: state.ListForm = new ListForm(); break;
            case ConsoleKey.M when item != null: OpenMovePicker(item); break;
            case ConsoleKey.X when !state.FocusRight && service.IsCustom(state.Menu): DeleteSelectedList(); break;
            case ConsoleKey.E when item != null: state.TaskForm = TaskForm.ForEdit(item); break;
            case ConsoleKey.UpArrow or ConsoleKey.K: MoveCursor(-1, list.Count); break;
            case ConsoleKey.DownArrow or ConsoleKey.J: MoveCursor(1, list.Count); break;
            case ConsoleKey.Enter when state.FocusRight && item != null: state.Detail = true; state.Scroll = 0; break;
            case ConsoleKey.RightArrow or ConsoleKey.Enter: state.FocusRight = true; break;
            case ConsoleKey.Tab: state.FocusRight = !state.FocusRight; break;
            case ConsoleKey.Escape when state.Query != null: state.Query = null; state.Selected = 0; break;
            case ConsoleKey.LeftArrow or ConsoleKey.Escape: state.FocusRight = false; break;
            case ConsoleKey.Spacebar when item != null: OpenStatusPicker(item); break;
            case ConsoleKey.I when item != null: service.ToggleImportant(item); break;
            case ConsoleKey.D when item != null: service.Delete(item); break;
        }
    }

    private Guid? CurrentCustomListId() =>
        service.IsCustom(state.Menu) && state.Query == null ? service.CustomAt(state.Menu).Id : null;

    private void MoveCursor(int delta, int count)
    {
        if (state.FocusRight) state.Selected = Math.Clamp(state.Selected + delta, 0, Math.Max(0, count - 1));
        else
        {
            state.Menu = Math.Clamp(state.Menu + delta, 0, service.MenuCount - 1);
            state.Selected = 0;
        }
    }

    private void DeleteSelectedList()
    {
        service.DeleteList(service.CustomAt(state.Menu));
        state.Menu = Math.Min(state.Menu, service.MenuCount - 1);
        state.Selected = 0;
    }

    private void OpenMovePicker(TodoItem item) => MovePickerOpener.Open(state, service, item);

    private void OpenStatusPicker(TodoItem item) => StatusPickerOpener.Open(state, item);
}

internal static class StatusPickerOpener
{
    public static void Open(AppState state, TodoItem item)
    {
        state.Target = item;
        state.StatusPick = Math.Max(0, StatusCatalog.Ordered.ToList().IndexOf(item.Status));
    }
}

internal static class MovePickerOpener
{
    public static void Open(AppState state, TodoService service, TodoItem item)
    {
        state.Target = item;
        var index = item.ListId == null ? 0 : 1 + Math.Max(0, service.Lists.ToList().FindIndex(l => l.Id == item.ListId));
        state.MovePick = index;
    }
}

/// <summary>Task details: scrolling the markdown preview and the few actions that still make sense there.</summary>
internal sealed class DetailKeyHandler(AppState state, BrowseModel browse, TodoService service, GlobalShortcuts shortcuts) : IKeyHandler
{
    public bool IsActive => state.Detail;

    public void Handle(ConsoleKeyInfo key)
    {
        var item = browse.SelectedItem(browse.Visible());
        if (shortcuts.TryHandle(key)) return;

        if (item == null || key.Key == ConsoleKey.Escape) { state.Detail = false; return; }
        switch (key.Key)
        {
            case ConsoleKey.E: state.TaskForm = TaskForm.ForEdit(item); return;
            case ConsoleKey.Spacebar: StatusPickerOpener.Open(state, item); return;
            case ConsoleKey.M: MovePickerOpener.Open(state, service, item); return;
        }

        if (state.View is not { } view) return;
        var max = Math.Max(0, view.Total - view.Visible);
        state.Scroll = Math.Clamp(key.Key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.K => state.Scroll - 1,
            ConsoleKey.DownArrow or ConsoleKey.J => state.Scroll + 1,
            ConsoleKey.PageUp => state.Scroll - view.Visible + 1,
            ConsoleKey.PageDown => state.Scroll + view.Visible - 1,
            ConsoleKey.Home => 0,
            ConsoleKey.End => max,
            _ => state.Scroll,
        }, 0, max);
    }
}

/// <summary>"p" mode: arrow keys move the selected task up and down the ranked list.</summary>
internal sealed class ReorderKeyHandler(AppState state, BrowseModel browse, TodoService service, GlobalShortcuts shortcuts) : IKeyHandler
{
    public bool IsActive => state.Reorder;

    public void Handle(ConsoleKeyInfo key)
    {
        if (shortcuts.TryHandle(key)) return;

        var list = browse.Visible();
        switch (key.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.K: state.Selected = service.Reorder(list, state.Selected, state.Selected - 1); break;
            case ConsoleKey.DownArrow or ConsoleKey.J: state.Selected = service.Reorder(list, state.Selected, state.Selected + 1); break;
            case ConsoleKey.Enter or ConsoleKey.Escape or ConsoleKey.P: state.Reorder = false; break;
        }
    }
}
