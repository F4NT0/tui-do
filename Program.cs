using System.Globalization;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

new App().Run();

class App
{
    static readonly string[] Menu = ["My Day", "Tasks", "Important", "Completed", "Cancelled"];
    static readonly string[] MenuIcons = ["[yellow]\uf522[/]", "[#c4a7e7]\uf412[/]", "[orange1]\udb81\udcce[/]", "[green]\uf05d[/]", "[red]\uf52f[/]"];
    const string Accent = "deepskyblue1";

    readonly List<CustomList> _lists = ListStore.Load();
    LForm? _lform;
    int? _movePick;
    TodoItem? _moveTarget;

    int MenuCount => Menu.Length + _lists.Count;
    string MenuName(int i) => i < Menu.Length ? Menu[i] : _lists[i - Menu.Length].Name;
    string MenuIcon(int i) => i < Menu.Length ? MenuIcons[i] : $"[#8be9fd]{Markup.Escape(_lists[i - Menu.Length].Icon)}[/]";
    static string Star(bool on) => on ? "[orange1]\udb81\udcce[/]" : "[grey]\uf41e[/]";

    class LForm
    {
        public string Name = "", Icon = "";
        public int Field;
        public string Error = "";
        public string Value { get => Field == 0 ? Name : Icon; set { if (Field == 0) Name = value; else Icon = value; } }
    }

    static readonly string[] StatusNames = ["Not started", "Doing", "Need more details", "Blocked", "Completed", "Cancelled"];
    static readonly Status[] StatusValues = [Status.Pending, Status.Doing, Status.Waiting, Status.Blocked, Status.Done, Status.Cancelled];

    readonly List<TodoItem> _items = Store.Load();
    int _menu, _sel;
    bool _focusRight, _detail, _searching;
    string? _query;
    int? _picker, _blockPick;
    int _scroll;
    ScrollView? _view;
    List<TodoItem> _blockers = [];
    TodoItem? _target;
    Form? _form;

    static bool IsOpen(TodoItem i) => i.Status is not (Status.Done or Status.Cancelled);

    IEnumerable<TodoItem> Filter(int menu) => menu switch
    {
        0 => _items.Where(i => i.ListId == null && IsOpen(i) && i.Date.Date <= DateTime.Today),
        1 => _items.Where(i => i.ListId == null && IsOpen(i) && i.Date.Date > DateTime.Today),
        2 => _items.Where(i => IsOpen(i) && i.Important),
        3 => _items.Where(i => i.Status == Status.Done),
        4 => _items.Where(i => i.Status == Status.Cancelled),
        _ => _items.Where(i => i.ListId == _lists[menu - Menu.Length].Id),
    };

