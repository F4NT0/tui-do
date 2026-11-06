using Spectre.Console;
using TuiDo.Abstractions;
using TuiDo.Domain;
using TuiDo.Presentation;

namespace TuiDo.Cli;

internal static class TodayCommand
{
    public static int Run(IAnsiConsole console, ITodoRepository repository, IClock clock)
    {
        var today = clock.Today;
        var lists = repository.LoadLists();
        var items = repository.LoadItems().Where(i => i.Date.Date == today).OrderBy(i => i.Time ?? TimeSpan.Zero).ThenBy(i => i.Code).ToList();

        console.MarkupLine($"[bold {Theme.Accent}]Today[/] [grey]{today:dd/MM/yyyy}[/]");
        if (items.Count == 0)
        {
            console.MarkupLine("[grey]No tasks for today.[/]");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded).BorderStyle(Style.Parse(Theme.Muted))
            .AddColumn(new TableColumn("[bold]Code[/]").RightAligned().NoWrap())
            .AddColumn("[bold]Task[/]")
            .AddColumn(new TableColumn("[bold]Status[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]List[/]").NoWrap());
        foreach (var item in items)
        {
            var color = Theme.StatusColor(item.Status);
            table.AddRow($"[grey]#{item.Code}[/]", Markup.Escape(item.Title),
                $"[{color}]{Theme.StatusGlyph(item.Status)} {Markup.Escape(StatusCatalog.Name(item.Status))}[/]",
                lists.FirstOrDefault(l => l.Id == item.ListId) is { } list
                    ? $"[{Theme.Cyan}]{Markup.Escape(list.Icon)}[/] {Markup.Escape(list.Name)}"
                    : $"{Theme.MenuIcons[0]} My Day");
        }
        console.Write(table);
        return 0;
    }
}
