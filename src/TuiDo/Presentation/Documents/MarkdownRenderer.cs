using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace TuiDo.Presentation.Documents;

// Markdown renderer modeled on clidocs (highlight.go): same palette, boxed code
// blocks with language label + line numbers, grid tables, keyword/icon colors.
internal static class MarkdownRenderer
{
    const string Border = "#6e7681", Orange = "#e8912d", Green = "#3fb950", LightBlue = "#a5d6ff", Blue = "#58a6ff";

    static readonly Dictionary<string, string> PkgColors = new()
    {
        ["winget"] = Blue, ["scoop"] = Blue, ["powershell"] = Blue, ["cargo"] = Blue, ["npm"] = Blue, ["pip"] = Blue,
        ["brew"] = Blue, ["apt"] = Blue, ["choco"] = Blue, ["dotnet"] = Blue, ["nu"] = "#00ff00",
        ["install"] = "#f1fa8c", ["add"] = "#f1fa8c", ["remove"] = "#f1fa8c", ["update"] = "#f1fa8c",
        ["upgrade"] = "#f1fa8c", ["run"] = "#f1fa8c", ["build"] = "#f1fa8c",
    };

    static readonly HashSet<string> Keywords = ["if", "else", "for", "foreach", "while", "return", "function", "func", "class", "public", "private",
        "static", "var", "const", "let", "def", "import", "from", "using", "new", "void", "int", "string", "bool", "true", "false", "null", "async", "await"];

    static readonly Regex Inline = new(
        @"(?<code>`[^`]+`)|(?<bold>\*\*.+?\*\*)|(?<it>(?<![\w*])\*[^*\s][^*]*\*(?![\w*]))|(?<link>\[[^\]]+\]\([^)]+\))|(?<icon>:check:|:x:|:work:)|(?<kw>\b(?:TODO|DOING|WORKING|DONE|FINISH|FAIL|NOT)\b)",
        RegexOptions.Compiled);

    static readonly Regex Super = new(@"(\w)\^([{(]?[\w\d+\-*/=]+[)}]?)", RegexOptions.Compiled);
    static readonly Regex MathInline = new(@"\$\$?([^$\n]+?)\$?\$", RegexOptions.Compiled);

    public static IRenderable Render(string md)
    {
        md = md.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
        md = MathInline.Replace(md, m => Super.Replace(m.Groups[1].Value, s => s.Groups[1].Value + s.Groups[2].Value.Trim('{', '}', '(', ')')));
        md = Super.Replace(md, s => s.Groups[1].Value + s.Groups[2].Value.Trim('{', '}', '(', ')'));

        var lines = md.Split('\n');
        var parts = new List<IRenderable>();
        var para = new List<string>();

        void FlushPara()
        {
            if (para.Count == 0) return;
            parts.Add(new Markup(Inl(string.Join(' ', para))));
            para.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var t = line.Trim();

            if (t.StartsWith("```"))
            {
                FlushPara();
                var lang = t[3..].Trim();
                var code = new List<string>();
                i++;
                while (i < lines.Length && lines[i].Trim() != "```") code.Add(lines[i++]);
                parts.Add(CodeBlock(lang, code));
                continue;
            }
            if (t.StartsWith('|'))
            {
                FlushPara();
                var rows = new List<string>();
                while (i < lines.Length && lines[i].Trim().StartsWith('|')) rows.Add(lines[i++]);
                i--;
                var tbl = TableOf(rows);
                if (tbl != null) parts.Add(tbl);
                continue;
            }
            if (t.Length == 0) { FlushPara(); continue; }

            var h = Regex.Match(t, @"^(#{1,6})\s+(.*)$");
            if (h.Success)
            {
                FlushPara();
                var lvl = h.Groups[1].Length;
                var txt = Markup.Escape(h.Groups[2].Value);
                parts.Add(new Markup(lvl switch
                {
                    1 => $"[bold white]{txt}[/]",
                    2 => $"[bold {Green}]{txt}[/]",
                    3 => $"[bold {Orange}]{txt}[/]",
                    4 => $"[bold {LightBlue}]{txt}[/]",
                    5 => $"[{LightBlue}]{txt.ToUpperInvariant()}[/]",
                    _ => $"[{Border}]{txt.ToUpperInvariant()}[/]",
                }));
                continue;
            }
            if (Regex.IsMatch(t, @"^([-*_])(\s*\1){2,}$"))
            {
                FlushPara();
                parts.Add(new Rule().RuleStyle(Style.Parse(Border)));
                continue;
            }
            var li = Regex.Match(line, @"^(\s*)([-*+]|\d+[.)])\s+(.*)$");
            if (li.Success)
            {
                FlushPara();
                var indent = new string(' ', li.Groups[1].Length / 2 * 2 + 1);
                var bullet = char.IsDigit(li.Groups[2].Value[0]) ? $"[{Border}]{li.Groups[2].Value}[/]" : "•";
                var body = li.Groups[3].Value;
                var task = Regex.Match(body, @"^\[( |x|X)\]\s+(.*)$");
                if (task.Success)
                {
                    bullet = task.Groups[1].Value == " " ? "[blue]\uf4aa[/]" : "[green]\uf05d[/]";
                    body = task.Groups[2].Value;
                }
                parts.Add(new Markup($"{indent}{bullet} {Inl(body)}"));
                continue;
            }
            if (t.StartsWith('>'))
            {
                FlushPara();
                parts.Add(new Markup($"[{Border}]│[/] [italic]{Inl(t.TrimStart('>').Trim())}[/]"));
                continue;
            }
            para.Add(t);
        }
        FlushPara();

        var spaced = new List<IRenderable>();
        foreach (var p in parts) { if (spaced.Count > 0) spaced.Add(new Text("")); spaced.Add(p); }
        return new Rows(spaced);
    }

