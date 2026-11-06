using System.Text;
using Spectre.Console;
using TuiDo.Installer;

Console.OutputEncoding = Encoding.UTF8;

if (!OperatingSystem.IsWindows())
{
    AnsiConsole.MarkupLine("[red]This installer only supports Windows 11.[/]");
    return 1;
}

AnsiConsole.WriteLine();
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

var dotnetInstalled = Steps.IsDotNet10Installed();
var fontInstalled = Steps.IsNerdFontInstalled();
var appInstalled = File.Exists(Path.Combine(Steps.InstallDir, "tuido.exe"));
var pathConfigured = Steps.IsInPath();
string Status(bool done, string yes = "already installed") => done ? $"[green]{yes}[/]" : "[yellow]will be installed[/]";

AnsiConsole.MarkupLine("[bold]The installer will perform these steps:[/]\n");
var plan = new Table().Border(TableBorder.Rounded).AddColumn("Step").AddColumn("Status");
plan.AddRow("1. .NET 10 runtime", Status(dotnetInstalled));
plan.AddRow("2. JetBrainsMono Nerd Font", Status(fontInstalled));
plan.AddRow("3. Download latest tuido release", "[yellow]will be downloaded[/]");
plan.AddRow("4. Install tuido", $"{(appInstalled ? "[yellow]will be updated[/]" : "[yellow]will be installed[/]")} [grey]({Markup.Escape(Steps.InstallDir)})[/]");
plan.AddRow("5. Configure PATH", Status(pathConfigured, "already configured"));
plan.AddRow("6. Verify installation", "[yellow]pending[/]");
AnsiConsole.Write(plan);
AnsiConsole.WriteLine();

if (!AnsiConsole.Confirm("Do you want to proceed with the installation?"))
{
    AnsiConsole.MarkupLine("\n[grey]Installation cancelled.[/]");
    return 0;
}
AnsiConsole.WriteLine();

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
if (success)
{
    AnsiConsole.MarkupLine($"\n[bold green]tuido {Markup.Escape(tag)} was installed.[/]\n");
    AnsiConsole.MarkupLine($"[bold]Installed at:[/]  {Markup.Escape(Steps.InstallDir)}");
    AnsiConsole.MarkupLine($"[bold]Added to PATH:[/] {Markup.Escape(Steps.InstallDir)} [grey](command:[/] [bold deepskyblue1]tuido[/][grey])[/]");
    AnsiConsole.MarkupLine("\n[grey]Open a new terminal, select the JetBrainsMono Nerd Font and try:[/]\n");
    AnsiConsole.MarkupLine("  [bold deepskyblue1]tuido[/]              [grey]open the app[/]");
    AnsiConsole.MarkupLine("  [bold deepskyblue1]tuido --help[/]       [grey]list all commands[/]");
    AnsiConsole.MarkupLine("  [bold deepskyblue1]tuido --version[/]    [grey]show version and check for updates[/]");
    AnsiConsole.MarkupLine("  [bold deepskyblue1]tuido --today[/]      [grey]show today's tasks[/]");
    AnsiConsole.MarkupLine("  [bold deepskyblue1]tuido --uninstall[/]  [grey]remove tuido[/]");
}
else AnsiConsole.MarkupLine("\n[bold red]Installation failed.[/]");
return success ? 0 : 1;
