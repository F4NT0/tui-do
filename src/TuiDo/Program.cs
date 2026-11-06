using System.Globalization;
using System.Text;
using Spectre.Console;
using TuiDo;
using TuiDo.Cli;
using TuiDo.Infrastructure;

// The UI is in English with fixed dd/MM/yyyy dates, so formatting must not depend on the machine culture.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

if (CliApp.IsCliCommand(args))
    return await new CliApp(AnsiConsole.Console, new GitHubReleaseChecker(), Installer.CreateDefault(AnsiConsole.Console), new ConsoleKeyReader()).RunAsync(args);

var start = new StartChoice(null, false);
if (!Console.IsInputRedirected)
{
    var choice = StartMenu.Show(AnsiConsole.Console, new ConsoleKeyReader());
    if (choice is null)
    {
        try { AnsiConsole.Clear(); } catch { }
        return 0;
    }
    start = choice;
}

var terminal = new ConsoleTerminal();
var keys = new ConsoleKeySource();
var clock = new SystemClock();

var app = TuiApp.Create(
    new JsonTodoRepository(start.Directory ?? JsonTodoRepository.DefaultDirectory, start.Markdown),
    clock,
    new PowerShellClipboard(),
    keys,
    new ExternalEditorLauncher(terminal, AnsiConsole.Console),
    new ExplorerFolderOpener(),
    path => new JsonTodoRepository(path, markdownDescriptions: true));

new ConsoleHost(app, keys, terminal, AnsiConsole.Console, clock).Run();
return 0;
