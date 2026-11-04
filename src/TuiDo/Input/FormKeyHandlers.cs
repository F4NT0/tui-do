using TuiDo.Abstractions;
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
        if (KeyChords.TryEdit(key, form.Value, out var edited)) form.Value = edited;
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

        if (form.Existing is { } existing) service.UpdateTask(existing, form.Title, date, form.Notes);
        else service.CreateTask(form.Title, date, form.Notes, form.ListId);
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
}
