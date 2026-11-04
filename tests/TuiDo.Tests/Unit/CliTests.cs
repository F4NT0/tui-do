using Spectre.Console.Testing;
using TuiDo.Cli;

namespace TuiDo.Tests.Unit;

public class CliTests
{
    private sealed class FakeReleases(ReleaseInfo? latest, Exception? error = null) : IReleaseChecker
    {
        public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken = default) =>
            error is null ? Task.FromResult(latest) : Task.FromException<ReleaseInfo?>(error);
    }

    private static async Task<(int Code, string Output)> Run(string arg, IReleaseChecker? releases = null, IKeyReader? keys = null)
    {
        var console = new TestConsole().Width(120);
        var installer = new Installer(console, "install", "data", "source");
        var code = await new CliApp(console, releases ?? new FakeReleases(null), installer, keys ?? new FakeKeyReader(ConsoleKey.Escape)).RunAsync([arg]);
        return (code, console.Output);
    }

    private sealed class FakeKeyReader(ConsoleKey key) : IKeyReader
    {
        public ConsoleKeyInfo ReadKey() => new('\0', key, false, false, false);
    }

    [Theory]
    [InlineData("--version", true)]
    [InlineData("-v", true)]
    [InlineData("--help", true)]
    [InlineData("--install", true)]
    [InlineData("--uninstall", true)]
    [InlineData("something", false)]
    public void Recognises_cli_commands(string arg, bool expected) => Assert.Equal(expected, CliApp.IsCliCommand([arg]));

    [Fact]
    public void No_arguments_start_the_app() => Assert.False(CliApp.IsCliCommand([]));

    [Fact]
    public async Task Version_starts_at_1_0_0()
    {
        var (code, output) = await Run("--version");
        Assert.Equal(0, code);
        Assert.Contains("tuido v1.0.0", output);
    }

    [Fact]
    public async Task Version_reports_a_different_release()
    {
        var (_, output) = await Run("--version", new FakeReleases(new("v1.2.0", "https://example/r")));
        Assert.Contains("New release available", output);
        Assert.Contains("v1.2.0", output);
    }

    [Fact]
    public async Task Version_reports_up_to_date_when_the_release_matches()
    {
        var (_, output) = await Run("--version", new FakeReleases(new("v1.0.0", "https://example/r")));
        Assert.Contains("latest release", output);
    }

    [Fact]
    public async Task Version_without_published_releases_says_so()
    {
        var (_, output) = await Run("--version");
        Assert.Contains("No release has been published yet", output);
    }

    [Fact]
    public async Task Version_survives_an_unreachable_repository()
    {
        var (code, output) = await Run("--version", new FakeReleases(null, new HttpRequestException("offline")));
        Assert.Equal(0, code);
        Assert.Contains("Could not check for updates", output);
        Assert.Contains("offline", output);
    }

    [Fact]
    public async Task Help_lists_cli_commands()
    {
        var (code, output) = await Run("--help");
        Assert.Equal(0, code);
        foreach (var text in new[] { "tuido --version", "tuido --help", "tuido --install", "tuido --uninstall" })
            Assert.Contains(text, output);
    }

    [Theory]
    [InlineData(ConsoleKey.Enter, true)]
    [InlineData(ConsoleKey.Escape, false)]
    public void Splash_shows_logo_description_and_version_and_waits_for_a_choice(ConsoleKey key, bool start)
    {
        var console = new TestConsole().Width(100);

        Assert.Equal(start, SplashScreen.Show(console, new FakeKeyReader(key)));

        Assert.Contains("████████╗██╗", console.Output);
        Assert.Contains("Terminal-native to-do manager", console.Output);
        Assert.Contains("V1.0.0", console.Output);
        Assert.Contains("Start tuido", console.Output);
    }

    [Fact]
    public void Installer_banner_uses_the_ansi_shadow_logo()
    {
        var console = new TestConsole().Width(100);

        Logo.Write(console);

        Assert.Contains("╚══██╔══╝██║", console.Output);
    }

    [Theory]
    [InlineData("v1.1.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v0.9.0", "1.0.0", false)]
    [InlineData("v2.0.0-beta", "1.9.9", true)]
    public void Compares_versions_ignoring_prefix_and_suffix(string remote, string local, bool newer) =>
        Assert.Equal(newer, VersionComparer.IsNewer(remote, local));
}
