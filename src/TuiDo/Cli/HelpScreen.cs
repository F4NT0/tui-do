using Spectre.Console;
using TuiDo.Presentation;
using TuiDo.Presentation.Views;

namespace TuiDo.Cli;

internal interface IKeyReader
{
    ConsoleKeyInfo ReadKey();
}

internal sealed class ConsoleKeyReader : IKeyReader
{
    public ConsoleKeyInfo ReadKey()
    {
        try { return Console.ReadKey(true); }
        catch (InvalidOperationException) { return new('\0', ConsoleKey.Escape, false, false, false); }
    }
}

internal static class HelpScreen
{
    private static readonly string[] Tabs = ["Commands", .. HelpContent.Tabs];

    private static readonly (string Command, string Description)[] CliCommands =
    [
        ("tuido", "Open the app"),
        ("tuido --version", "Show the version and check for a new release"),
        ("tuido --today", "Show today's tasks in a table"),
        ("tuido --help", "Show this help"),
        ("tuido --install", "Install tuido and add it to your PATH"),
        ("tuido --uninstall", "Remove tuido, its PATH entry and all its data"),
    ];

    public static void Show(IAnsiConsole console, IKeyReader keys)
    {
        var tab = 0;
        Draw(console, tab);

        while (true)
        {
            var key = keys.ReadKey();
            var next = tab;
            if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.H or ConsoleKey.UpArrow) next = Math.Max(0, tab - 1);
            else if (key.Key is ConsoleKey.RightArrow or ConsoleKey.L or ConsoleKey.DownArrow or ConsoleKey.Tab) next = Math.Min(Tabs.Length - 1, tab + 1);
            else if (key.KeyChar >= '1' && key.KeyChar < '1' + Tabs.Length) next = key.KeyChar - '1';
            else if (key.Key == ConsoleKey.Escape || key.KeyChar is '?' or 'q' || key.Key == ConsoleKey.Enter) break;

            if (next == tab) continue;
            tab = next;
            try { console.Clear(); } catch { break; }
            Draw(console, tab);
        }
    }

    private static void Draw(IAnsiConsole console, int tab)
    {
        console.MarkupLine($"[bold {Theme.Accent}]tuido[/] [grey]v{AppInfo.Version} - terminal to-do lists[/]");
        console.WriteLine();
        console.MarkupLine("  " + string.Join("   ", Tabs.Select((t, i) =>
            i == tab ? $"[bold {Theme.Accent} underline]{i + 1} {Markup.Escape(t)}[/]" : $"[grey]{i + 1} {Markup.Escape(t)}[/]")));
        console.WriteLine();

        if (tab == 0)
        {
            var table = new Table().HideHeaders().NoBorder().AddColumn(new TableColumn("").NoWrap()).AddColumn("");
            foreach (var (cmd, text) in CliCommands)
                table.AddRow($"[{Theme.Accent}]{cmd}[/]", $"[grey]{text}[/]");
            console.Write(new Rule($"[bold {Theme.Orange}]CLI commands[/]").LeftJustified().RuleStyle(Style.Parse(Theme.Muted)));
            console.Write(table);
        }
        else
            foreach (var section in HelpContent.Page(tab - 1))
            {
                console.Write(new Rule($"[bold {Theme.Orange}]{Markup.Escape(section.Title)}[/]").LeftJustified().RuleStyle(Style.Parse(Theme.Muted)));
                var table = new Table().HideHeaders().NoBorder().AddColumn(new TableColumn("").NoWrap()).AddColumn("");
                foreach (var e in section.Entries)
                    table.AddRow($"[{Theme.Orange}]{Markup.Escape(e.Key)}[/]", $"[grey]{Markup.Escape(e.Description)}[/]");
                console.Write(table);
            }

        console.WriteLine();
        console.MarkupLine($"[grey]←→ / 1-{Tabs.Length}: switch tab   Esc: exit[/]");
    }
}
