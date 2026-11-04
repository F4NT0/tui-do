using TuiDo.Domain;
using TuiDo.Services;

namespace TuiDo.Presentation;

/// <summary>The task list currently shown on the right plus the selected row, kept in range.</summary>
internal sealed class BrowseModel(AppState state, TodoService service)
{
    public bool Ranked => service.IsRanked(state.Menu, state.Query);

    public List<TodoItem> Visible()
    {
        var list = service.Visible(state.Menu, state.Query);
        state.Selected = Math.Clamp(state.Selected, 0, Math.Max(0, list.Count - 1));
        return list;
    }

    public TodoItem? SelectedItem(List<TodoItem> visible) => visible.Count > 0 ? visible[state.Selected] : null;
}
