using Spectre.Console;

namespace TuiDo.Cli;

/// <summary>"TUI-DO" in the ANSI Shadow figlet font.</summary>
internal static class Logo
{
    public static readonly string[] Lines =
    [
        "████████╗██╗   ██╗██╗      ██████╗  ██████╗ ",
        "╚══██╔══╝██║   ██║██║      ██╔══██╗██╔═══██╗",
        "   ██║   ██║   ██║██║█████╗██║  ██║██║   ██║",
        "   ██║   ██║   ██║██║╚════╝██║  ██║██║   ██║",
        "   ██║   ╚██████╔╝██║      ██████╔╝╚██████╔╝",
        "   ╚═╝    ╚═════╝ ╚═╝      ╚═════╝  ╚═════╝ ",
    ];

    public static void Write(IAnsiConsole console)
    {
        foreach (var line in Lines) console.MarkupLine($"[deepskyblue1]{line}[/]");
        console.WriteLine();
    }
}
