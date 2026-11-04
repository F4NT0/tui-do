using Spectre.Console;
using Spectre.Console.Rendering;
using TuiDo.Services;

namespace TuiDo.Presentation.Views;

internal static class SidebarView
{
    public const int Width = 32;
    public const int ListsPanelHeight = BuiltInLists.Count + 2;

    public static Panel Lists(AppState state, TodoService service, bool modal) => new(new Rows(
        Enumerable.Range(0, BuiltInLists.Count).Select(i => Row(state, service, i))))
    {
        Header = Theme.Header("Lists"),
        Border = BoxBorder.Rounded,
        Expand = true,
        BorderStyle = Style.Parse(state.FocusRight || modal || service.IsCustom(state.Menu) ? "grey" : Theme.Accent),
    };

    public static Panel Custom(AppState state, TodoService service, bool modal)
    {
        IRenderable content = service.Lists.Count == 0
            ? new Markup("[grey]Press [bold]l[/] to create a list[/]")
            : new Rows(Enumerable.Range(BuiltInLists.Count, service.Lists.Count).Select(i => Row(state, service, i)));

        return new Panel(content)
        {
            Header = Theme.Header("My Lists"),
            Border = BoxBorder.Rounded,
            Expand = true,
            BorderStyle = Style.Parse(state.FocusRight || modal || !service.IsCustom(state.Menu) ? "grey" : Theme.Accent),
        };
    }

    private static IRenderable Row(AppState state, TodoService service, int index)
    {
        var selected = index == state.Menu && state.Query == null;
        var name = Markup.Escape(service.MenuName(index));
        var icon = service.IsCustom(index) ? Theme.ListIcon(service.CustomAt(index)) : Theme.MenuIcons[index];
        var marker = selected ? $"[bold {Theme.Accent}]>[/]" : " ";
        var label = selected ? $"[bold {Theme.Accent}]{name}[/]" : name;
        return new Markup($"{marker} {icon}  {label} [grey]({service.MenuCountOf(index)})[/]");
    }
}
