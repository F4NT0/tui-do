using TuiDo.Abstractions;
using TuiDo.Domain;
using TuiDo.Presentation;
using TuiDo.Services;

namespace TuiDo.Input;

internal sealed class StatusPickerKeyHandler(AppState state, TodoService service) : IKeyHandler
{
    public bool IsActive => state.StatusPick != null;

    public void Handle(ConsoleKeyInfo key)
    {
        var last = StatusCatalog.Ordered.Count - 1;
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: state.StatusPick = Math.Max(0, state.StatusPick!.Value - 1); break;
            case ConsoleKey.DownArrow: state.StatusPick = Math.Min(last, state.StatusPick!.Value + 1); break;
            case ConsoleKey.Escape: state.StatusPick = null; break;
            case ConsoleKey.Enter: Confirm(StatusCatalog.Ordered[state.StatusPick!.Value]); break;
        }
    }

    private void Confirm(Status status)
    {
        state.StatusPick = null;
        var target = state.Target!;
        if (status != Status.Blocked)
        {
            service.SetStatus(target, status);
            return;
        }

        state.BlockerCandidates = service.BlockerCandidates(target);
        if (state.BlockerCandidates.Count > 0) state.BlockPick = 0;
    }
}

internal sealed class BlockedByPickerKeyHandler(AppState state, TodoService service) : IKeyHandler
{
    public bool IsActive => state.BlockPick != null;

    public void Handle(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: state.BlockPick = Math.Max(0, state.BlockPick!.Value - 1); break;
            case ConsoleKey.DownArrow: state.BlockPick = Math.Min(state.BlockerCandidates.Count - 1, state.BlockPick!.Value + 1); break;
            case ConsoleKey.Escape: state.BlockPick = null; break;
            case ConsoleKey.Enter:
                service.Block(state.Target!, state.BlockerCandidates[state.BlockPick!.Value]);
                state.BlockPick = null;
                break;
        }
    }
}

internal sealed class MovePickerKeyHandler(AppState state, TodoService service) : IKeyHandler
{
    public bool IsActive => state.MovePick != null;

    public void Handle(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: state.MovePick = Math.Max(0, state.MovePick!.Value - 1); break;
            case ConsoleKey.DownArrow: state.MovePick = Math.Min(service.Lists.Count, state.MovePick!.Value + 1); break;
            case ConsoleKey.Escape: state.MovePick = null; break;
            case ConsoleKey.Enter:
                var choice = state.MovePick!.Value;
                service.MoveToList(state.Target!, choice == 0 ? null : service.Lists[choice - 1].Id);
                state.MovePick = null;
                state.Detail = false;
                break;
        }
    }
}

internal sealed class EditorPickerKeyHandler(AppState state, IEditorLauncher launcher, NotificationLog log) : IKeyHandler
{
    public bool IsActive => state.EditorPick != null;

    public void Handle(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: state.EditorPick = Math.Max(0, state.EditorPick!.Value - 1); break;
            case ConsoleKey.DownArrow: state.EditorPick = Math.Min(launcher.Editors.Count - 1, state.EditorPick!.Value + 1); break;
            case ConsoleKey.Escape: state.EditorPick = null; break;
            case ConsoleKey.Enter: Open(launcher.Editors[state.EditorPick!.Value]); break;
        }
    }

    private void Open(EditorInfo editor)
    {
        state.EditorPick = null;
        if (!launcher.IsInstalled(editor))
        {
            log.Warning($"{editor.Name} is not installed on this computer");
            return;
        }

        try
        {
            var form = state.TaskForm!;
            form.Notes = launcher.Edit(editor, form.Notes);
            log.Success($"About updated from {editor.Name}");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log.Error($"Could not run {editor.Name}: {e.Message}");
        }
    }
}
