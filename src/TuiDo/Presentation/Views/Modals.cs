using Spectre.Console;
using Spectre.Console.Rendering;
using TuiDo.Abstractions;
using TuiDo.Domain;
using TuiDo.Presentation.Forms;
using TuiDo.Services;

namespace TuiDo.Presentation.Views;

internal static class Modals
{
    public static IRenderable Picker(string title, IEnumerable<string> labels, int selected, int width, IReadOnlyList<string> icons)
    {
        var rows = labels.Select((label, i) => new Markup(i == selected
            ? $"[bold {Theme.Accent}]> {icons[i]}  {Markup.Escape(label)}[/]"
            : $"  {icons[i]}  {Markup.Escape(label)}"));
        return Frame(title, new Rows(rows), width);
    }

    public static IRenderable StatusPicker(int selected) =>
        Picker("Status", StatusCatalog.Ordered.Select(StatusCatalog.Name), selected, 36, StatusCatalog.Ordered.Select(Theme.StatusIcon).ToList());

    public static IRenderable BlockedBy(IReadOnlyList<TodoItem> candidates, int selected, DateTime today) =>
        Picker("Blocked by", candidates.Select(c => $"#{c.Code} {c.Title}"), selected, 56, candidates.Select(c => Theme.ItemIcon(c, today)).ToList());

    public static IRenderable MoveToList(IReadOnlyList<CustomList> lists, int selected) =>
        Picker("Move to list", new[] { "None (My Day / Tasks)" }.Concat(lists.Select(l => l.Name)), selected, 44,
            new[] { "[grey]\uf522[/]" }.Concat(lists.Select(Theme.ListIcon)).ToList());

    public static IRenderable Editors(IReadOnlyList<EditorInfo> editors, int selected) =>
        Picker("Edit About in", editors.Select(e => e.Name), selected, 34, editors.Select(e => $"[{Theme.Cyan}]{e.Icon}[/]").ToList());

    public static IRenderable Priority(string input, int count) =>
        Frame("Priority", new Markup($"Move to position (1-{count}): [bold]{input}[/][blink]▏[/]"), 44);

    public static IRenderable DataLocation(string itemsPath, string listsPath) =>
        Frame("\uf07b Data location", new Rows(
            new Markup("[grey]Your task lists are stored as JSON here:[/]"),
            new Text(""),
            new Markup($"[bold {Theme.Orange}]Tasks[/]  {Markup.Escape(itemsPath)}"),
            new Markup($"[bold {Theme.Orange}]Lists[/]  {Markup.Escape(listsPath)}"),
            new Text(""),
            new Markup("[grey]Enter: open folder  ·  Esc/o: close[/]")), width: null);

    public static IRenderable Help(int tab)
    {
        var chips = HelpContent.Tabs.Select((t, i) => i == tab ? $"[bold {Theme.Accent}]{i + 1} {t}[/]" : $"[grey]{i + 1} {t}[/]");
        var body = new List<IRenderable> { new Markup(string.Join("  [grey]│[/]  ", chips)), new Text("") };

        var first = true;
        foreach (var section in HelpContent.Page(tab))
        {
            if (!first) body.Add(new Text(""));
            first = false;
            body.Add(new Markup($"[bold {Theme.Orange}]{Markup.Escape(section.Title)}[/]"));
            body.Add(new Rule().RuleStyle(Style.Parse(Theme.Muted)));
            var grid = new Grid().AddColumn(new GridColumn().Width(18).NoWrap()).AddColumn();
            foreach (var entry in section.Entries)
                grid.AddRow(new Markup($"[{Theme.Orange}]{Markup.Escape(entry.Key)}[/]"), new Markup($"[grey]{Markup.Escape(entry.Description)}[/]"));
            body.Add(grid);
        }

        body.Add(new Text(""));
        body.Add(new Markup("[grey]←→ / 1-4: switch tab  ·  Esc or ?: close[/]"));
        return Frame("Help", new Rows(body), 78);
    }

