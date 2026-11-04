using Spectre.Console;
using TuiDo.Presentation;

namespace TuiDo.Cli;

internal static class SplashScreen
{
    private const string Subtitle = "Terminal-native to-do manager";
    private const string Description = "Organize tasks, lists and priorities with markdown notes in a two-panel TUI";

    /// <summary>Shows the start screen. Returns true to start the app, false to quit.</summary>
    public static bool Show(IAnsiConsole console, IKeyReader keys)
    {
        try { console.Clear(); } catch { }

        const int contentHeight = 15;
        for (var i = 0; i < Math.Max(0, (console.Profile.Height - contentHeight) / 2); i++) console.WriteLine();

        foreach (var line in Logo.Lines) Center(console, $"[deepskyblue1]{line}[/]");
        console.WriteLine();
        Center(console, $"[{Theme.Accent}]{Subtitle}[/]");
        Center(console, $"[grey]{Description}[/]");
        console.WriteLine();
        Center(console, $"[yellow]V{AppInfo.Version}[/]");
        console.WriteLine();
        Center(console, $"[{Theme.Muted}]{new string('─', 48)}[/]");
        console.WriteLine();
        Center(console, $"[orange1]>[/]  [bold green]Start tuido[/]");
        console.WriteLine();
        Center(console, "[grey]enter: start   q / esc: quit[/]");

        while (true)
        {
            var key = keys.ReadKey();
            if (key.Key == ConsoleKey.Enter) return true;
            if (key.Key == ConsoleKey.Escape || key.KeyChar is 'q' or 'Q') return false;
        }
    }

    private static void Center(IAnsiConsole console, string markup) => console.Write(Align.Center(new Markup(markup)));
}
