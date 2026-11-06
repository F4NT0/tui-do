using System.Text;
using Spectre.Console;
using TuiDo.Installer;

Console.OutputEncoding = Encoding.UTF8;

if (!OperatingSystem.IsWindows())
{
    AnsiConsole.MarkupLine("[red]This installer only supports Windows 11.[/]");
    return 1;
}

foreach (var line in new[]
{
    "████████╗██╗   ██╗██╗      ██████╗  ██████╗ ",
    "╚══██╔══╝██║   ██║██║      ██╔══██╗██╔═══██╗",
    "   ██║   ██║   ██║██║█████╗██║  ██║██║   ██║",
    "   ██║   ██║   ██║██║╚════╝██║  ██║██║   ██║",
    "   ██║   ╚██████╔╝██║      ██████╔╝╚██████╔╝",
    "   ╚═╝    ╚═════╝ ╚═╝      ╚═════╝  ╚═════╝ ",
}) AnsiConsole.MarkupLine($"[deepskyblue1]{line}[/]");
AnsiConsole.MarkupLine("\n[bold deepskyblue1]tuido installer[/] [grey]- installs .NET 10, JetBrainsMono Nerd Font and the latest tuido release[/]\n");

var temp = Path.Combine(Path.GetTempPath(), "tuido-installer-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(temp);
var results = new List<(string Step, bool Ok, string Detail)>();
var tag = "";

await AnsiConsole.Progress()
    .AutoClear(false)
    .Columns(new TaskDescriptionColumn { Alignment = Justify.Left }, new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
    .StartAsync(async ctx =>
    {
        var dotnet = ctx.AddTask("[grey]1. .NET 10 runtime[/]", maxValue: 100);
        var font = ctx.AddTask("[grey]2. JetBrainsMono Nerd Font[/]", maxValue: 100);
        var download = ctx.AddTask("[grey]3. Download latest tuido release[/]", maxValue: 100);
        var install = ctx.AddTask("[grey]4. Install tuido[/]", maxValue: 100);
        var path = ctx.AddTask("[grey]5. Configure PATH[/]", maxValue: 100);
        var verify = ctx.AddTask("[grey]6. Verify installation[/]", maxValue: 100);

        foreach (var t in new[] { font, download, install, path, verify }) t.IsIndeterminate = false;

        async Task<bool> Run(ProgressTask task, string title, Func<Task<string>> action)
        {
            task.Description = $"[deepskyblue1]{title}[/]";
            try
            {
                var detail = await action();
                task.Value = 100;
                task.Description = $"[green]{title}[/]";
                results.Add((title, true, detail));
                return true;
            }
            catch (Exception ex)
            {
                task.Description = $"[red]{title}[/]";
                task.StopTask();
                results.Add((title, false, ex.Message));
                return false;
            }
        }

        string? downloaded = null;
        _ = await Run(dotnet, "1. .NET 10 runtime", () => Steps.InstallDotNetAsync(temp, p => dotnet.Value = p, i => dotnet.IsIndeterminate = i))
            && await Run(font, "2. JetBrainsMono Nerd Font", () => Steps.InstallFontAsync(temp, p => font.Value = p, d => font.Description = $"[deepskyblue1]2. {d}[/]"))
            && await Run(download, "3. Download latest tuido release", async () =>
            {
                (downloaded, tag) = await Steps.DownloadAppAsync(temp, p => download.Value = p);
                return $"release {tag}";
            })
            && await Run(install, "4. Install tuido", () => Task.FromResult(Steps.InstallApp(downloaded!, p => install.Value = p)))
            && await Run(path, "5. Configure PATH", () => Task.FromResult(Steps.AddToPath()))
            && await Run(verify, "6. Verify installation", () => Task.FromResult(Steps.Verify()));
    });

try { Directory.Delete(temp, true); } catch { }

AnsiConsole.WriteLine();
var table = new Table().Border(TableBorder.Rounded).AddColumn("Step").AddColumn("Result");
foreach (var (step, ok, detail) in results)
    table.AddRow(Markup.Escape(step), ok ? $"[green]{Markup.Escape(detail)}[/]" : $"[red]{Markup.Escape(detail)}[/]");
AnsiConsole.Write(table);

var success = results.Count == 6 && results.All(r => r.Ok);
AnsiConsole.MarkupLine(success
    ? $"\n[bold green]tuido {Markup.Escape(tag)} was installed.[/] [grey]Open a new terminal, select the JetBrainsMono Nerd Font and run[/] [bold deepskyblue1]tuido[/]"
    : "\n[bold red]Installation failed.[/]");
return success ? 0 : 1;
