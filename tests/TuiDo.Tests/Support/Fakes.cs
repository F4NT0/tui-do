using TuiDo.Abstractions;
using TuiDo.Domain;

namespace TuiDo.Tests.Support;

internal sealed class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 11, 3, 18, 51, 45);

    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class FakeKeySource : IKeySource
{
    private readonly Queue<ConsoleKeyInfo> _queue = new();

    public bool Available => _queue.Count > 0;

    public ConsoleKeyInfo Read() => _queue.Dequeue();

    public void Push(ConsoleKeyInfo key) => _queue.Enqueue(key);
}

internal sealed class FakeClipboard : IClipboard
{
    public string Text { get; set; } = "";

    public string GetText() => Text;
}

internal sealed class FakeFolderOpener : IFolderOpener
{
    public List<string> Opened { get; } = [];

    public Exception? Failure { get; set; }

    public void Open(string directory)
    {
        if (Failure != null) throw Failure;
        Opened.Add(directory);
    }
}

internal sealed class FakeEditorLauncher : IEditorLauncher
{
    public IReadOnlyList<EditorInfo> Editors { get; } =
    [
        new("Neovim", "\uf36f", "nvim"),
        new("Vim", "\ue7c5", "vim"),
        new("VS Code", "\ue8da", "code"),
    ];

    public HashSet<string> Installed { get; } = ["nvim", "vim", "code"];

    public string? EditedText { get; set; }

    public string? ReceivedText { get; private set; }

    public EditorInfo? LaunchedWith { get; private set; }

    public bool IsInstalled(EditorInfo editor) => Installed.Contains(editor.Executable);

    public string Edit(EditorInfo editor, string text)
    {
        LaunchedWith = editor;
        ReceivedText = text;
        return EditedText ?? text;
    }
}

internal sealed class InMemoryRepository : ITodoRepository
{
    public static readonly string Directory = Path.Combine("data", "tui-do");

    public string ItemsPath => Path.Combine(Directory, "tasks.json");

    public string ListsPath => Path.Combine(Directory, "lists.json");

    public List<TodoItem> Items { get; } = [];

    public List<CustomList> Lists { get; } = [];

    public bool FailOnSave { get; set; }

    public int ItemSaves { get; private set; }

    public int ListSaves { get; private set; }

    public List<TodoItem> LoadItems() => [.. Items];

    public List<CustomList> LoadLists() => [.. Lists];

    public void SaveItems(IReadOnlyCollection<TodoItem> items)
    {
        if (FailOnSave) throw new IOException("disk is full");
        ItemSaves++;
        Items.Clear();
        Items.AddRange(items);
    }

    public void SaveLists(IReadOnlyCollection<CustomList> lists)
    {
        if (FailOnSave) throw new IOException("disk is full");
        ListSaves++;
        Lists.Clear();
        Lists.AddRange(lists);
    }
}
