#:package Spectre.Console@0.57.2
#:property PublishAot=false

using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Spectre.Console;

var root = FindRoot();
var trx = Path.Combine(Path.GetTempPath(), $"tuido-{Guid.NewGuid():N}.trx");
var extra = string.Join(' ', args.Select(a => $"\"{a}\""));

var exit = 0;
var wall = Stopwatch.StartNew();
AnsiConsole.Status().Start("Running tests...", _ =>
{
    var psi = new ProcessStartInfo("dotnet",
        $"test TuiDo.slnx -c Release --logger \"trx;LogFileName={trx}\" {extra}")
    {
        WorkingDirectory = root,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    using var p = Process.Start(psi)!;
    p.OutputDataReceived += (_, _) => { };
    p.ErrorDataReceived += (_, _) => { };
    p.BeginOutputReadLine();
    p.BeginErrorReadLine();
    p.WaitForExit();
    exit = p.ExitCode;
});

if (!File.Exists(trx))
{
    AnsiConsole.MarkupLine("[red]No test results were produced. Build probably failed; run `dotnet build` to see why.[/]");
    return 1;
}

XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
var results = XDocument.Load(trx).Descendants(ns + "UnitTestResult")
    .Select(r => (
        Name: (string)r.Attribute("testName")!,
        Outcome: (string)r.Attribute("outcome")!,
        Time: TimeSpan.Parse((string?)r.Attribute("duration") ?? "0", CultureInfo.InvariantCulture),
        Error: (string?)r.Descendants(ns + "Message").FirstOrDefault()))
    .OrderBy(r => r.Name, StringComparer.Ordinal)
    .ToList();
File.Delete(trx);

const string Completed = "\uf05d", Cancelled = "\uf52f", Warning = "\uea6c";

var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .AddColumn(new TableColumn("").Centered())
    .AddColumn("[bold]Test[/]")
    .AddColumn(new TableColumn("[bold]Time[/]").RightAligned());

foreach (var r in results)
{
    var (icon, color) = r.Outcome switch
    {
        "Passed" => (Completed, "green"),
        "Failed" => (Cancelled, "red"),
        _ => (Warning, "yellow")
    };
    table.AddRow($"[{color}]{icon}[/]", $"[{color}]{Markup.Escape(r.Name)}[/]", $"[grey]{Format(r.Time)}[/]");
}
AnsiConsole.Write(table);

foreach (var f in results.Where(r => r.Outcome == "Failed"))
    AnsiConsole.MarkupLine($"\n[red]{Cancelled} {Markup.Escape(f.Name)}[/]\n[grey]{Markup.Escape(f.Error ?? "")}[/]");

var passed = results.Count(r => r.Outcome == "Passed");
var failed = results.Count(r => r.Outcome == "Failed");
var other = results.Count - passed - failed;
AnsiConsole.MarkupLine(
    $"\n[green]{Completed} {passed} passed[/]  [red]{Cancelled} {failed} failed[/]  [yellow]{Warning} {other} skipped[/]  " +
    $"[grey]total {results.Count} in {Format(wall.Elapsed)}[/]");

return failed > 0 || results.Count == 0 || exit != 0 ? 1 : 0;

static string Format(TimeSpan t) => t.TotalSeconds >= 1 ? $"{t.TotalSeconds:F2} s" : $"{t.TotalMilliseconds:F0} ms";

static string FindRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TuiDo.slnx"))) dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}
