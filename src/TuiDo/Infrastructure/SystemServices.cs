using System.Diagnostics;
using System.Text;
using Spectre.Console;
using TuiDo.Abstractions;

namespace TuiDo.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}

internal sealed class ConsoleKeySource : IKeySource
{
    public bool Available => Console.KeyAvailable;

    public ConsoleKeyInfo Read() => Console.ReadKey(true);
}

internal sealed class ConsoleTerminal : ITerminal
{
    public int Width => Console.WindowWidth;

    public int Height => Console.WindowHeight;

    public bool CursorVisible { set => Console.CursorVisible = value; }

    public void Clear() => AnsiConsole.Clear();

    public void Home() => Console.SetCursorPosition(0, 0);
}

internal sealed class PowerShellClipboard : IClipboard
{
    public string GetText()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell", "-NoProfile -Command \"[Console]::OutputEncoding=[Text.Encoding]::UTF8; Get-Clipboard -Raw\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var process = Process.Start(psi)!;
            var text = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Replace("\t", "    ");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "";
        }
    }
}

internal sealed class ExplorerFolderOpener : IFolderOpener
{
    public void Open(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
    }
}

internal sealed class ExternalEditorLauncher(ITerminal terminal, IAnsiConsole console) : IEditorLauncher
{
    public IReadOnlyList<EditorInfo> Editors { get; } =
    [
        new("Neovim", "\uf36f", "nvim"),
        new("Vim", "\ue7c5", "vim"),
        new("VS Code", "\ue8da", "code"),
    ];

    public bool IsInstalled(EditorInfo editor) => FindExecutable(editor.Executable) != null;

    public string Edit(EditorInfo editor, string text, string? file = null)
    {
        var path = FindExecutable(editor.Executable) ?? throw new FileNotFoundException($"{editor.Name} is not installed");
        var temp = file ?? Path.Combine(Path.GetTempPath(), $"tui-do-{Guid.NewGuid():N}.md");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(temp))!);
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            terminal.Clear();
            terminal.CursorVisible = true;
            var isCode = editor.Executable == "code";
            if (isCode) console.MarkupLine($"[grey]Editing in {editor.Name}. Close the file tab to return to tui-do...[/]");
            var psi = isCode
                ? new ProcessStartInfo("cmd.exe", $"/c \"\"{path}\" --wait \"{temp}\"\"") { UseShellExecute = false, CreateNoWindow = true }
                : new ProcessStartInfo(path, $"\"{temp}\"") { UseShellExecute = false };
            using (var process = Process.Start(psi)) process?.WaitForExit();
            var edited = File.ReadAllText(temp).Replace("\r\n", "\n").TrimEnd('\n');
            if (file != null && string.IsNullOrWhiteSpace(edited)) File.Delete(temp);
            return edited;
        }
        finally
        {
            if (file == null)
            {
                try { File.Delete(temp); }
                catch (IOException) { }
            }

            terminal.CursorVisible = false;
            terminal.Clear();
        }
    }

    private static string? FindExecutable(string name)
    {
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                foreach (var extension in extensions)
                {
                    var candidate = Path.Combine(dir.Trim('"'), name + extension);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch (ArgumentException) { }
        }

        return null;
    }
}
