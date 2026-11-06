namespace TuiDo.Presentation;

/// <summary>Navigation state of the floating folder browser. A null <see cref="Current"/> lists the drives.</summary>
internal sealed class DirectoryBrowser
{
    public const int Visible = 12;

    public DirectoryBrowser(string start)
    {
        Current = Directory.Exists(start) ? Path.GetFullPath(start) : Directory.GetCurrentDirectory();
        Refresh();
    }

    public string? Current { get; private set; }

    public int Cursor { get; private set; }

    public List<(string Name, string Path)> Entries { get; private set; } = [];

    public int First => Math.Clamp(Cursor - Visible / 2, 0, Math.Max(0, Entries.Count - Visible));

    /// <summary>The highlighted directory, or the one being viewed when it has no sub-directories.</summary>
    public string? Selected => Entries.Count > 0 ? Entries[Cursor].Path : Current;

    public void Move(int delta) => Cursor = Math.Clamp(Cursor + delta, 0, Math.Max(0, Entries.Count - 1));

    public void Enter()
    {
        if (Entries.Count == 0) return;
        Current = Entries[Cursor].Path;
        Cursor = 0;
        Refresh();
    }

    public void Up()
    {
        if (Current == null) return;
        var previous = Trim(Current);
        Current = Path.GetDirectoryName(previous);
        Refresh();
        Cursor = Math.Max(0, Entries.FindIndex(e => string.Equals(Trim(e.Path), previous, StringComparison.OrdinalIgnoreCase)));
    }

    private void Refresh()
    {
        Entries = List(Current);
        Cursor = Math.Clamp(Cursor, 0, Math.Max(0, Entries.Count - 1));
    }

    private static string Trim(string path) => path.TrimEnd(Path.DirectorySeparatorChar);

    private static List<(string Name, string Path)> List(string? directory)
    {
        try
        {
            if (directory == null) return DriveInfo.GetDrives().Select(d => (d.Name, d.Name)).ToList();
            return new DirectoryInfo(directory).EnumerateDirectories()
                .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden) && !d.Attributes.HasFlag(FileAttributes.System))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Select(d => (d.Name, d.FullName)).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }
}
