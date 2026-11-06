using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TuiDo.Installer;

internal static class Steps
{
    public const string AppRepository = "F4NT0/tui-do";
    public const string DotNetRuntimeUrl = "https://aka.ms/dotnet/10.0/dotnet-runtime-win-x64.exe";

    public static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "tuido");

    private static readonly string UserFontsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");

    public static bool IsDotNet10Installed()
    {
        try
        {
            var info = new ProcessStartInfo("dotnet", "--list-runtimes") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Split('\n').Any(l => l.StartsWith("Microsoft.NETCore.App 10."));
        }
        catch { return false; }
    }

    public static async Task<string> InstallDotNetAsync(string temp, Action<double> progress, Action<bool> indeterminate)
    {
        if (IsDotNet10Installed()) return ".NET 10 already installed";
        var installer = Path.Combine(temp, "dotnet-runtime-10-win-x64.exe");
        await GitHub.DownloadAsync(DotNetRuntimeUrl, installer, progress);

        indeterminate(true);
        var info = new ProcessStartInfo(installer, "/install /quiet /norestart") { UseShellExecute = true, Verb = "runas" };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the .NET installer.");
        await process.WaitForExitAsync();
        if (process.ExitCode is not (0 or 1638 or 3010)) throw new InvalidOperationException($".NET installer failed with exit code {process.ExitCode}.");
        return ".NET 10 installed";
    }

    public static bool IsNerdFontInstalled() =>
        Directory.Exists(UserFontsDir) && Directory.EnumerateFiles(UserFontsDir, "JetBrainsMonoNerdFont*.ttf").Any()
        || Directory.EnumerateFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "JetBrainsMonoNerdFont*.ttf").Any();

    public static async Task<string> InstallFontAsync(string temp, Action<double> progress, Action<string> describe)
    {
        if (IsNerdFontInstalled()) return "JetBrainsMono Nerd Font already installed";
        var release = await GitHub.LatestAsync("ryanoasis/nerd-fonts");
        var asset = release.Assets.FirstOrDefault(a => a.Name.Equals("JetBrainsMono.zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("JetBrainsMono.zip not found in the latest Nerd Fonts release.");

        var zip = Path.Combine(temp, asset.Name);
        await GitHub.DownloadAsync(asset.Url, zip, p => progress(p / 2));

        describe("Registering fonts");
        Directory.CreateDirectory(UserFontsDir);
        using var archive = ZipFile.OpenRead(zip);
        var fonts = archive.Entries.Where(e => e.Name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)).ToList();
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts");
        for (var i = 0; i < fonts.Count; i++)
        {
            var dest = Path.Combine(UserFontsDir, fonts[i].Name);
            fonts[i].ExtractToFile(dest, true);
            key.SetValue(Path.GetFileNameWithoutExtension(fonts[i].Name) + " (TrueType)", dest);
            AddFontResource(dest);
            progress(50 + (i + 1) * 50.0 / fonts.Count);
        }
        return $"{fonts.Count} font file(s) installed";
    }

    public static async Task<(string Path, string Tag)> DownloadAppAsync(string temp, Action<double> progress)
    {
        var release = await GitHub.LatestAsync(AppRepository);
        var asset = release.Assets
            .Where(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .Where(a => !a.Name.Contains("installer", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault() ?? throw new InvalidOperationException($"Release {release.Tag} has no Windows .exe or .zip asset.");

        var path = Path.Combine(temp, asset.Name);
        await GitHub.DownloadAsync(asset.Url, path, progress);
        return (path, release.Tag);
    }

    public static string InstallApp(string downloaded, Action<double> progress)
    {
        Directory.CreateDirectory(InstallDir);
        if (downloaded.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extract = Path.Combine(Path.GetDirectoryName(downloaded)!, "app");
            ZipFile.ExtractToDirectory(downloaded, extract, true);
            var files = Directory.GetFiles(extract, "*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                var dest = Path.Combine(InstallDir, Path.GetRelativePath(extract, files[i]));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(files[i], dest, true);
                progress((i + 1) * 100.0 / files.Length);
            }
        }
        else File.Copy(downloaded, Path.Combine(InstallDir, "tuido.exe"), true);
        progress(100);

        var exe = Path.Combine(InstallDir, "tuido.exe");
        return File.Exists(exe) ? exe : throw new FileNotFoundException("tuido.exe was not found after extraction.", exe);
    }

    public static string AddToPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey("Environment", true) ?? throw new InvalidOperationException("Could not open the user environment.");
        var current = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var paths = current.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Any(p => string.Equals(p.TrimEnd('\\'), InstallDir, StringComparison.OrdinalIgnoreCase))) return "already in PATH";
        key.SetValue("Path", string.Join(';', [.. paths, InstallDir]), RegistryValueKind.ExpandString);
        SendMessageTimeout(new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "Environment", 2, 1000, out _);
        return "PATH updated";
    }

    public static string Verify()
    {
        var info = new ProcessStartInfo(Path.Combine(InstallDir, "tuido.exe"), "--version") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not run tuido.exe.");
        var line = process.StandardOutput.ReadLine() ?? "";
        process.Kill();
        return line;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResource(string path);
}