    public static IRenderable Calendar(TodoService service, DateTime selected, DateTime today)
    {
        var first = new DateTime(selected.Year, selected.Month, 1);
        var days = DateTime.DaysInMonth(selected.Year, selected.Month);

        var table = new Table { Border = TableBorder.Rounded, BorderStyle = Style.Parse(Theme.Muted) }.ShowRowSeparators();
        foreach (var weekday in new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" })
            table.AddColumn(new TableColumn($"[bold {Theme.Orange}]{weekday}[/]") { Width = 9, NoWrap = true });

        var cells = new List<IRenderable>();
        for (var i = 0; i < (int)first.DayOfWeek; i++) cells.Add(new Text(" "));
        for (var day = 1; day <= days; day++)
        {
            var date = new DateTime(selected.Year, selected.Month, day);
            var tasks = service.OnDate(date).ToList();
            var number = date == selected.Date ? $"[black on {Theme.Accent}] {day,2} [/]"
                : date == today.Date ? $"[bold green] {day,2} [/]"
                : $"[grey] {day,2} [/]";
            var count = tasks.Count > 0 ? $" [bold]({tasks.Count})[/]" : "";
            var icons = tasks.Count == 0 ? " " : string.Concat(tasks.Take(5).Select(t => Theme.ItemIcon(t, today))) + (tasks.Count > 5 ? "[grey]+[/]" : "");
            cells.Add(new Markup($"{number}{count}\n{icons}"));
        }

        while (cells.Count % 7 != 0) cells.Add(new Text(" "));
        for (var r = 0; r < cells.Count; r += 7) table.AddRow(cells.Skip(r).Take(7));

        var dayTasks = service.OnDate(selected).ToList();
        IRenderable agenda = dayTasks.Count == 0
            ? new Markup("[grey]No tasks this day.[/]")
            : new Rows(dayTasks.Take(5).Select(t => new Markup(
                $"{Theme.ItemIcon(t, today)}  [grey]#{t.Code}[/] {Markup.Escape(t.Title)}  [grey]{StatusCatalog.Name(t.Status)}[/]")));

        return Frame("\uf073 Calendar", new Rows(
            new Markup($"[bold]{selected:MMMM yyyy}[/]  [grey]·  {selected:dd/MM/yyyy}[/]"),
            table,
            new Markup($"[bold {Theme.Orange}]Tasks on {selected:dd/MM}[/]"),
            agenda,
            new Markup("[grey]←→↑↓: day  ·  PgUp/PgDn: month  ·  t: today  ·  Esc/c: close[/]")), width: null);
    }

    public static IRenderable TaskEditor(TaskForm form) =>
        Frame(form.Editing ? $"\uf040 Edit task #{form.Existing!.Code}" : "\uf067 New task", new Rows(FormParts(form)), 64);

    public static IRenderable NewList(ListForm form)
    {
        var parts = new List<IRenderable>
        {
            Field("Name", form.Name, form.Field == ListForm.NameField),
            Field("Icon (e.g. f522, \\uf522 or paste the glyph)", form.Icon, form.Field == ListForm.IconField),
            new Markup($"Preview: [{Theme.Cyan}]{Markup.Escape(IconParser.Parse(form.Icon))}[/]  {Markup.Escape(form.Name)}"),
        };
        if (form.Error != "") parts.Add(new Markup($"[red]{Markup.Escape(form.Error)}[/]"));
        return Frame("\uf067 New list", new Rows(parts), 64);
    }

    private static List<IRenderable> FormParts(TaskForm form)
    {
        var parts = new List<IRenderable>
        {
            Field("Title", form.Title, form.Field == TaskForm.TitleField),
            Field("Date (dd/MM/yyyy)", form.Date, form.Field == TaskForm.DateField),
            Field("About (Markdown)", form.Notes, form.Field == TaskForm.NotesField, multiline: true),
        };
        if (form.Error != "") parts.Add(new Markup($"[red]{Markup.Escape(form.Error)}[/]"));
        return parts;
    }

    private static IRenderable Field(string label, string text, bool active, bool multiline = false)
    {
        var shown = Markup.Escape(text) + (active ? "[blink]▏[/]" : "");
        if (!multiline) shown = shown.Replace("\n", " ");
        return new Panel(new Markup(shown.Length == 0 ? " " : shown))
        {
            Header = Theme.Header(label),
            Border = BoxBorder.Rounded,
            Expand = true,
            BorderStyle = Style.Parse(active ? "orange1" : "grey"),
        };
    }

    private static Panel Frame(string title, IRenderable content, int? width) => new(content)
    {
        Header = Theme.Header(title),
        Border = BoxBorder.Rounded,
        BorderStyle = Style.Parse(Theme.Accent),
        Padding = new Padding(1, 0),
        Width = width,
    };
}