    static string Inl(string s)
    {
        var sb = new StringBuilder();
        int last = 0;
        foreach (Match m in Inline.Matches(s))
        {
            sb.Append(Markup.Escape(s[last..m.Index]));
            var v = m.Value;
            if (m.Groups["code"].Success) sb.Append($"[#ff5f5f on #303030] {Markup.Escape(v[1..^1])} [/]");
            else if (m.Groups["bold"].Success) sb.Append($"[bold {Orange}]{Markup.Escape(v[2..^2])}[/]");
            else if (m.Groups["it"].Success) sb.Append($"[italic]{Markup.Escape(v[1..^1])}[/]");
            else if (m.Groups["link"].Success)
            {
                var mm = Regex.Match(v, @"\[([^\]]+)\]\(([^)]+)\)");
                sb.Append($"[underline {Blue}]{Markup.Escape(mm.Groups[1].Value)}[/]");
            }
            else if (m.Groups["icon"].Success)
                sb.Append(v switch { ":check:" => "[#50c878]\uf05d[/]", ":x:" => "[#dc5050]\uf52f[/]", _ => "[#ffc83c]\uea6c[/]" });
            else sb.Append(v switch
            {
                "TODO" => "[#64b4ff]TODO[/]",
                "DOING" or "WORKING" => $"[#ffc83c]{v}[/]",
                "DONE" or "FINISH" => $"[#50c878]{v}[/]",
                _ => $"[#dc5050]{v}[/]",
            });
            last = m.Index + m.Length;
        }
        sb.Append(Markup.Escape(s[last..]));
        return sb.ToString();
    }

    static IRenderable? TableOf(List<string> rows)
    {
        var data = new List<List<string>>();
        foreach (var r in rows)
        {
            var row = r.Trim();
            if (Regex.IsMatch(row, @"^[\s|:\-]+$")) continue;
            var cells = row.Split('|').Select(c => c.Trim()).ToList();
            if (cells.Count > 0 && cells[0] == "") cells.RemoveAt(0);
            if (cells.Count > 0 && cells[^1] == "") cells.RemoveAt(cells.Count - 1);
            if (cells.Count > 0) data.Add(cells);
        }
        if (data.Count == 0) return null;
        int n = data.Max(r => r.Count);
        var table = new Table { Border = TableBorder.Square, BorderStyle = Style.Parse(Border) }.ShowRowSeparators();
        for (int c = 0; c < n; c++)
            table.AddColumn(new TableColumn(new Markup($"[bold {Blue}]{Markup.Escape(c < data[0].Count ? data[0][c] : "")}[/]")));
        foreach (var r in data.Skip(1))
            table.AddRow(Enumerable.Range(0, n).Select(c => (IRenderable)new Markup(Inl(c < r.Count ? r[c] : ""))));
        return table;
    }

    static IRenderable CodeBlock(string lang, List<string> code)
    {
        int numW = code.Count.ToString().Length;
        var rows = code.Select((l, i) => new Markup($"[{Border}]{(i + 1).ToString().PadLeft(numW)}[/]  {Highlight(l)}"));
        return new Panel(new Rows(rows.Cast<IRenderable>().DefaultIfEmpty(new Text(" "))))
        {
            Header = string.IsNullOrEmpty(lang) ? null : new PanelHeader(" " + lang + " "),
            Border = BoxBorder.Square,
            BorderStyle = Style.Parse(Border),
            Expand = false,
        };
    }

    static string Highlight(string line)
    {
        var first = line.Trim().Split(' ', 2)[0];
        var sb = new StringBuilder();
        var rx = new Regex(@"(?<c>#.*|//.*)|(?<s>""[^""]*""|'[^']*')|(?<n>\b\d+(\.\d+)?\b)|(?<w>[A-Za-z_][\w-]*)|(?<o>.)");
        bool pkg = PkgColors.ContainsKey(first);
        foreach (Match m in rx.Matches(line))
        {
            var v = Markup.Escape(m.Value);
            if (m.Groups["c"].Success) sb.Append($"[italic #6272a4]{v}[/]");
            else if (m.Groups["s"].Success) sb.Append($"[#f1fa8c]{v}[/]");
            else if (m.Groups["n"].Success) sb.Append($"[#bd93f9]{v}[/]");
            else if (m.Groups["w"].Success)
            {
                if (pkg && PkgColors.TryGetValue(m.Value, out var c)) sb.Append($"[{c}]{v}[/]");
                else if (!pkg && Keywords.Contains(m.Value)) sb.Append($"[#50fa7b]{v}[/]");
                else sb.Append(v);
            }
            else sb.Append(v);
        }
        return sb.ToString();
    }
}
