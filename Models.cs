using System.Text.Json;

enum Status { Pending = 0, Done = 2, Cancelled = 3, Waiting = 4, Blocked = 5, Doing = 6 }

class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Code { get; set; }
    public string Title { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public string Notes { get; set; } = "";
    public Status Status { get; set; } = Status.Pending;
    public bool Important { get; set; }
    public int? BlockedBy { get; set; }
    public Guid? ListId { get; set; }
    public int Priority { get; set; }
}

static class Store
{
    static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tui-do", "tasks.json");

    public static string FilePath => Path;
    public static List<TodoItem> Load()
    {
        List<TodoItem> items;
        try { items = File.Exists(Path) ? JsonSerializer.Deserialize<List<TodoItem>>(File.ReadAllText(Path)) ?? [] : []; }
        catch { items = []; }
        var next = items.Select(i => i.Code).DefaultIfEmpty(0).Max() + 1;
        foreach (var i in items.Where(i => i.Code == 0)) i.Code = next++;
        return items;
    }

    public static void Save(List<TodoItem> items)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(items));
    }
}

static class Clip
{
    public static string Get()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("powershell", "-NoProfile -Command \"[Console]::OutputEncoding=[Text.Encoding]::UTF8; Get-Clipboard -Raw\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            using var p = System.Diagnostics.Process.Start(psi)!;
            var s = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return s.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Replace("\t", "    ");
        }
        catch { return ""; }
    }
}

class CustomList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "\uf022";
}

static class ListStore
{
    static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tui-do", "lists.json");

    public static string FilePath => Path;
    public static List<CustomList> Load()
    {
        try { return File.Exists(Path) ? JsonSerializer.Deserialize<List<CustomList>>(File.ReadAllText(Path)) ?? [] : []; }
        catch { return []; }
    }

    public static void Save(List<CustomList> lists)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(lists));
    }

    public static string ParseIcon(string s)
    {
        s = s.Trim();
        if (s.Length == 0) return "\uf022";
        var bs = ((char)92).ToString();
        var pat = bs + bs + "u([0-9a-fA-F]{4})";
        if (System.Text.RegularExpressions.Regex.IsMatch(s, pat))
            return System.Text.RegularExpressions.Regex.Replace(s, pat, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        var h = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
        if (h.Length is >= 4 and <= 6 && h.All(Uri.IsHexDigit))
        {
            var cp = Convert.ToInt32(h, 16);
            if (cp is > 0 and <= 0x10FFFF && !(cp >= 0xD800 && cp <= 0xDFFF)) return char.ConvertFromUtf32(cp);
        }
        return s;
    }
}
