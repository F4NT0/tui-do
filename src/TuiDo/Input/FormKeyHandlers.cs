using TuiDo.Abstractions;
using TuiDo.Domain;
using TuiDo.Presentation;
using TuiDo.Presentation.Forms;
using TuiDo.Services;

namespace TuiDo.Input;

internal sealed class TaskFormKeyHandler(AppState state, TodoService service, IClipboard clipboard, IKeySource keys, NotificationLog log) : IKeyHandler
{
    private const string TabAsSpaces = "    ";

    public bool IsActive => state.TaskForm != null;

    public void Handle(ConsoleKeyInfo key)
    {
        var form = state.TaskForm!;
        if (key.Key == ConsoleKey.Escape) { state.TaskForm = null; return; }
        if (form.Field == TaskForm.NotesField && KeyChords.IsEditorShortcut(key)) { state.EditorPick = 0; return; }

        if (KeyChords.Ctrl(key, ConsoleKey.A, (char)1)) { form.Time = TodoItem.AllDay; return; }

        var afterCarriageReturn = form.LastWasCarriageReturn;
        form.LastWasCarriageReturn = key.KeyChar == '\r';

        if (KeyChords.IsPaste(key)) { Paste(form); return; }
        if (key.KeyChar == '\n' && afterCarriageReturn) return;

        // Keys already queued behind this one mean the terminal is pasting: keep newlines/tabs as text instead of acting on them.
        if (form.Field == TaskForm.NotesField && key.Key == ConsoleKey.Tab && keys.Available) { form.Notes += TabAsSpaces; return; }
        if (key.Key == ConsoleKey.Enter && (key.Modifiers != 0 || key.KeyChar == '\n' || keys.Available))
        {
            if (form.Field == TaskForm.NotesField) form.Notes += "\n";
            return;
        }

        if (key.Key == ConsoleKey.Enter || KeyChords.IsSaveShortcut(key)) { Save(form); return; }

        form.Error = "";
        if (key.Key == ConsoleKey.Tab) { form.Field = (form.Field + 1) % TaskForm.FieldCount; return; }
        if (key.Key == ConsoleKey.UpArrow && form.Field > 0 && !(form.Field == TaskForm.NotesField && form.Notes.Contains('\n'))) { form.Field--; return; }
        if (form.Field == TaskForm.TimeField) { EditTime(form, key); return; }
        if (KeyChords.TryEdit(key, form.Value, out var edited)) form.Value = edited;
    }

    private static void EditTime(TaskForm form, ConsoleKeyInfo key)
    {
        var current = form.IsAllDay ? "" : form.Time;
        if (key.Key == ConsoleKey.Backspace) form.Time = current.Length > 0 ? current[..^1] : "";
        else if (current.Length < 5 && char.IsDigit(key.KeyChar)) form.Time = current.Length == 2 ? current + ":" + key.KeyChar : current + key.KeyChar;
        else if (current.Length is 1 or 2 && key.KeyChar == ':') form.Time = current + ":";
    }

    private void Paste(TaskForm form)
    {
        var text = clipboard.GetText();
        if (form.Field != TaskForm.NotesField) text = text.Replace("\n", " ");
        form.Value += text;
    }

    private void Save(TaskForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Title)) { Reject(form, "Title is required"); return; }
        if (!form.TryParseDate(out var date)) { Reject(form, "Invalid date (dd/MM/yyyy)", "Invalid date"); return; }

        if (!form.TryParseTime(out var time)) { Reject(form, "Invalid time (HH:mm)", "Invalid time"); return; }

        if (form.Existing is { } existing) service.UpdateTask(existing, form.Title, date, form.Notes, time);
        else service.CreateTask(form.Title, date, form.Notes, form.ListId, time);
        state.TaskForm = null;
    }

    private void Reject(TaskForm form, string inlineError, string? notification = null)
    {
        form.Error = inlineError;
        log.Error(notification ?? inlineError);
    }
}

internal sealed class ListFormKeyHandler(AppState state, TodoService service, IClipboard clipboard, NotificationLog log) : IKeyHandler
{
    public bool IsActive => state.ListForm != null;

    public void Handle(ConsoleKeyInfo key)
    {
        var form = state.ListForm!;
        if (form.IconPick is { } pick) { PickIcon(form, pick, key); return; }
        if (form.Field == ListForm.IconField && key.KeyChar is 'i' or 'I')
        {
            form.IconPick = Math.Max(0, Array.IndexOf(ListForm.Icons, form.Icon));
            return;
        }

        if (key.Key == ConsoleKey.Escape) { state.ListForm = null; return; }
        if (KeyChords.IsPaste(key)) { form.Value += clipboard.GetText().Replace("\n", " "); return; }

        if (key.Key == ConsoleKey.Enter)
        {
            if (string.IsNullOrWhiteSpace(form.Name))
            {
                form.Error = "Name is required";
                log.Error("List name is required");
                return;
            }

            state.Menu = Services.BuiltInLists.Count + service.AddList(form.Name, form.Icon);
            state.Selected = 0;
            state.ListForm = null;
            return;
        }

        form.Error = "";
        if (key.Key is ConsoleKey.Tab or ConsoleKey.UpArrow or ConsoleKey.DownArrow) { form.Field = 1 - form.Field; return; }
        if (KeyChords.TryEdit(key, form.Value, out var edited)) form.Value = edited;
    }

    private static void PickIcon(ListForm form, int pick, ConsoleKeyInfo key)
    {
        var last = ListForm.Icons.Length - 1;
        switch (key.Key)
        {
            case ConsoleKey.Escape: form.IconPick = null; break;
            case ConsoleKey.Enter: form.Icon = ListForm.Icons[pick]; form.IconPick = null; break;
            case ConsoleKey.LeftArrow: form.IconPick = Math.Max(0, pick - 1); break;
            case ConsoleKey.RightArrow: form.IconPick = Math.Min(last, pick + 1); break;
            case ConsoleKey.UpArrow: form.IconPick = Math.Max(0, pick - ListForm.IconColumns); break;
            case ConsoleKey.DownArrow: form.IconPick = Math.Min(last, pick + ListForm.IconColumns); break;
        }
    }
}
