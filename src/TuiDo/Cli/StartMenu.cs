using Spectre.Console;
using Spectre.Console.Rendering;
using TuiDo.Infrastructure;
using TuiDo.Presentation;

namespace TuiDo.Cli;

/// <summary>Where the app should read and write its data. A null directory means the default data folder.</summary>
internal sealed record StartChoice(string? Directory, bool Markdown);

internal static class StartMenu
{
    private const int PanelWidth = 56;
    private static readonly string[] Options = ["Create new directory", "Open existing directory", "Start tuido (default data)"];

    /// <summary>Shows the main menu. Returns null to quit.</summary>
    public static StartChoice? Show(IAnsiConsole console, IKeyReader keys)
    {
        var selected = 0;
        string? message = null;
        while (true)
        {
            DrawMenu(console, selected, message);
            message = null;
            var key = keys.ReadKey();
            if (key.Key == ConsoleKey.Escape || key.KeyChar is 'q' or 'Q') return null;
            if (key.Key == ConsoleKey.UpArrow) selected = (selected + Options.Length - 1) % Options.Length;
            else if (key.Key == ConsoleKey.DownArrow) selected = (selected + 1) % Options.Length;
            else if (key.KeyChar is >= '1' and <= '3') selected = key.KeyChar - '1';
            else if (key.Key == ConsoleKey.Enter)
            {
                switch (selected)
                {
                    case 0 when Create(console, keys, out var created, out message): return created;
                    case 1 when Open(console, keys, out var opened, out message): return opened;
                    case 2: return new StartChoice(null, false);
                }
            }
        }
    }

    // ---- create ----

