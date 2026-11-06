using System.Globalization;
using System.Runtime.CompilerServices;

namespace TuiDo.Tests.Support;

internal static class TestEnvironment
{
    /// <summary>The UI is English with fixed formats; keep tests independent from the machine's regional settings.</summary>
    [ModuleInitializer]
    public static void UseInvariantCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }
}

/// <summary>The Nerd Font glyphs used by the UI, so assertions read like the design.</summary>
internal static class Glyph
{
    public const string NotStarted = "\uf4aa";
    public const string Doing = "\uf192";
    public const string NeedMoreDetails = "\uf29c";
    public const string Event = "\uf2be";
    public const string Blocked = "\udb80\udcad";
    public const string Completed = "\uf05d";
    public const string Cancelled = "\uf52f";
    public const string Overdue = "\uea6c";
    public const string StarOn = "\udb81\udcce";
    public const string StarOff = "\uf41e";
    public const string MyDay = "\uf522";
    public const string Tasks = "\uf412";
    public const string Neovim = "\uf36f";
    public const string Vim = "\ue7c5";
    public const string VsCode = "\ue8da";
}
