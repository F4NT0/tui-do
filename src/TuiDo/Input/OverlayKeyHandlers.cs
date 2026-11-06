using TuiDo.Abstractions;
using TuiDo.Presentation;
using TuiDo.Presentation.Views;
using TuiDo.Services;

namespace TuiDo.Input;

internal sealed class SearchKeyHandler(AppState state) : IKeyHandler
{
    public bool IsActive => state.Searching;

    public void Handle(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape: state.Searching = false; state.Query = null; state.FocusRight = false; state.Selected = 0; break;
            case ConsoleKey.Enter: state.Searching = false; break;
            default:
                if (KeyChords.TryEdit(key, state.Query ?? "", out var edited))
                {
                    state.Query = edited;
                    state.Selected = 0;
                }

                break;
        }
    }
}

internal sealed class PriorityKeyHandler(AppState state, BrowseModel browse, TodoService service) : IKeyHandler
{
    private const int MaxDigits = 4;

    public bool IsActive => state.PriorityInput != null;

    public void Handle(ConsoleKeyInfo key)
    {
        var input = state.PriorityInput!;
        switch (key.Key)
        {
            case ConsoleKey.Escape: state.PriorityInput = null; break;
            case ConsoleKey.Backspace: state.PriorityInput = input.Length > 0 ? input[..^1] : input; break;
            case ConsoleKey.Enter:
                var list = browse.Visible();
                if (int.TryParse(input, out var position) && list.Count > 0)
                    state.Selected = service.Reorder(list, state.Selected, position - 1);
                state.PriorityInput = null;
                break;
            default:
                if (char.IsDigit(key.KeyChar) && input.Length < MaxDigits) state.PriorityInput = input + key.KeyChar;
                break;
        }
    }
}

internal sealed class HelpKeyHandler(AppState state) : IKeyHandler
{
    public bool IsActive => state.HelpTab != null;

    public void Handle(ConsoleKeyInfo key)
    {
        var last = HelpContent.Tabs.Count - 1;
        switch (key.Key)
        {
            case ConsoleKey.Escape or ConsoleKey.Q: state.HelpTab = null; break;
            case ConsoleKey.LeftArrow or ConsoleKey.H or ConsoleKey.UpArrow: state.HelpTab = Math.Max(0, state.HelpTab!.Value - 1); break;
            case ConsoleKey.RightArrow or ConsoleKey.L or ConsoleKey.DownArrow or ConsoleKey.Tab: state.HelpTab = Math.Min(last, state.HelpTab!.Value + 1); break;
            default:
                if (key.KeyChar == '?') state.HelpTab = null;
                else if (key.KeyChar >= '1' && key.KeyChar <= '1' + last) state.HelpTab = key.KeyChar - '1';
                break;
        }
    }
}

internal sealed class CalendarKeyHandler(AppState state, IClock clock) : IKeyHandler
{
    public bool IsActive => state.Calendar != null;

    public void Handle(ConsoleKeyInfo key)
    {
        var day = state.Calendar!.Value;
        if (key.Key == ConsoleKey.Enter) { state.CalendarDay = true; return; }
        if (key.Key is ConsoleKey.Escape or ConsoleKey.C && state.CalendarDay) { state.CalendarDay = false; return; }
        state.Calendar = key.Key switch
        {
            ConsoleKey.LeftArrow or ConsoleKey.H => day.AddDays(-1),
            ConsoleKey.RightArrow or ConsoleKey.L => day.AddDays(1),
            ConsoleKey.UpArrow or ConsoleKey.K => day.AddDays(-7),
            ConsoleKey.DownArrow or ConsoleKey.J => day.AddDays(7),
            ConsoleKey.PageUp => day.AddMonths(-1),
            ConsoleKey.PageDown => day.AddMonths(1),
            ConsoleKey.T => clock.Today,
            ConsoleKey.Escape or ConsoleKey.C => null,
            _ => day,
        };
    }
}

internal sealed class DirectoryPickerKeyHandler(AppState state, TodoService service, Func<string, ITodoRepository> factory, NotificationLog log) : IKeyHandler
{
    public bool IsActive => state.DirPick != null;

    public void Handle(ConsoleKeyInfo key)
    {
        var browser = state.DirPick!;
        switch (key.Key)
        {
            case ConsoleKey.Escape: state.DirPick = null; break;
            case ConsoleKey.UpArrow: browser.Move(-1); break;
            case ConsoleKey.DownArrow: browser.Move(1); break;
            case ConsoleKey.PageUp: browser.Move(-DirectoryBrowser.Visible); break;
            case ConsoleKey.PageDown: browser.Move(DirectoryBrowser.Visible); break;
            case ConsoleKey.RightArrow: browser.Enter(); break;
            case ConsoleKey.LeftArrow or ConsoleKey.Backspace: browser.Up(); break;
            case ConsoleKey.Enter when browser.Selected is { } selected: Open(selected); break;
        }
    }

    private void Open(string path)
    {
        service.Switch(factory(path));
        state.DirPick = null;
        state.Menu = 0;
        state.Selected = 0;
        state.Query = null;
        state.FocusRight = false;
        state.Detail = false;
        log.Success($"Opened {Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))}");
    }
}

internal sealed class DataLocationKeyHandler(AppState state, TodoService service, IFolderOpener opener, NotificationLog log) : IKeyHandler
{
    public bool IsActive => state.ShowPath;

    public void Handle(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Enter)
        {
            try
            {
                opener.Open(Path.GetDirectoryName(service.ItemsPath)!);
                log.Success("Opened data folder");
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
            {
                log.Error($"Could not open folder: {e.Message}");
            }

            state.ShowPath = false;
        }
        else if (key.Key == ConsoleKey.Escape || key.KeyChar == 'o') state.ShowPath = false;
    }
}
