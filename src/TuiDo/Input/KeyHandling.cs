using TuiDo.Abstractions;
using TuiDo.Presentation;
using TuiDo.Presentation.Views;
using TuiDo.Services;

namespace TuiDo.Input;

/// <summary>One key handler per UI mode. The first active handler (in registration order) receives the key.</summary>
internal interface IKeyHandler
{
    bool IsActive { get; }

    void Handle(ConsoleKeyInfo key);
}

internal static class KeyChords
{
    public static bool Ctrl(ConsoleKeyInfo key, ConsoleKey letter, char controlChar) =>
        key.KeyChar == controlChar || (key.Key == letter && key.Modifiers.HasFlag(ConsoleModifiers.Control));

    public static bool IsPaste(ConsoleKeyInfo key) =>
        Ctrl(key, ConsoleKey.V, (char)22) || (key.Key == ConsoleKey.Insert && key.Modifiers.HasFlag(ConsoleModifiers.Shift));

    public static bool IsEditorShortcut(ConsoleKeyInfo key) => Ctrl(key, ConsoleKey.E, (char)5);

    public static bool IsSaveShortcut(ConsoleKeyInfo key) => key.Key == ConsoleKey.F2 || key.KeyChar == (char)19;

    /// <summary>Applies backspace / printable characters to a text value. Returns false when the key is not text input.</summary>
    public static bool TryEdit(ConsoleKeyInfo key, string current, out string edited)
    {
        edited = current;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (current.Length > 0) edited = current[..^1];
            return true;
        }

        if (char.IsControl(key.KeyChar)) return false;
        edited = current + key.KeyChar;
        return true;
    }
}

/// <summary>Shortcuts available from every browsing mode (list, detail, reorder).</summary>
internal sealed class GlobalShortcuts(AppState state, IClock clock)
{
    public bool TryHandle(ConsoleKeyInfo key)
    {
        if (key.KeyChar == 'o') { state.ShowPath = true; return true; }
        if (key.Key == ConsoleKey.C && state.FocusRight && !state.Detail) { state.Calendar = clock.Today; return true; }
        if (key.KeyChar == '?') { state.HelpTab = state.Detail ? HelpContent.DetailsTab : state.FocusRight ? HelpContent.TasksTab : HelpContent.ListsTab; return true; }
        if (key.KeyChar == '/')
        {
            state.Query = "";
            state.Searching = true;
            state.Selected = 0;
            state.Detail = false;
            state.FocusRight = true;
            return true;
        }

        return false;
    }
}
