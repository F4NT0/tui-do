using Spectre.Console;
using TuiDo.Abstractions;
using TuiDo.Infrastructure;
using TuiDo.Presentation;
using TuiDo.Presentation.Views;

namespace TuiDo.Cli;

internal sealed class CliApp(IAnsiConsole console, IReleaseChecker releases, Installer installer, IKeyReader keys,
    ITodoRepository? repository = null, IClock? clock = null)
{
    public static bool IsCliCommand(string[] args) =>
        args.Length > 0 && args[0].ToLowerInvariant() is "--version" or "-v" or "--help" or "-h" or "--install" or "--uninstall" or "--today";

    public async Task<int> RunAsync(string[] args)
    {
        switch (args[0].ToLowerInvariant())
        {
            case "--version" or "-v": return await VersionAsync();
            case "--help" or "-h": Help(); return 0;
            case "--today":
                return TodayCommand.Run(console, repository ?? new JsonTodoRepository(JsonTodoRepository.DefaultDirectory), clock ?? new SystemClock());
            case "--install": return installer.Install();
            case "--uninstall": return installer.Uninstall();
            default: return 1;
        }
    }

    private async Task<int> VersionAsync()
    {
        console.MarkupLine($"[bold {Theme.Accent}]tuido[/] [bold white]v{AppInfo.Version}[/]");

        ReleaseInfo? latest = null;
        string? error = null;
        await console.Status().Spinner(Spinner.Known.Dots).StartAsync("[grey]Checking for a new release...[/]", async _ =>
        {
            try { latest = await releases.GetLatestAsync(); }
            catch (Exception ex) { error = ex.Message; }
        });

        if (error != null) console.MarkupLine($"[yellow]\uea6c Could not check for updates:[/] [grey]{Markup.Escape(error)}[/]");
        else if (latest is null) console.MarkupLine("[grey]No release has been published yet.[/]");
        else if (VersionComparer.IsDifferent(latest.Tag, AppInfo.Version))
            console.MarkupLine($"[orange1]\uea6c New release available:[/] [bold]{Markup.Escape(latest.Tag)}[/] [grey]({Markup.Escape(latest.Url)})[/]");
        else console.MarkupLine("[green]\uf05d You are on the latest release.[/]");
        return 0;
    }

    private void Help() => HelpScreen.Show(console, keys);
}
