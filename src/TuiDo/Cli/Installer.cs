using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console;
using TuiDo.Infrastructure;
using TuiDo.Presentation;

namespace TuiDo.Cli;

internal sealed class Installer(IAnsiConsole console, string installDir, string dataDir, string sourceDir)
{
    private const string Done = "[green]\uf05d[/]", Failed = "[red]\uf52f[/]", Warn = "[yellow]\uea6c[/]";

    public static Installer CreateDefault(IAnsiConsole console) =>
        new(console, AppInfo.DefaultInstallDir, JsonTodoRepository.DefaultDirectory, AppContext.BaseDirectory);

    private string InstalledExe => Path.Combine(installDir, AppInfo.ExeName);

    public int Install()
    {
        if (!OperatingSystem.IsWindows()) return Unsupported();
        Banner("Installer", "Copies tuido to your user profile and adds it to the user PATH");
        console.MarkupLine($"  [grey]Install directory:[/] [bold]{Markup.Escape(installDir)}[/]\n");
        if (!console.Confirm("Install tuido?")) { console.MarkupLine("[grey]Cancelled.[/]"); return 1; }
        console.WriteLine();

        var ok = Step("Locating program files", () =>
        {
            var exe = Path.Combine(sourceDir, AppInfo.ExeName);
            return File.Exists(exe) ? exe : throw new FileNotFoundException($"{AppInfo.ExeName} not found in {sourceDir}. Run a published build.");
        })
        && Step("Creating install directory", () => { Directory.CreateDirectory(installDir); return installDir; })
        && Step("Copying files", CopyFiles)
        && Step("Adding to user PATH", () => AddToPath() ? "PATH updated" : "already in PATH")
        && Step("Verifying installation", () => File.Exists(InstalledExe) ? InstalledExe : throw new FileNotFoundException(InstalledExe));

        console.WriteLine();
        if (ok) console.MarkupLine($"{Done} [bold green]tuido was installed.[/] [grey]Open a new terminal and run[/] [bold {Theme.Accent}]tuido[/]");
        else console.MarkupLine($"{Failed} [bold red]Installation failed.[/]");
        return ok ? 0 : 1;
    }

    public int Uninstall()
    {
        if (!OperatingSystem.IsWindows()) return Unsupported();
        Banner("Uninstaller", "Removes tuido, its PATH entry and all of its data");
        console.MarkupLine($"  [grey]Program:[/] [bold]{Markup.Escape(installDir)}[/]");
        console.MarkupLine($"  [grey]Data   :[/] [bold]{Markup.Escape(dataDir)}[/] [yellow](your tasks and lists will be deleted)[/]\n");
        if (!console.Confirm("Uninstall tuido and delete all of its data?", false)) { console.MarkupLine("[grey]Cancelled.[/]"); return 1; }
        console.WriteLine();

        var ok = Step("Locating installation", () => Directory.Exists(installDir) ? installDir : "not found, nothing to remove")
            & Step("Removing from user PATH", () => RemoveFromPath() ? "PATH updated" : "was not in PATH")
            & Step("Deleting program files", DeleteProgram)
            & Step("Deleting data and settings", () =>
            {
                if (!Directory.Exists(dataDir)) return "no data found";
                Directory.Delete(dataDir, true);
                return dataDir;
            });

        console.WriteLine();
        if (ok) console.MarkupLine($"{Done} [bold green]tuido was uninstalled.[/]");
        else console.MarkupLine($"{Failed} [bold red]Uninstall finished with errors.[/]");
        return ok ? 0 : 1;
    }

    private string CopyFiles()
    {
        if (Same(sourceDir, installDir)) return "already running from the install directory";
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
            var dest = Path.Combine(installDir, Path.GetRelativePath(sourceDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
            count++;
        }
        return $"{count} file(s)";
    }

    private string DeleteProgram()
    {
        if (!Directory.Exists(installDir)) return "nothing to delete";
        var running = Environment.ProcessPath is { } p && p.StartsWith(installDir, StringComparison.OrdinalIgnoreCase);
        if (!running) { Directory.Delete(installDir, true); return installDir; }

        // The running executable is locked on Windows, so a detached command removes the folder once this process exits.
        Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c for /l %i in (1,1,60) do (rmdir /s /q \"{installDir}\" 2>nul & if not exist \"{installDir}\" exit /b & ping 127.0.0.1 -n 2 >nul)")
        { CreateNoWindow = true, UseShellExecute = false });
        return "scheduled, finishes when this window closes";
    }

    private bool Step(string title, Func<string> action)
    {
        try
        {
            var detail = console.Status().Spinner(Spinner.Known.Dots).Start($"[grey]{title}...[/]", _ => { Thread.Sleep(150); return action(); });
            console.MarkupLine($" {Done} {title} [grey]- {Markup.Escape(detail)}[/]");
            return true;
        }
        catch (Exception ex)
        {
            console.MarkupLine($" {Failed} {title} [red]- {Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private void Banner(string subtitle, string description)
    {
        Logo.Write(console);
        console.MarkupLine($"[bold {Theme.Accent}]{subtitle}[/] [grey]v{AppInfo.Version} - {description}[/]\n");
    }

    private int Unsupported()
    {
        console.MarkupLine($"{Warn} [yellow]Install and uninstall are only supported on Windows.[/]");
        return 1;
    }

    private bool AddToPath() => UpdatePath(paths => paths.Any(p => Same(p, installDir)) ? null : [.. paths, installDir]);

    private bool RemoveFromPath() => UpdatePath(paths => paths.Any(p => Same(p, installDir)) ? [.. paths.Where(p => !Same(p, installDir))] : null);

    private static bool Same(string a, string b) =>
        string.Equals(Path.GetFullPath(a.Trim()).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private static bool UpdatePath(Func<string[], string[]?> change)
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment", true)
            ?? throw new InvalidOperationException("Could not open the user environment registry key.");
        var current = key.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var updated = change(current.Split(';', StringSplitOptions.RemoveEmptyEntries));
        if (updated is null) return false;
        key.SetValue("Path", string.Join(';', updated), Microsoft.Win32.RegistryValueKind.ExpandString);
        NotifyEnvironmentChanged();
        return true;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);

    private static void NotifyEnvironmentChanged() =>
        SendMessageTimeout(new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "Environment", 2, 1000, out _);
}