    private static bool Create(IAnsiConsole console, IKeyReader keys, out StartChoice? choice, out string? message)
    {
        choice = null;
        message = null;
        var name = "";
        var location = Directory.GetCurrentDirectory();
        string? error = null;
        while (true)
        {
            var full = name.Length == 0 ? location : SafeCombine(location, name);
            var available = console.Profile.Width - 8;
            var shownLocation = Fit(location, available);
            var shownFull = Fit(full, available);
            var width = Math.Max(PanelWidth, Math.Max(shownLocation.Length, shownFull.Length) + 6);
            var body = new Rows(
                new Markup($"[{Theme.Muted}]Name[/]"),
                new Markup($"[bold white]{Markup.Escape(name)}[/][{Theme.Accent}]▌[/]"),
                new Markup(" "),
                new Markup($"[{Theme.Muted}]Location[/]"),
                new Markup($"[white]{Markup.Escape(shownLocation)}[/]"),
                new Markup(" "),
                new Markup($"[{Theme.Muted}]Will be created at[/]"),
                new Markup($"[green]{Markup.Escape(shownFull)}[/]"),
                new Markup(" "),
                new Markup(error != null ? $"[red]{Markup.Escape(error)}[/]" : " "));
            DrawScreen(console, Framed(body, "Create new directory", "type: name   tab: browse location   enter: create   esc: back", width), 13);
            error = null;

            var key = keys.ReadKey();
            if (key.Key == ConsoleKey.Escape) return false;
            if (key.Key == ConsoleKey.Tab) location = Browse(console, keys, location) ?? location;
            else if (key.Key == ConsoleKey.Backspace) name = name.Length > 0 ? name[..^1] : name;
            else if (key.Key == ConsoleKey.Enter)
            {
                var trimmed = name.Trim();
                if (trimmed.Length == 0 || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { error = "Invalid directory name."; continue; }
                try
                {
                    var path = Path.GetFullPath(Path.Combine(location, trimmed));
                    if (Directory.Exists(path) && JsonTodoRepository.LooksLikeWorkspace(path)) { error = "This directory already contains tasks."; continue; }
                    new JsonTodoRepository(path, markdownDescriptions: true).Initialize();
                    choice = new StartChoice(path, true);
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    error = ex.Message;
                }
            }
            else if (!char.IsControl(key.KeyChar)) name += key.KeyChar;
        }
    }

    // ---- open ----

    private static bool Open(IAnsiConsole console, IKeyReader keys, out StartChoice? choice, out string? message)
    {
        choice = null;
        message = null;
        var path = Browse(console, keys, Directory.GetCurrentDirectory(), "Open directory");
        if (path == null) return false;
        choice = new StartChoice(path, true);
        return true;
    }

    // ---- folder browser ----

    /// <summary>Floating folder browser. Returns the selected directory, or null when cancelled.</summary>
    private static string? Browse(IAnsiConsole console, IKeyReader keys, string start, string title = "Select location")
    {
        string? current = Directory.Exists(start) ? Path.GetFullPath(start) : Directory.GetCurrentDirectory();
        var cursor = 0;
        const int visible = 12;
        while (true)
        {
            var entries = List(current);
            cursor = Math.Clamp(cursor, 0, Math.Max(0, entries.Count - 1));
            var first = Math.Clamp(cursor - visible / 2, 0, Math.Max(0, entries.Count - visible));

            var rows = new List<IRenderable> { new Markup($"[orange1]{Markup.Escape(current ?? "This PC")}[/]"), new Markup(" ") };
            if (entries.Count == 0) rows.Add(new Markup($"[{Theme.Muted}](no sub-directories)[/]"));
            for (var i = first; i < Math.Min(entries.Count, first + visible); i++)
                rows.Add(new Markup(i == cursor ? $"[orange1]>[/] [bold green]\uf07b {Markup.Escape(entries[i].Name)}[/]" : $"  [grey]\uf07b {Markup.Escape(entries[i].Name)}[/]"));
            for (var i = Math.Min(entries.Count, visible); i < visible; i++) rows.Add(new Markup(" "));

            DrawScreen(console, Framed(new Rows(rows), title, "↑/↓: move   →: enter   ←: up   enter: select   esc: cancel"), visible + 6);

            var key = keys.ReadKey();
            switch (key.Key)
            {
                case ConsoleKey.Escape: return null;
                case ConsoleKey.UpArrow: cursor = Math.Max(0, cursor - 1); break;
                case ConsoleKey.DownArrow: cursor = Math.Min(entries.Count - 1, cursor + 1); break;
                case ConsoleKey.PageUp: cursor = Math.Max(0, cursor - visible); break;
                case ConsoleKey.PageDown: cursor = Math.Min(entries.Count - 1, cursor + visible); break;
                case ConsoleKey.RightArrow when entries.Count > 0: current = entries[cursor].Path; cursor = 0; break;
                case ConsoleKey.LeftArrow or ConsoleKey.Backspace:
                    if (current == null) break;
                    var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar));
                    var previous = current;
                    current = parent;
                    cursor = Math.Max(0, List(current).FindIndex(e => string.Equals(e.Path.TrimEnd(Path.DirectorySeparatorChar), previous.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)));
                    break;
                case ConsoleKey.Enter when entries.Count > 0: return entries[cursor].Path;
                case ConsoleKey.Enter when current != null: return current;
            }
        }
    }

    private static List<(string Name, string Path)> List(string? directory)
    {
        try
        {
            if (directory == null) return DriveInfo.GetDrives().Select(d => (d.Name, d.Name)).ToList();
            return new DirectoryInfo(directory).EnumerateDirectories()
                .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden) && !d.Attributes.HasFlag(FileAttributes.System))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Select(d => (d.Name, d.FullName)).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>Shows the full path when it fits; otherwise abbreviates the home folder to ~, then truncates the middle.</summary>
    private static string Fit(string path, int available)
    {
        if (path.Length <= available) return path;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            && (path.Length == home.Length || path[home.Length] is '\\' or '/'))
            path = "~" + path[home.Length..];
        if (path.Length <= available || available < 8) return path;
        var keep = available - 1;
        return path[..(keep / 2)] + "…" + path[^(keep - keep / 2)..];
    }

    private static string SafeCombine(string location, string name)
    {
        try { return Path.GetFullPath(Path.Combine(location, name.Trim())); }
        catch (ArgumentException) { return Path.Combine(location, name); }
    }

    // ---- drawing ----

    private static IRenderable Framed(IRenderable body, string title, string hint, int width = PanelWidth) => new Rows(
        new Panel(body) { Border = BoxBorder.Rounded, Width = width, Padding = new Padding(2, 1), BorderStyle = new Style(Color.DeepSkyBlue1), Header = Theme.Header(title) },
        new Markup(" "),
        new Markup($"[grey]{Markup.Escape(hint)}[/]").Centered());

    private static void DrawMenu(IAnsiConsole console, int selected, string? message)
    {
        var options = new Rows(Options.Select((o, i) => (IRenderable)new Markup(
            i == selected ? $"[orange1]>[/] [bold green]{i + 1}. {o}[/]" : $"  [grey]{i + 1}. {o}[/]")));
        var content = new Rows(
            new Rows(Logo.Lines.Select(l => (IRenderable)new Markup($"[deepskyblue1]{l}[/]").Centered())),
            new Markup(" "),
            new Markup($"[{Theme.Accent}]Terminal-native to-do manager[/]").Centered(),
            new Markup(" "),
            new Markup($"[yellow]V{AppInfo.Version}[/]").Centered(),
            new Markup(" "),
            Align.Center(new Panel(options) { Border = BoxBorder.Rounded, Width = PanelWidth, Padding = new Padding(4, 1), BorderStyle = new Style(Color.Grey) }),
            new Markup(message != null ? $"[red]{Markup.Escape(message)}[/]" : " ").Centered(),
            new Markup("[grey]↑/↓: select   enter: confirm   q / esc: quit[/]").Centered());
        DrawScreen(console, content, Logo.Lines.Length + 15);
    }

    private static void DrawScreen(IAnsiConsole console, IRenderable content, int height)
    {
        try { console.Clear(); } catch { }
        for (var i = 0; i < Math.Max(0, (console.Profile.Height - height) / 2); i++) console.WriteLine();
        console.Write(Align.Center(content));
    }

    private static Align Centered(this Markup markup) => Align.Center(markup);
}
