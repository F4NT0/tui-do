using TuiDo.Domain;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class HelpRenderTests
{
    [Fact]
    public void Question_mark_opens_a_floating_help_with_numbered_tabs()
    {
        var screen = new TestApp().Press('?').Screen();

        Assert.Contains("Help", screen);
        foreach (var tab in new[] { "1 Lists", "2 Tasks", "3 Details", "4 Forms & modals" })
            Assert.Contains(tab, screen);
        Assert.Contains("Esc or ?: close", screen);
        Assert.Contains("My Day (0)", screen);
    }

    [Fact]
    public void Help_opens_on_the_tab_of_the_panel_that_has_focus()
    {
        Assert.Contains("Lists panel (left)", new TestApp().Press('?').Screen());

        var tasks = TestApp.With(a => a.AddTask("x")).FocusTasks().Press('?').Screen();
        Assert.Contains("Task panel (right)", tasks);

        var detail = TestApp.With(a => a.AddTask("x")).FocusTasks().Enter().Press('?').Screen();
        Assert.Contains("Task details", detail);
    }

    [Fact]
    public void Arrow_keys_and_digits_switch_tabs()
    {
        var app = new TestApp().Press('?').Press(ConsoleKey.RightArrow);
        Assert.Contains("Task panel (right)", app.Screen());

        app.Press('4');
        Assert.Contains("New / edit task", app.Screen());
        Assert.Contains("Status, move and blocked-by modals", app.Screen());

        app.Press(ConsoleKey.LeftArrow).Press(ConsoleKey.LeftArrow).Press(ConsoleKey.LeftArrow).Press(ConsoleKey.LeftArrow);
        Assert.Contains("Lists panel (left)", app.Screen());
    }

    [Fact]
    public void Every_tab_documents_its_commands()
    {
        var app = new TestApp().Press('?');
        Assert.Contains("Delete the selected custom list", app.Screen());

        app.Press('2');
        var tasks = app.Screen();
        foreach (var text in new[] { "Change status", "Move the task to a custom list", "Reorder", "priority position", "calendar", "Toggle important" })
            Assert.Contains(text, tasks);

        app.Press('3');
        Assert.Contains("Scroll one page", app.Screen());

        app.Press('4');
        Assert.Contains("Paste from the clipboard", app.Screen());
        Assert.Contains("Neovim, Vim or VS Code", app.Screen());
    }

    [Fact]
    public void Esc_question_mark_and_q_close_it()
    {
        foreach (var close in new Action<TestApp>[] { a => a.Esc(), a => a.Press('?'), a => a.Press('q') })
        {
            var app = new TestApp().Press('?');
            close(app);

            Assert.DoesNotContain("Esc or ?: close", app.Screen());
            Assert.False(app.App.Quit);
        }
    }
}

public class CalendarRenderTests
{
    private static TestApp App() => TestApp.With(a =>
    {
        a.AddTask("today one", code: 1);
        a.AddTask("today two", code: 2, status: Status.Done);
        a.AddTask("tomorrow", code: 3, date: a.Today.AddDays(1));
        a.AddTask("late", code: 4, date: a.Today.AddDays(-1));
    });

    [Fact]
    public void C_in_the_task_panel_opens_the_month_calendar()
    {
        var screen = App().FocusTasks().Press('c').Screen();

        Assert.Contains("Calendar", screen);
        Assert.Contains("November 2026", screen);
        foreach (var day in new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" })
            Assert.Contains(day, screen);
        Assert.Contains("←→↑↓: day | PgUp/PgDn: month | t: today | Esc: close", screen);
    }

    [Fact]
    public void C_does_nothing_while_the_lists_panel_has_focus()
    {
        Assert.DoesNotContain("Calendar", App().Press('c').Screen());
    }

    [Fact]
    public void Days_show_how_many_tasks_they_have_and_their_status_icons()
    {
        var screen = App().FocusTasks().Press('c').Screen();

        Assert.Contains("3 (2)", screen.Replace("  ", " "));
        Assert.Contains($"{Glyph.NotStarted}{Glyph.Completed}", screen);
        Assert.Contains("(1)", screen);
        Assert.Contains(Glyph.Overdue, screen);
    }

    [Fact]
    public void Lists_the_tasks_of_the_selected_day_below_the_grid()
    {
        var app = App().FocusTasks().Press('c');
        var today = app.Screen();

        Assert.Contains("Tasks on 03/11", today);
        Assert.Contains("#1 today one", today);
        Assert.Contains("#2 today two", today);
        Assert.Contains("Completed", today.Split("Tasks on 03/11")[1]);

        app.Press(ConsoleKey.RightArrow);
        var tomorrow = app.Screen();
        Assert.Contains("Tasks on 04/11", tomorrow);
        Assert.Contains("#3 tomorrow", tomorrow.Split("Tasks on 04/11")[1]);
    }

    [Fact]
    public void Days_without_tasks_say_so()
    {
        var app = App().FocusTasks().Press('c').Press(ConsoleKey.DownArrow);

        Assert.Contains("No tasks this day.", app.Screen());
    }

    [Fact]
    public void Arrow_keys_move_by_day_and_week_and_page_keys_by_month()
    {
        var app = App().FocusTasks().Press('c');

        app.Press(ConsoleKey.LeftArrow);
        Assert.Contains("02/11/2026", app.Screen());

        app.Press(ConsoleKey.UpArrow);
        Assert.Contains("26/10/2026", app.Screen());
        Assert.Contains("October 2026", app.Screen());

        app.Press(ConsoleKey.PageDown).Press(ConsoleKey.PageDown);
        Assert.Contains("December 2026", app.Screen());

        app.Press('t');
        Assert.Contains("November 2026", app.Screen());
        Assert.Contains("Tasks on 03/11", app.Screen());
    }

    [Fact]
    public void Esc_and_c_close_the_calendar()
    {
        var app = App().FocusTasks().Press('c').Esc();
        Assert.DoesNotContain("Calendar", app.Screen());

        app.Press('c').Press('c');
        Assert.DoesNotContain("Calendar", app.Screen());
    }

    [Fact]
    public void Tasks_from_any_list_are_counted()
    {
        var app = TestApp.With(a =>
        {
            var work = a.AddList("Work");
            a.AddTask("in list", listId: work.Id, code: 1);
        });
        app.FocusTasks().Press('c');

        Assert.Contains("#1 in list", app.Screen());
    }

    [Fact]
    public void Many_tasks_on_one_day_are_summarised_with_a_plus()
    {
        var app = TestApp.With(a =>
        {
            for (var i = 1; i <= 7; i++) a.AddTask($"t{i}", code: i);
        });
        app.FocusTasks().Press('c');

        var screen = app.Screen();
        Assert.Contains("(7)", screen);
        Assert.Contains("+", screen);
    }
}

public class DataLocationRenderTests
{
    [Fact]
    public void O_shows_where_the_json_files_live()
    {
        var screen = new TestApp().Press('o').Screen();

        Assert.Contains("Data location", screen);
        Assert.Contains(@"C:\data\tui-do\tasks.json", screen);
        Assert.Contains(@"C:\data\tui-do\lists.json", screen);
        Assert.Contains("Enter: open folder", screen);
    }