    List<TodoItem> Current()
    {
        IEnumerable<TodoItem> src;
        if (_query != null)
        {
            var q = _query.Trim().TrimStart('#');
            src = _items.Where(i => q.Length == 0 || i.Code.ToString() == q || i.Title.Contains(_query.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        else src = Filter(_menu);
        if (Ranked) return src.OrderBy(i => i.Priority == 0 ? int.MaxValue : i.Priority).ThenBy(i => i.Date).ThenBy(i => i.Code).ToList();
        return src.OrderBy(i => i.Date).ThenBy(i => i.Code).ToList();
    }

    readonly List<(DateTime At, string Msg, bool Ok)> _notes = [];
    bool _showPath;

    void Notify(string msg, bool ok = true)
    {
        _notes.Add((DateTime.Now, msg, ok));
        if (_notes.Count > 200) _notes.RemoveAt(0);
    }

    void SaveItems(string? ok = null)
    {
        try { Store.Save(_items); if (ok != null) Notify(ok); }
        catch (Exception e) { Notify($"Failed to save tasks: {e.Message}", false); }
    }

    void SaveLists(string? ok = null)
    {
        try { ListStore.Save(_lists); if (ok != null) Notify(ok); }
        catch (Exception e) { Notify($"Failed to save lists: {e.Message}", false); }
    }

    IRenderable TopBar()
    {
        var clock = new Panel(new Markup($"[bold green]{DateTime.Now:dd/MM/yyyy HH:mm:ss}[/]"))
            { Border = BoxBorder.Rounded, BorderStyle = Style.Parse("green"), Expand = true };
        var n = ActiveNote();
        string text = "";
        if (n != null)
        {
            var max = Math.Max(10, Console.WindowWidth - 25 - 6);
            var msg = n.Value.Msg.Replace('\n', ' ');
            if (msg.Length > max) msg = msg[..(max - 1)] + "…";
            var c = n.Value.Ok ? "green" : "red";
            text = $"[{c}]{(n.Value.Ok ? "\uf05d" : "\uf52f")} {Markup.Escape(msg)}[/]";
        }
        var notes = new Panel(new Markup(text.Length == 0 ? " " : text)) { Border = BoxBorder.Rounded, BorderStyle = Style.Parse(n == null ? "grey" : n.Value.Ok ? "green" : "red"), Expand = true };
        return new Layout("top").SplitColumns(new Layout("clock", clock).Size(25), new Layout("notes", notes));
    }

    (DateTime At, string Msg, bool Ok)? ActiveNote() =>
        _notes.Count > 0 && (DateTime.Now - _notes[^1].At).TotalSeconds < 2 ? _notes[^1] : null;
    int NoteSig() => ActiveNote() is { } a ? _notes.Count : 0;

    DateTime? _cal;

    void HandleCal(ConsoleKeyInfo k)
    {
        var d = _cal!.Value;
        _cal = k.Key switch
        {
            ConsoleKey.LeftArrow or ConsoleKey.H => d.AddDays(-1),
            ConsoleKey.RightArrow or ConsoleKey.L => d.AddDays(1),
            ConsoleKey.UpArrow or ConsoleKey.K => d.AddDays(-7),
            ConsoleKey.DownArrow or ConsoleKey.J => d.AddDays(7),
            ConsoleKey.PageUp => d.AddMonths(-1),
            ConsoleKey.PageDown => d.AddMonths(1),
            ConsoleKey.T => DateTime.Today,
            ConsoleKey.Escape or ConsoleKey.C => null,
            _ => d,
        };
    }

    IRenderable CalModal()
    {
        var sel = _cal!.Value.Date;
        var first = new DateTime(sel.Year, sel.Month, 1);
        int days = DateTime.DaysInMonth(sel.Year, sel.Month);
        var table = new Table { Border = TableBorder.Rounded, BorderStyle = Style.Parse("#6e7681") }.ShowRowSeparators();
        foreach (var wd in new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" })
            table.AddColumn(new TableColumn($"[bold #e8912d]{wd}[/]") { Width = 9, NoWrap = true });
        var cells = new List<IRenderable>();
        for (int i = 0; i < (int)first.DayOfWeek; i++) cells.Add(new Text(" "));
        for (int day = 1; day <= days; day++)
        {
            var date = new DateTime(sel.Year, sel.Month, day);
            var tasks = _items.Where(t => t.Date.Date == date).OrderBy(t => t.Code).ToList();
            var num = date == sel ? $"[black on {Accent}] {day,2} [/]" : date == DateTime.Today ? $"[bold green] {day,2} [/]" : $"[grey] {day,2} [/]";
            var count = tasks.Count > 0 ? $" [bold]({tasks.Count})[/]" : "";
            var icons = tasks.Count == 0 ? " " : string.Join("", tasks.Take(5).Select(t => Icon(t))) + (tasks.Count > 5 ? "[grey]+[/]" : "");
            cells.Add(new Markup($"{num}{count}\n{icons}"));
        }
        while (cells.Count % 7 != 0) cells.Add(new Text(" "));
        for (int r = 0; r < cells.Count; r += 7) table.AddRow(cells.Skip(r).Take(7));

        var dayTasks = _items.Where(t => t.Date.Date == sel).OrderBy(t => t.Code).ToList();
        var list = dayTasks.Count == 0
            ? [new Markup("[grey]No tasks this day.[/]")]
            : dayTasks.Take(5).Select(t => new Markup($"{Icon(t)}  [grey]#{t.Code}[/] {Markup.Escape(t.Title)}  [grey]{StatusNames[Math.Max(0, Array.IndexOf(StatusValues, t.Status))]}[/]")).ToArray();
        return new Panel(new Rows(
                new Markup($"[bold]{sel:MMMM yyyy}[/]  [grey]·  {sel:dd/MM/yyyy}[/]"),
                table,
                new Markup($"[bold #e8912d]Tasks on {sel:dd/MM}[/]"),
                new Rows(list),
                new Markup("[grey]←→↑↓: day  ·  PgUp/PgDn: month  ·  t: today  ·  Esc/c: close[/]")))
            { Header = H("\uf073 Calendar"), Border = BoxBorder.Rounded, BorderStyle = Style.Parse(Accent), Padding = new Padding(1, 0) };
    }

    IRenderable PathModal()
    {
        var rows = new IRenderable[]
        {
            new Markup("[grey]Your task lists are stored as JSON here:[/]"),
            new Text(""),
            new Markup($"[bold #e8912d]Tasks[/]  {Markup.Escape(Store.FilePath)}"),
            new Markup($"[bold #e8912d]Lists[/]  {Markup.Escape(ListStore.FilePath)}"),
            new Text(""),
            new Markup("[grey]Enter: open folder  ·  Esc/o: close[/]"),
        };
        return new Panel(new Rows(rows)) { Header = H("\uf07b Data location"), Border = BoxBorder.Rounded, BorderStyle = Style.Parse(Accent), Padding = new Padding(1, 0) };
    }

    void HandlePath(ConsoleKeyInfo k)
    {
        if (k.Key == ConsoleKey.Enter)
        {
            try
            {
                var dir = Path.GetDirectoryName(Store.FilePath)!;
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                Notify("Opened data folder");
            }
            catch (Exception e) { Notify($"Could not open folder: {e.Message}", false); }
            _showPath = false;
        }
        else if (k.Key == ConsoleKey.Escape || k.KeyChar == 'o') _showPath = false;
    }

    bool Ranked => _query == null && (_menu == 0 || _menu >= Menu.Length);
    bool _reorder;
    string? _prioInput;

    int NextPriority() => _items.Select(i => i.Priority).DefaultIfEmpty(0).Max() + 1;

    void Normalize(List<TodoItem> visible)
    {
        var seen = new HashSet<int>();
        foreach (var i in visible) if (i.Priority == 0 || !seen.Add(i.Priority)) { i.Priority = NextPriority(); seen.Add(i.Priority); }
    }

    void MoveTask(List<TodoItem> list, int from, int to)
    {
        to = Math.Clamp(to, 0, list.Count - 1);
        if (to == from) return;
        Normalize(list);
        var values = list.Select(i => i.Priority).OrderBy(v => v).ToList();
        var it = list[from];
        list.RemoveAt(from); list.Insert(to, it);
        for (int n = 0; n < list.Count; n++) list[n].Priority = values[n];
        _sel = to;
        SaveItems($"'{it.Title}' moved to position {to + 1}");
    }

    void HandlePrio(ConsoleKeyInfo k)
    {
        switch (k.Key)
        {
            case ConsoleKey.Escape: _prioInput = null; break;
            case ConsoleKey.Enter:
                var list = Current();
                if (int.TryParse(_prioInput, out var n) && list.Count > 0) MoveTask(list, _sel, n - 1);
                _prioInput = null; break;
            case ConsoleKey.Backspace: if (_prioInput!.Length > 0) _prioInput = _prioInput[..^1]; break;
            default: if (char.IsDigit(k.KeyChar) && _prioInput!.Length < 4) _prioInput += k.KeyChar; break;
        }
    }

    static PanelHeader H(string s) => new("\u2800" + s + "\u2800");

    static string StatusIcon(Status s) => s switch
    {
        Status.Done => "[green]\uf05d[/]",
        Status.Cancelled => "[red]\uf52f[/]",
        Status.Waiting => "[hotpink]\uf501[/]",
        Status.Doing => "[orange1]\ueba6[/]",
        Status.Blocked => "[grey]\udb80\udcad[/]",
        _ => "[blue]\uf4aa[/]",
    };

    static string Icon(TodoItem i) =>
        i.Status == Status.Pending && i.Date.Date < DateTime.Today ? "[yellow]\uea6c[/]" : StatusIcon(i.Status);

    public void Run()
    {
        Console.CursorVisible = false;
        AnsiConsole.Clear();
        int w = 0, h = 0;
        void Draw()
        {
            if (w != Console.WindowWidth || h != Console.WindowHeight)
            {
                w = Console.WindowWidth; h = Console.WindowHeight;
                AnsiConsole.Clear();
            }
            Console.SetCursorPosition(0, 0);
            AnsiConsole.Write(Build(h - 1));
        }
        while (true)
        {
            Draw();
            var sec = DateTime.Now.Second; var sig = NoteSig();
            while (!Console.KeyAvailable)
            {
                Thread.Sleep(50);
                if (DateTime.Now.Second != sec || NoteSig() != sig || w != Console.WindowWidth || h != Console.WindowHeight) { Draw(); sec = DateTime.Now.Second; sig = NoteSig(); }
            }
            var key = Console.ReadKey(true);
            if (_cal != null) HandleCal(key);
            else if (_showPath) HandlePath(key);
            else if (_form != null)
            {
                HandleForm(key);
                while (_form != null && Console.KeyAvailable) HandleForm(Console.ReadKey(true));
            }
            else if (_lform != null) HandleLForm(key);
            else if (_movePick != null) HandleMove(key);
            else if (_blockPick != null) HandleBlockPick(key);
            else if (_picker != null) HandlePicker(key);
            else if (_prioInput != null) HandlePrio(key);
            else if (_help != null) HandleHelp(key);
            else if (_searching) HandleSearch(key);
            else if (HandleMain(key)) break;
        }
        Console.CursorVisible = true;
        AnsiConsole.Clear();
    }

    void OpenPicker(TodoItem item)
    {
        _target = item;
        _picker = Math.Max(0, Array.IndexOf(StatusValues, item.Status));
    }

    bool HandleMain(ConsoleKeyInfo k)
    {
        var list = Current();
        _sel = Math.Clamp(_sel, 0, Math.Max(0, list.Count - 1));
        var item = list.Count > 0 ? list[_sel] : null;
        if (k.KeyChar == 'o') { _showPath = true; return false; }
        if (k.Key == ConsoleKey.C && _focusRight && !_detail) { _cal = DateTime.Today; return false; }
        if (k.KeyChar == '?') { _help = _detail ? 2 : _focusRight ? 1 : 0; return false; }
        if (k.KeyChar == '/') { _query = ""; _searching = true; _sel = 0; _detail = false; _focusRight = true; return false; }
        if (_detail)
        {
            if (item == null || k.Key == ConsoleKey.Escape) _detail = false;
            else if (k.Key == ConsoleKey.E) _form = new Form(item, true);
            else if (k.Key == ConsoleKey.Spacebar) OpenPicker(item);
            else if (k.Key == ConsoleKey.M) OpenMove(item);
            else if (_view != null)
            {
                var max = Math.Max(0, _view.Total - _view.Visible);
                _scroll = k.Key switch
                {
                    ConsoleKey.UpArrow or ConsoleKey.K => _scroll - 1,
                    ConsoleKey.DownArrow or ConsoleKey.J => _scroll + 1,
                    ConsoleKey.PageUp => _scroll - _view.Visible + 1,
                    ConsoleKey.PageDown => _scroll + _view.Visible - 1,
                    ConsoleKey.Home => 0,
                    ConsoleKey.End => max,
                    _ => _scroll,
                };
                _scroll = Math.Clamp(_scroll, 0, max);
            }
            return false;
        }
        if (_reorder)
        {
            if (k.Key is ConsoleKey.UpArrow or ConsoleKey.K) MoveTask(list, _sel, _sel - 1);
            else if (k.Key is ConsoleKey.DownArrow or ConsoleKey.J) MoveTask(list, _sel, _sel + 1);
            else if (k.Key is ConsoleKey.Enter or ConsoleKey.Escape or ConsoleKey.P) _reorder = false;
            return false;
        }
        if (k.KeyChar == 'P' && _focusRight && Ranked && item != null) { _prioInput = ""; return false; }
        if (k.Key == ConsoleKey.P && _focusRight && Ranked && item != null) { _reorder = true; return false; }
        switch (k.Key)
        {
            case ConsoleKey.Q: return true;
            case ConsoleKey.N: _form = new Form(new TodoItem { ListId = _menu >= Menu.Length && _query == null ? _lists[_menu - Menu.Length].Id : null }); break;
            case ConsoleKey.L: _lform = new LForm(); break;
            case ConsoleKey.M when item != null: OpenMove(item); break;
            case ConsoleKey.X when !_focusRight && _menu >= Menu.Length:
                var gone = _lists[_menu - Menu.Length];
                foreach (var o in _items.Where(o => o.ListId == gone.Id)) o.ListId = null;
                _lists.Remove(gone); _menu = Math.Min(_menu, MenuCount - 1); _sel = 0;
                SaveLists(); SaveItems($"List '{gone.Name}' deleted"); break;
            case ConsoleKey.E when item != null: _form = new Form(item, true); break;
            case ConsoleKey.UpArrow or ConsoleKey.K:
                if (_focusRight) _sel = Math.Max(0, _sel - 1); else { _menu = Math.Max(0, _menu - 1); _sel = 0; }
                break;
            case ConsoleKey.DownArrow or ConsoleKey.J:
                if (_focusRight) _sel = Math.Min(Math.Max(0, list.Count - 1), _sel + 1); else { _menu = Math.Min(MenuCount - 1, _menu + 1); _sel = 0; }
                break;
            case ConsoleKey.Enter when _focusRight && item != null: { _detail = true; _scroll = 0; } break;
            case ConsoleKey.RightArrow or ConsoleKey.Enter: _focusRight = true; break;
            case ConsoleKey.Tab: _focusRight = !_focusRight; break;
            case ConsoleKey.Escape when _query != null: _query = null; _sel = 0; break;
            case ConsoleKey.LeftArrow or ConsoleKey.Escape: _focusRight = false; break;
            case ConsoleKey.Spacebar when item != null: OpenPicker(item); break;
            case ConsoleKey.I when item != null: item.Important = !item.Important; SaveItems($"#{item.Code} {(item.Important ? "marked" : "unmarked")} as important"); break;
            case ConsoleKey.D when item != null:
                _items.Remove(item);
                foreach (var o in _items.Where(o => o.BlockedBy == item.Code)) { o.BlockedBy = null; if (o.Status == Status.Blocked) o.Status = Status.Pending; }
                SaveItems($"Task #{item.Code} deleted"); break;
        }
        return false;
    }

    void OpenMove(TodoItem item)
    {
        _moveTarget = item;
        _movePick = item.ListId == null ? 0 : 1 + Math.Max(0, _lists.FindIndex(l => l.Id == item.ListId));
    }

    void HandleMove(ConsoleKeyInfo k)
    {
        switch (k.Key)
        {
            case ConsoleKey.UpArrow: _movePick = Math.Max(0, _movePick!.Value - 1); break;
            case ConsoleKey.DownArrow: _movePick = Math.Min(_lists.Count, _movePick!.Value + 1); break;
            case ConsoleKey.Enter:
                _moveTarget!.ListId = _movePick == 0 ? null : _lists[_movePick!.Value - 1].Id;
                _moveTarget.Priority = NextPriority();
                SaveItems($"#{_moveTarget.Code} moved"); _movePick = null; _detail = false; break;
            case ConsoleKey.Escape: _movePick = null; break;
        }
    }

    void HandleLForm(ConsoleKeyInfo k)
    {
        var f = _lform!;
        if (k.Key == ConsoleKey.Escape) { _lform = null; return; }
        if ((k.Key == ConsoleKey.V && k.Modifiers.HasFlag(ConsoleModifiers.Control)) || (k.Key == ConsoleKey.Insert && k.Modifiers.HasFlag(ConsoleModifiers.Shift)) || k.KeyChar == (char)22)
        { f.Value += Clip.Get().Replace("\n", " "); return; }
        if (k.Key == ConsoleKey.Enter)
        {
            if (string.IsNullOrWhiteSpace(f.Name)) { f.Error = "Name is required"; Notify("List name is required", false); return; }
            _lists.Add(new CustomList { Name = f.Name.Trim(), Icon = ListStore.ParseIcon(f.Icon) });
            SaveLists($"List '{f.Name.Trim()}' created"); _menu = Menu.Length + _lists.Count - 1; _sel = 0; _lform = null; return;
        }
        f.Error = "";
        if (k.Key is ConsoleKey.Tab or ConsoleKey.UpArrow or ConsoleKey.DownArrow) { f.Field = 1 - f.Field; return; }
        if (k.Key == ConsoleKey.Backspace) { if (f.Value.Length > 0) f.Value = f.Value[..^1]; return; }
        if (!char.IsControl(k.KeyChar)) f.Value += k.KeyChar;
    }

    IRenderable LFormPanel()
    {
        var f = _lform!;
        IRenderable Box(string label, string text, int idx) => new Panel(new Markup(Markup.Escape(text) + (f.Field == idx ? "[blink]▏[/]" : "") is { Length: > 0 } s ? s : " "))
        { Header = H(label), Border = BoxBorder.Rounded, Expand = true, BorderStyle = Style.Parse(f.Field == idx ? "orange1" : "grey") };
        var parts = new List<IRenderable>
        {
            Box("Name", f.Name, 0),
            Box("Icon (e.g. f522, \\uf522 or paste the glyph)", f.Icon, 1),
            new Markup($"Preview: [#8be9fd]{Markup.Escape(ListStore.ParseIcon(f.Icon))}[/]  {Markup.Escape(f.Name)}"),
        };
        if (f.Error != "") parts.Add(new Markup($"[red]{Markup.Escape(f.Error)}[/]"));
        return new Panel(new Rows(parts)) { Header = H("\uf067 New list"), Border = BoxBorder.Rounded, BorderStyle = Style.Parse(Accent), Width = 64 };
    }

    void HandleSearch(ConsoleKeyInfo k)
    {
        switch (k.Key)
        {
            case ConsoleKey.Escape: _searching = false; _query = null; _focusRight = false; _sel = 0; break;
            case ConsoleKey.Enter: _searching = false; break;
            case ConsoleKey.Backspace: if (_query!.Length > 0) _query = _query[..^1]; _sel = 0; break;
            default: if (!char.IsControl(k.KeyChar)) { _query += k.KeyChar; _sel = 0; } break;
        }
    }

    void HandlePicker(ConsoleKeyInfo k)
    {
        switch (k.Key)
        {
            case ConsoleKey.UpArrow: _picker = Math.Max(0, _picker!.Value - 1); break;
            case ConsoleKey.DownArrow: _picker = Math.Min(StatusValues.Length - 1, _picker!.Value + 1); break;
            case ConsoleKey.Enter:
                var st = StatusValues[_picker!.Value];
                _picker = null;
                if (st == Status.Blocked)
                {
                    _blockers = _items.Where(i => i != _target && IsOpen(i)).OrderBy(i => i.Code).ToList();
                    if (_blockers.Count > 0) _blockPick = 0;
                    break;
                }
                _target!.Status = st; _target.BlockedBy = null;
                SaveItems($"#{_target.Code} status: {StatusNames[Array.IndexOf(StatusValues, st)]}"); break;
            case ConsoleKey.Escape: _picker = null; break;
        }
    }

    void HandleBlockPick(ConsoleKeyInfo k)
    {
        switch (k.Key)
        {
            case ConsoleKey.UpArrow: _blockPick = Math.Max(0, _blockPick!.Value - 1); break;
            case ConsoleKey.DownArrow: _blockPick = Math.Min(_blockers.Count - 1, _blockPick!.Value + 1); break;
            case ConsoleKey.Enter:
                _target!.Status = Status.Blocked; _target.BlockedBy = _blockers[_blockPick!.Value].Code;
                SaveItems($"#{_target.Code} blocked by #{_target.BlockedBy}"); _blockPick = null; break;
            case ConsoleKey.Escape: _blockPick = null; break;
        }
    }

    void HandleForm(ConsoleKeyInfo k)
    {
        var f = _form!;
        if (k.Key == ConsoleKey.Escape) { _form = null; return; }
        bool pasteCr = f.LastCR; f.LastCR = k.KeyChar == '\r';
        bool ctrlV = (k.Key == ConsoleKey.V && k.Modifiers.HasFlag(ConsoleModifiers.Control)) || (k.Key == ConsoleKey.Insert && k.Modifiers.HasFlag(ConsoleModifiers.Shift)) || k.KeyChar == (char)22;
        if (ctrlV)
        {
            var text = Clip.Get();
            if (f.Field != 2) text = text.Replace("\n", " ");
            f.Value += text; return;
        }
        if (k.KeyChar == '\n' && pasteCr) return;
        if (f.Field == 2 && k.Key == ConsoleKey.Tab && Console.KeyAvailable) { f.Notes += "    "; return; }
        if (k.Key == ConsoleKey.Enter && (k.Modifiers != 0 || k.KeyChar == '\n' || Console.KeyAvailable))
        {
            if (f.Field == 2) f.Notes += "\n";
            return;
        }
        if (k.Key == ConsoleKey.Enter || k.Key == ConsoleKey.F2 || k.KeyChar == (char)19)
        {
            if (string.IsNullOrWhiteSpace(f.Title)) { f.Error = "Title is required"; Notify("Title is required", false); return; }
            if (!f.TryDate(out var d)) { f.Error = "Invalid date (dd/MM/yyyy)"; Notify("Invalid date", false); return; }
            var t = f.Item;
            t.Title = f.Title.Trim(); t.Date = d; t.Notes = f.Notes;
            if (!f.Editing) { t.Priority = NextPriority(); t.Code = _items.Select(i => i.Code).DefaultIfEmpty(0).Max() + 1; _items.Add(t); }
            SaveItems(f.Editing ? $"Task #{t.Code} updated" : $"Task #{t.Code} created"); _form = null; return;
        }
        f.Error = "";
        if (k.Key == ConsoleKey.Tab) { f.Field = (f.Field + 1) % 3; return; }
        if (k.Key == ConsoleKey.UpArrow && f.Field > 0 && !(f.Field == 2 && f.Notes.Contains('\n'))) { f.Field--; return; }
        if (k.Key == ConsoleKey.Backspace)
        {
            if (f.Value.Length > 0) f.Value = f.Value[..^1];
            return;
        }
        if (!char.IsControl(k.KeyChar)) f.Value += k.KeyChar;
    }

    IRenderable Modal(string title, IEnumerable<string> lines, int sel, int width, string[] icons)
    {
        var rows = lines.Select((n, i) => new Markup(i == sel ? $"[bold {Accent}]> {icons[i]}  {Markup.Escape(n)}[/]" : $"  {icons[i]}  {Markup.Escape(n)}"));
        return new Panel(new Rows(rows)) { Header = H(title), Border = BoxBorder.Rounded, BorderStyle = Style.Parse(Accent), Width = width };
    }

    int? _help;

    static readonly string[] HelpTabs = ["Lists", "Tasks", "Details", "Forms & modals"];
    static readonly (string Title, (string Key, string Desc)[] Rows)[][] HelpPages =
    [
        [("Lists panel (left)", [("Up / k", "Previous list."), ("Down / j", "Next list (continues into My Lists)."), ("Enter / Right", "Open the task panel for the selected list."),
            ("n", "New task (in My Lists it is created inside that list)."), ("l", "New custom list (name + icon)."), ("x", "Delete the selected custom list; its tasks go back to My Day / Tasks."),
            ("/", "Search all tasks by #code or title."), ("Tab", "Move focus to the task panel."), ("o", "Show where the JSON data files are stored."), ("?", "Open this help."), ("q", "Quit.")])],
        [("Task panel (right)", [("Up / k", "Previous task."), ("Down / j", "Next task."), ("Enter", "Open task details (markdown preview)."), ("n", "New task."), ("e", "Edit the selected task."),
            ("space", "Change status (Not started, Doing, Need more details, Blocked, Completed, Cancelled)."), ("m", "Move the task to a custom list."), ("p", "Reorder: move the task up/down (My Day and custom lists), Enter to finish."), ("P", "Set the priority position by number."), ("c", "Open the month calendar with task counts and statuses."), ("i", "Toggle important."), ("d", "Delete the task."),
            ("/", "Search by #code or title."), ("o", "Show data file location."), ("Tab / Left / Esc", "Back to the lists panel.")])],
        [("Task details", [("Up / k", "Scroll up one line."), ("Down / j", "Scroll down one line."), ("PgUp / PgDn", "Scroll one page."), ("Home / End", "Jump to top / bottom."),
            ("e", "Edit the task."), ("space", "Change status."), ("m", "Move to a custom list."), ("o", "Show data file location."), ("Esc", "Back to the task list.")])],
        [("New / edit task", [("Tab", "Next field."), ("Enter", "Save."), ("Shift+Enter", "New line in About."), ("Ctrl+V", "Paste from the clipboard."), ("Esc", "Cancel.")]),
         ("Status, move and blocked-by modals", [("Up / Down", "Select an option."), ("Enter", "Confirm."), ("Esc", "Cancel.")])],
    ];

    void HandleHelp(ConsoleKeyInfo k)
    {
        switch (k.Key)
        {
            case ConsoleKey.Escape or ConsoleKey.Q: _help = null; break;
            case ConsoleKey.LeftArrow or ConsoleKey.H or ConsoleKey.UpArrow: _help = Math.Max(0, _help!.Value - 1); break;
            case ConsoleKey.RightArrow or ConsoleKey.L or ConsoleKey.DownArrow or ConsoleKey.Tab: _help = Math.Min(HelpTabs.Length - 1, _help!.Value + 1); break;
            default:
                if (k.KeyChar == '?') _help = null;
                else if (k.KeyChar is >= '1' and <= '4') _help = k.KeyChar - '1';
                break;
        }
    }

    IRenderable HelpModal()
    {
        var chips = HelpTabs.Select((t, i) => i == _help ? $"[bold {Accent}]{i + 1} {t}[/]" : $"[grey]{i + 1} {t}[/]");
        var body = new List<IRenderable> { new Markup(string.Join("  [grey]│[/]  ", chips)), new Text("") };
        bool first = true;
        foreach (var (title, rows) in HelpPages[_help!.Value])
        {
            if (!first) body.Add(new Text(""));
            first = false;
            body.Add(new Markup($"[bold #e8912d]{Markup.Escape(title)}[/]"));
            body.Add(new Rule().RuleStyle(Style.Parse("#6e7681")));
            var grid = new Grid().AddColumn(new GridColumn().Width(18).NoWrap()).AddColumn();
            foreach (var (key, desc) in rows)
                grid.AddRow(new Markup($"[#e8912d]{Markup.Escape(key)}[/]"), new Markup($"[grey]{Markup.Escape(desc)}[/]"));
            body.Add(grid);
        }
        body.Add(new Text(""));
        body.Add(new Markup("[grey]←→ / 1-4: switch tab  ·  Esc or ?: close[/]"));
        return new Panel(new Rows(body)) { Header = H("Help"), Border = BoxBorder.Rounded, BorderStyle = Style.Parse(Accent), Width = 78, Padding = new Padding(1, 0) };
    }

    IRenderable Build(int height)
    {
        var list = Current();
        _sel = Math.Clamp(_sel, 0, Math.Max(0, list.Count - 1));

        IRenderable MenuRow(int i)
        {
            var sel = i == _menu && _query == null;
            var name = Markup.Escape(MenuName(i));
            return new Markup($"{(sel ? $"[bold {Accent}]>[/]" : " ")} {MenuIcon(i)}  {(sel ? $"[bold {Accent}]{name}[/]" : name)} [grey]({Filter(i).Count()})[/]");
        }
        var menu = new Rows(Enumerable.Range(0, Menu.Length).Select(MenuRow));
        IRenderable custom = _lists.Count == 0
            ? new Markup("[grey]Press [bold]l[/] to create a list[/]")
            : new Rows(Enumerable.Range(Menu.Length, _lists.Count).Select(MenuRow));

        var rows = new List<IRenderable>();
        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            var sel = i == _sel && _focusRight;
            var title = Markup.Escape(it.Title);
            if (it.Status is Status.Done or Status.Cancelled) title = $"[strikethrough grey]{title}[/]";
            else if (sel) title = $"[bold green]{title}[/]";
            var star = Star(it.Important);
            var blocked = it.Status == Status.Blocked && it.BlockedBy != null ? $"  [grey]blocked by #{it.BlockedBy}[/]" : "";
            var rank = Ranked ? $"[grey]{i + 1,2}[/] " : "";
            var marker = sel && _reorder ? "[orange1]↕[/]" : sel ? "[orange1]>[/]" : " ";
            rows.Add(new Markup($"{marker} {rank}{Icon(it)}  [grey]#{it.Code}[/] {title}  {star}"));
            rows.Add(new Markup($"     {(Ranked ? "   " : "")}[grey]{it.Date:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(it.Notes) ? "" : "  \uf249 notes")}[/]{blocked}"));
        }
        if (rows.Count == 0) rows.Add(new Markup("[grey]No tasks. Press [bold]n[/] to create one.[/]"));

        IRenderable right = new Rows(rows);
        var title2 = _query != null ? $"Search: {_query}" : MenuName(_menu);
        if (_detail && list.Count > 0)
        {
            var it = list[_sel];
            title2 = $"#{it.Code} {it.Title}";
            var sname = it.Status == Status.Pending && it.Date.Date < DateTime.Today ? "Overdue" : StatusNames[Array.IndexOf(StatusValues, it.Status)];
            var by = it.Status == Status.Blocked && it.BlockedBy != null ? $"  ·  waiting for [bold]#{it.BlockedBy}[/]" : "";
            _view = new ScrollView(Md.Render(string.IsNullOrWhiteSpace(it.Notes) ? "*No notes.*" : it.Notes), Math.Max(3, height - 9), _scroll);
            var pct = _view.Total <= _view.Visible ? "" : $"  ·  {(int)(100.0 * (_scroll + _view.Visible) / Math.Max(1, _view.Total))}%";
            right = new Rows(
                new Markup($"{Icon(it)}  [bold]{Markup.Escape(it.Title)}[/]  {Star(it.Important)}"),
                new Markup($"[grey]#{it.Code}  ·  {it.Date:dd/MM/yyyy}  ·  {sname}{by}{pct}[/]"),
                new Text(""),
                _view);
        }
        if (_cal != null) right = Align.Center(CalModal(), VerticalAlignment.Middle);
        else if (_showPath) right = Align.Center(PathModal(), VerticalAlignment.Middle);
        else if (_help != null) right = Align.Center(HelpModal(), VerticalAlignment.Middle);
        else if (_form != null) right = Align.Center(_form.Render(), VerticalAlignment.Middle);
        else if (_lform != null) right = Align.Center(LFormPanel(), VerticalAlignment.Middle);
        else if (_movePick != null)
            right = Align.Center(Modal("Move to list", new[] { "None (My Day / Tasks)" }.Concat(_lists.Select(l => l.Name)), _movePick.Value, 44,
                new[] { "[grey]\uf522[/]" }.Concat(_lists.Select(l => $"[#8be9fd]{Markup.Escape(l.Icon)}[/]")).ToArray()), VerticalAlignment.Middle);
        else if (_picker != null)
            right = Align.Center(Modal("Status", StatusNames, _picker.Value, 36, StatusValues.Select(s => StatusIcon(s)).ToArray()), VerticalAlignment.Middle);
        else if (_prioInput != null)
            right = Align.Center(new Panel(new Markup($"Move to position (1-{list.Count}): [bold]{_prioInput}[/][blink]▏[/]")) { Header = H("Priority"), Border = BoxBorder.Rounded, BorderStyle = Style.Parse(Accent), Width = 44 }, VerticalAlignment.Middle);
        else if (_blockPick != null)
            right = Align.Center(Modal("Blocked by", _blockers.Select(b => $"#{b.Code} {b.Title}"), _blockPick.Value, 56, _blockers.Select(b => Icon(b)).ToArray()), VerticalAlignment.Middle);
        bool modal = _cal != null || _showPath || _prioInput != null || _help != null || _form != null || _picker != null || _blockPick != null || _lform != null || _movePick != null;

        var leftPanel = new Panel(menu) { Header = H("Lists"), Border = BoxBorder.Rounded, Expand = true, BorderStyle = _focusRight || modal || _menu >= Menu.Length ? Style.Parse("grey") : Style.Parse(Accent) };
        var customPanel = new Panel(custom) { Header = H("My Lists"), Border = BoxBorder.Rounded, Expand = true, BorderStyle = _focusRight || modal || _menu < Menu.Length ? Style.Parse("grey") : Style.Parse(Accent) };
        var rightPanel = new Panel(right) { Header = H(title2), Border = BoxBorder.Rounded, Expand = true, BorderStyle = _focusRight && !modal ? Style.Parse(Accent) : Style.Parse("grey") };

        var help = _cal != null ? "←→↑↓: day | PgUp/PgDn: month | t: today | Esc: close"
            : _form != null ? "Enter: save | Shift+Enter: new line (About) | Ctrl+V: paste | Tab: next field | Esc: cancel"
            : _lform != null ? "Enter: create | Tab: next field | Ctrl+V: paste | Esc: cancel"
            : _picker != null || _blockPick != null || _movePick != null ? "↑↓: select | Enter: confirm | Esc: cancel"
            : _prioInput != null ? "Type a position | Enter: confirm | Esc: cancel"
            : _reorder ? "↑↓: move task | Enter/p: done"
            : _searching ? $"/{_query}▏  (Enter: keep results | Esc: cancel)"
            : _detail ? "↑↓: scroll | e: edit | Esc: back | ?: help"
            : _focusRight ? "↑↓: select | Enter: open | n: new | c: calendar | space: status | ?: help"
            : "↑↓: select | Enter: open | n: new | l: new list | ?: help | q: quit";


        var layout = new Layout("root").SplitRows(
            new Layout("topbar", TopBar()).Size(3),
            new Layout("body").SplitColumns(new Layout("left").SplitRows(new Layout("lists", leftPanel).Size(Menu.Length + 2), new Layout("custom", customPanel)).Size(32), new Layout("right", rightPanel)),
            new Layout("footer", new Markup($"[grey]{Markup.Escape(help)}[/]")).Size(1));
        AnsiConsole.Profile.Height = height;
        return layout;
    }

    class Form
    {
        public TodoItem Item { get; }
        public bool Editing { get; }
        public string Title, Date, Notes;
        public int Field;
        public bool LastCR;
        public string Error = "";

        public Form(TodoItem item, bool editing = false)
        {
            Item = item; Editing = editing;
            Title = item.Title; Notes = item.Notes;
            Date = item.Date.ToString("dd/MM/yyyy");
        }

        public string Value
        {
            get => Field switch { 0 => Title, 1 => Date, _ => Notes };
            set { if (Field == 0) Title = value; else if (Field == 1) Date = value; else Notes = value; }
        }

        public bool TryDate(out DateTime d) =>
            DateTime.TryParseExact(Date.Trim(), new[] { "dd/MM/yyyy", "yyyy-MM-dd", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

        IRenderable Box(string label, string text, int idx, bool multi = false)
        {
            var active = Field == idx;
            var shown = Markup.Escape(text) + (active ? "[blink]▏[/]" : "");
            if (!multi) shown = shown.Replace("\n", " ");
            return new Panel(new Markup(shown.Length == 0 ? " " : shown))
            {
                Header = H(label),
                Border = BoxBorder.Rounded,
                Expand = true,
                BorderStyle = Style.Parse(active ? "orange1" : "grey"),
            };
        }

        public IRenderable Render()
        {
            var parts = new List<IRenderable>
            {
                Box("Title", Title, 0),
                Box("Date (dd/MM/yyyy)", Date, 1),
                Box("About (Markdown)", Notes, 2, true),
            };
            if (Error != "") parts.Add(new Markup($"[red]{Markup.Escape(Error)}[/]"));
            return new Panel(new Rows(parts))
            {
                Header = H(Editing ? $"\uf040 Edit task #{Item.Code}" : "\uf067 New task"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse(Accent),
                Width = 64,
            };
        }
    }
}
