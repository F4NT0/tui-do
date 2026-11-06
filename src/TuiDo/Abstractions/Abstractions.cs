using TuiDo.Domain;

namespace TuiDo.Abstractions;

internal interface IClock
{
    DateTime Now { get; }

    DateTime Today => Now.Date;
}

internal interface IKeySource
{
    bool Available { get; }

    ConsoleKeyInfo Read();
}

internal interface IClipboard
{
    string GetText();
}

internal interface IFolderOpener
{
    void Open(string directory);
}

internal interface ITerminal
{
    int Width { get; }

    int Height { get; }

    bool CursorVisible { set; }

    void Clear();

    void Home();
}

internal sealed record EditorInfo(string Name, string Icon, string Executable);

internal interface IEditorLauncher
{
    IReadOnlyList<EditorInfo> Editors { get; }

    bool IsInstalled(EditorInfo editor);

    /// <summary>Opens <paramref name="text"/> in the editor and returns the edited text once it closes.</summary>
    string Edit(EditorInfo editor, string text, string? file = null);
}

/// <summary>Implemented by repositories that keep each task's description in its own markdown file.</summary>
internal interface IDescriptionStore
{
    string? DescriptionPathFor(TodoItem item);
}

internal interface ITodoRepository
{
    string ItemsPath { get; }

    string ListsPath { get; }

    List<TodoItem> LoadItems();

    List<CustomList> LoadLists();

    void SaveItems(IReadOnlyCollection<TodoItem> items);

    void SaveLists(IReadOnlyCollection<CustomList> lists);
}
