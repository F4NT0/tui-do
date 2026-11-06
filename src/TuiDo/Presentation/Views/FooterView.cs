namespace TuiDo.Presentation.Views;

/// <summary>Shows only the shortcuts that matter for whatever currently has the focus.</summary>
internal static class FooterView
{
    public static string Text(AppState s) =>
        s.HelpTab != null ? "←→ / 1-4: switch tab | Esc or ?: close"
        : s.ShowPath ? "Enter: open folder | Esc/o: close"
        : s.Calendar != null ? "←→↑↓: day | PgUp/PgDn: month | t: today | Enter: day tasks | Esc: close"
        : s.TaskForm != null ? "Enter: save | Shift+Enter: new line (About) | Ctrl+V: paste | Ctrl+E: open About in editor | Ctrl+A: All Day | Tab: next field | Esc: cancel"
        : s.DirPick != null ? "↑↓: move | →: enter | ←: up | Enter: open directory | Esc: cancel"
        : s.ListForm is { IconPick: not null } ? "←→↑↓: move | Enter: choose icon | Esc: back"
        : s.ListForm != null ? "Enter: create | Tab: next field | i: icon list (Icon field) | Ctrl+V: paste | Esc: cancel"
        : s.StatusPick != null || s.BlockPick != null || s.MovePick != null || s.EditorPick != null ? "↑↓: select | Enter: confirm | Esc: cancel"
        : s.PriorityInput != null ? "Type a position | Enter: confirm | Esc: cancel"
        : s.Reorder ? "↑↓: move task | Enter/p: done"
        : s.Searching ? $"/{s.Query}▏  (Enter: keep results | Esc: cancel)"
        : s.Detail ? "↑↓: scroll | e: edit | Esc: back | ?: help"
        : s.FocusRight ? "↑↓: select | Enter: open | n: new | c: calendar | space: status | ?: help"
        : "↑↓: select | Enter: open | n: new | l: new list | ?: help | q: quit";
}