    [Fact]
    public void Works_from_the_task_panel_and_the_detail_view()
    {
        var app = TestApp.With(a => a.AddTask("x"));
        Assert.Contains("Data location", app.FocusTasks().Press('o').Screen());
        app.Esc().Enter();
        Assert.Contains("Data location", app.Press('o').Screen());
    }

    [Fact]
    public void Enter_opens_the_folder_and_notifies()
    {
        var app = new TestApp().Press('o').Enter();

        Assert.Equal([@"C:\data\tui-do"], app.Folders.Opened);
        var screen = app.Screen();
        Assert.Contains("Opened data folder", screen);
        Assert.DoesNotContain("Data location", screen);
    }

    [Fact]
    public void Failure_to_open_the_folder_is_reported_as_an_error()
    {
        var app = new TestApp();
        app.Folders.Failure = new IOException("explorer missing");
        app.Press('o').Enter();

        Assert.Contains($"{Glyph.Cancelled} Could not open folder: explorer missing", app.Screen());
    }

    [Fact]
    public void Esc_and_o_close_it_without_opening_anything()
    {
        var app = new TestApp().Press('o').Esc();
        Assert.DoesNotContain("Data location", app.Screen());

        app.Press('o').Press('o');
        Assert.DoesNotContain("Data location", app.Screen());
        Assert.Empty(app.Folders.Opened);
    }
}

public class FooterRenderTests
{
    private static string Footer(string screen) => screen.TrimEnd('\n').Split('\n')[^1];

    [Fact]
    public void Lists_panel_footer_only_has_the_essentials()
    {
        var footer = Footer(new TestApp().Screen());

        Assert.Equal("↑↓: select | Enter: open | n: new | l: new list | ?: help | q: quit", footer.Trim());
    }

    [Fact]
    public void Task_panel_footer_changes_with_focus()
    {
        var footer = Footer(TestApp.With(a => a.AddTask("x")).FocusTasks().Screen());

        Assert.Equal("↑↓: select | Enter: open | n: new | c: calendar | space: status | ?: help", footer.Trim());
    }

    [Fact]
    public void Detail_footer_lists_scrolling_and_back()
    {
        var footer = Footer(TestApp.With(a => a.AddTask("x")).FocusTasks().Enter().Screen());

        Assert.Equal("↑↓: scroll | e: edit | Esc: back | ?: help", footer.Trim());
    }

    [Fact]
    public void Each_modal_has_its_own_footer()
    {
        Assert.Contains("Ctrl+V: paste", Footer(new TestApp().Press('n').Screen()));
        Assert.Contains("Enter: create", Footer(new TestApp().Press('l').Screen()));
        Assert.Contains("↑↓: select | Enter: confirm", Footer(TestApp.With(a => a.AddTask("x")).FocusTasks().Press(' ').Screen()));
        Assert.Contains("Type a position", Footer(TestApp.With(a => a.AddTask("x")).FocusTasks().Press('P').Screen()));
        Assert.Contains("↑↓: move task", Footer(TestApp.With(a => a.AddTask("x")).FocusTasks().Press('p').Screen()));
        Assert.Contains("switch tab", Footer(new TestApp().Press((char)63).Screen()));
        Assert.Contains("Enter: open folder", Footer(new TestApp().Press((char)111).Screen()));
    }

    [Fact]
    public void Footer_never_shows_the_commands_of_other_panels()
    {
        var lists = Footer(new TestApp().Screen());

        Assert.DoesNotContain("calendar", lists);
        Assert.DoesNotContain("space: status", lists);
    }
}
