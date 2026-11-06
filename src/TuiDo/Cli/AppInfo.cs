using System.Reflection;

namespace TuiDo.Cli;

internal static class AppInfo
{
    public const string Command = "tuido";
    public const string Repository = "F4NT0/tui-do";
    public const string ReleasesUrl = "https://github.com/" + Repository + "/releases";

    public static string Version { get; } =
        (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.1")
        .Split('+')[0];

    public static string ExeName => OperatingSystem.IsWindows() ? Command + ".exe" : Command;

    public static string DefaultInstallDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Command);
}
