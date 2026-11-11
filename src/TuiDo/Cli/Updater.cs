using Spectre.Console;
using TuiDo.Presentation;

namespace TuiDo.Cli;

internal sealed class Updater(IAnsiConsole console, IReleaseChecker releases, string? installDir = null, HttpMessageHandler? handler = null)
{
    public async Task<int> RunAsync()
    {
        console.MarkupLine($"[bold {Theme.Accent}]tuido[/] [bold white]v{AppInfo.Version}[/]");

        ReleaseInfo? latest;
        try { latest = await releases.GetLatestAsync(); }
        catch (Exception ex) { return Fail($"Could not check for updates: {ex.Message}"); }

        if (latest is null) { console.MarkupLine("[grey]No release has been published yet.[/]"); return 0; }
        if (!VersionComparer.IsNewer(latest.Tag, AppInfo.Version)) { console.MarkupLine("[green]\uf05d You are on the latest release.[/]"); return 0; }
        if (latest.AssetUrl is null) return Fail($"Release {latest.Tag} has no {AppInfo.ExeName} asset.");

        var target = ResolveTarget();
        console.MarkupLine($"[orange1]\uea6c New release available:[/] [bold]{Markup.Escape(latest.Tag)}[/]");
        try
        {
            await console.Status().Spinner(Spinner.Known.Dots).StartAsync($"[grey]Downloading {Markup.Escape(latest.Tag)}...[/]", async _ => await ReplaceAsync(latest.AssetUrl, target));
        }
        catch (Exception ex) { return Fail($"Update failed: {ex.Message}"); }

        console.MarkupLine($"[green]\uf05d Updated to {Markup.Escape(latest.Tag)}[/] [grey]({Markup.Escape(target)})[/]");
        return 0;
    }

    private string ResolveTarget()
    {
        var dir = installDir ?? AppInfo.DefaultInstallDir;
        var installed = Path.Combine(dir, AppInfo.ExeName);
        if (File.Exists(installed) || Environment.ProcessPath is not { } running) return installed;
        return running;
    }

    private async Task ReplaceAsync(string url, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var fresh = target + ".new";
        var old = target + ".old";

        using (var http = handler is null ? new HttpClient() : new HttpClient(handler))
        {
            http.Timeout = TimeSpan.FromMinutes(5);
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Command}/{AppInfo.Version}");
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(fresh);
            await response.Content.CopyToAsync(file);
        }

        // A running executable cannot be overwritten on Windows, but it can be renamed.
        if (File.Exists(old)) File.Delete(old);
        if (File.Exists(target)) File.Move(target, old);
        File.Move(fresh, target);
        try { File.Delete(old); } catch { }
    }

    private int Fail(string message)
    {
        console.MarkupLine($"[red]\uf52f {Markup.Escape(message)}[/]");
        return 1;
    }
}
