using Spectre.Console.Testing;
using TuiDo.Domain;

namespace TuiDo.Tests.Support;

/// <summary>Drives the real <see cref="TuiApp"/> with fake I/O and renders it into a Spectre <see cref="TestConsole"/>.</summary>
internal sealed class TestApp
{
    public const int DefaultWidth = 140;
    public const int DefaultHeight = 40;

    public TestApp()
    {
        App = TuiApp.Create(Repository, Clock, Clipboard, Keys, Editors, Folders);
    }

    public FakeClock Clock { get; } = new();

    public InMemoryRepository Repository { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public FakeKeySource Keys { get; } = new();

    public FakeEditorLauncher Editors { get; } = new();

    public FakeFolderOpener Folders { get; } = new();

    public TuiApp App { get; private set; }

    /// <summary>Builds the app after the repository has been seeded (the service loads data in its constructor).</summary>
    public static TestApp With(Action<TestApp> arrange)
    {
        var app = new TestApp();
        arrange(app);
        app.Restart();
        return app;
    }

    public void Restart() => App = TuiApp.Create(Repository, Clock, Clipboard, Keys, Editors, Folders);

    public DateTime Today => Clock.Now.Date;

    public TodoItem AddTask(string title, DateTime? date = null, Status status = Status.Pending, Guid? listId = null,
        bool important = false, string notes = "", int? code = null, int priority = 0)
    {
        var item = new TodoItem
        {
            Title = title,
            Date = date ?? Today,
            Status = status,
            ListId = listId,
            Important = important,
            Notes = notes,
            Code = code ?? Repository.Items.Count + 1,
            Priority = priority,
        };
        Repository.Items.Add(item);
        return item;
    }

    public CustomList AddList(string name, string icon = "\uf022")
    {
        var list = new CustomList { Name = name, Icon = icon };
        Repository.Lists.Add(list);
        return list;
    }

    // ---- input ----

    public TestApp Press(ConsoleKey key, ConsoleModifiers modifiers = 0)
    {
        var ch = key switch
        {
            ConsoleKey.Enter => '\r',
            ConsoleKey.Tab => '\t',
            ConsoleKey.Escape => '\u001b',
            ConsoleKey.Backspace => '\b',
            ConsoleKey.Spacebar => ' ',
            _ => '\0',
        };
        Redraw();
        App.Press(new ConsoleKeyInfo(ch, key, modifiers.HasFlag(ConsoleModifiers.Shift), modifiers.HasFlag(ConsoleModifiers.Alt), modifiers.HasFlag(ConsoleModifiers.Control)));
        return this;
    }

    public TestApp Press(char c)
    {
        var key = c switch
        {
            >= 'a' and <= 'z' => (ConsoleKey)(c - 'a' + (int)ConsoleKey.A),
            >= 'A' and <= 'Z' => (ConsoleKey)(c - 'A' + (int)ConsoleKey.A),
            >= '0' and <= '9' => (ConsoleKey)(c - '0' + (int)ConsoleKey.D0),
            ' ' => ConsoleKey.Spacebar,
            '/' or '?' => ConsoleKey.Oem2,
            _ => default,
        };
        Redraw();
        App.Press(new ConsoleKeyInfo(c, key, char.IsUpper(c) || c == '?', false, false));
        return this;
    }

    public TestApp Type(string text)
    {
        foreach (var c in text) Press(c);
        return this;
    }

    public TestApp Ctrl(char letter) => Ctrl(letter, (ConsoleKey)(char.ToUpperInvariant(letter) - 'A' + (int)ConsoleKey.A));

    private TestApp Ctrl(char letter, ConsoleKey key)
    {
        Redraw();
        App.Press(new ConsoleKeyInfo((char)(char.ToUpperInvariant(letter) - 'A' + 1), key, false, false, true));
        return this;
    }

    public TestApp Down(int times = 1) => Repeat(ConsoleKey.DownArrow, times);

    public TestApp Up(int times = 1) => Repeat(ConsoleKey.UpArrow, times);

    public TestApp Enter() => Press(ConsoleKey.Enter);

    public TestApp Esc() => Press(ConsoleKey.Escape);

    public TestApp Tab() => Press(ConsoleKey.Tab);

    private TestApp Repeat(ConsoleKey key, int times)
    {
        for (var i = 0; i < times; i++) Press(key);
        return this;
    }

    /// <summary>Focus the task panel (same as pressing Tab on the lists panel).</summary>
    public TestApp FocusTasks() => Tab();

    // ---- output ----

    /// <summary>The real host draws before it waits for every key; do the same so handlers see an up-to-date scroll view.</summary>
    private void Redraw() => Screen();

    public string Screen(int width = DefaultWidth, int height = DefaultHeight)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Profile.Height = height - 1; // same as ConsoleHost: the last terminal row is never drawn on
        console.Write(App.Render(console, width, height - 1));
        return console.Output;
    }
}
