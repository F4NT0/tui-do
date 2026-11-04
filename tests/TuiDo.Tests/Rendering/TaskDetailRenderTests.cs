using TuiDo.Domain;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class TaskDetailRenderTests
{
    private static TestApp Open(string notes, Action<TodoItem>? tweak = null)
    {
        var app = TestApp.With(a =>
        {
            var task = a.AddTask("Write docs", notes: notes, code: 7);
            tweak?.Invoke(task);
        });
        return app.FocusTasks().Enter();
    }

    [Fact]
    public void Enter_replaces_the_task_list_with_the_selected_task_details()
    {
        var screen = Open("hello").Screen();

        Assert.Contains("#7 Write docs", screen);
        Assert.Contains("#7  ·  03/11/2026  ·  Not started", screen);
        Assert.Contains("hello", screen);
        Assert.Contains("↑↓: scroll | e: edit | Esc: back | ?: help", screen);
        Assert.DoesNotContain("No tasks", screen);
    }

    [Fact]
    public void Esc_returns_to_the_task_list_of_the_same_set()
    {
        var app = Open("hello").Esc();

        var screen = app.Screen();

        Assert.Contains("My Day", screen.Split('\n')[3]);
        Assert.Contains("#7 Write docs", screen);
        Assert.DoesNotContain("Not started", screen);
    }

    [Fact]
    public void Empty_notes_say_so()
    {
        Assert.Contains("No notes.", Open("").Screen());
    }

    [Fact]
    public void Overdue_and_blocked_details_are_spelled_out()
    {
        var overdue = Open("x", t => t.Date = new DateTime(2026, 11, 1)).Screen();
        Assert.Contains("Overdue", overdue);
        Assert.Contains(Glyph.Overdue, overdue);

        var blocked = Open("x", t => { t.Status = Status.Blocked; t.BlockedBy = 3; }).Screen();
        Assert.Contains("Blocked", blocked);
        Assert.Contains("waiting for #3", blocked);
    }

    [Fact]
    public void Headings_are_rendered_without_their_hash_marks()
    {
        var screen = Open("# Title one\n\n## Title two\n\n### Title three").Screen();

        Assert.Contains("Title one", screen);
        Assert.Contains("Title two", screen);
        Assert.Contains("Title three", screen);
        Assert.DoesNotContain("# Title one", screen);
        Assert.DoesNotContain("## Title two", screen);
    }

    [Fact]
    public void Inline_formatting_marks_are_consumed()
    {
        var screen = Open("some **bold** and *italic* and `code` here").Screen();

        Assert.Contains("some bold and italic and  code  here", screen);
        Assert.DoesNotContain("**", screen);
        Assert.DoesNotContain("`", screen);
    }

    [Fact]
    public void Lists_use_bullets_and_keep_numbers()
    {
        var screen = Open("- apples\n- pears\n\n1. first\n2. second").Screen();

        Assert.Contains("• apples", screen);
        Assert.Contains("• pears", screen);
        Assert.Contains("1. first", screen);
        Assert.Contains("2. second", screen);
    }

    [Fact]
    public void Task_checkboxes_become_status_icons()
    {
        var screen = Open("- [ ] todo item\n- [x] done item").Screen();

        Assert.Contains($"{Glyph.NotStarted} todo item", screen);
        Assert.Contains($"{Glyph.Completed} done item", screen);
    }

    [Fact]
    public void Icon_shortcodes_and_status_keywords_are_replaced()
    {
        var screen = Open(":check: ok :x: no :work: wip TODO DOING DONE FAIL").Screen();

        Assert.Contains($"{Glyph.Completed} ok", screen);
        Assert.Contains($"{Glyph.Cancelled} no", screen);
        Assert.Contains($"{Glyph.Overdue} wip", screen);
        Assert.DoesNotContain(":check:", screen);
        Assert.Contains("TODO DOING DONE FAIL", screen);
    }

    [Fact]
    public void Quotes_and_rules_are_drawn()
    {
        var screen = Open("> quoted text\n\n---\n\nafter").Screen();

        Assert.Contains("│ quoted text", screen);
        Assert.Contains("────────", screen);
        Assert.Contains("after", screen);
    }

    [Fact]
    public void Superscript_math_drops_the_caret()
    {
        Assert.Contains("x2", Open("area is x^2 here").Screen());
    }

    [Fact]
    public void Markdown_tables_are_drawn_as_a_grid_with_header_and_cells()
    {
        var screen = Open("| Name | Qty |\n|---|---|\n| apple | 3 |\n| pear | 5 |").Screen();

        Assert.Contains("Name", screen);
        Assert.Contains("apple", screen);
        Assert.Contains("pear", screen);
        Assert.Contains("┌", screen);
        Assert.Contains("┼", screen.Replace("╋", "┼").Replace("╪", "┼").Replace("╬", "┼"));
        Assert.DoesNotContain("|---|", screen);
    }

    [Fact]
    public void Code_blocks_show_language_label_line_numbers_and_stay_snug()
    {
        var screen = Open("```bash\nnpm install\nnpm run build\n```").Screen();

        Assert.Contains("bash", screen);
        Assert.Contains("1  npm install", screen);
        Assert.Contains("2  npm run build", screen);
        Assert.DoesNotContain("```", screen);

        var boxLine = screen.Split('\n').First(l => l.Contains("1  npm install"));
        Assert.True(boxLine.Count(c => c == '│') >= 3, "the code box must be narrower than the panel");
    }

    [Fact]
    public void Long_notes_show_a_percentage_and_scroll_with_the_keyboard()
    {
        var notes = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"paragraph {i:00}"));
        var app = Open(notes);

        var top = app.Screen();
        Assert.Contains("paragraph 01", top);
        Assert.DoesNotContain("paragraph 60", top);
        Assert.Matches(@"\d+%", top);

        app.Press(ConsoleKey.End);
        var bottom = app.Screen();
        Assert.Contains("paragraph 60", bottom);
        Assert.DoesNotContain("paragraph 01", bottom);
        Assert.Contains("100%", bottom);

        app.Press(ConsoleKey.Home);
        Assert.Contains("paragraph 01", app.Screen());
    }

    [Fact]
    public void Arrow_keys_scroll_a_line_at_a_time_and_page_keys_a_page()
    {
        var notes = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"paragraph {i:00}"));
        var app = Open(notes);

        app.Down(2);
        var scrolled = app.Screen();
        Assert.DoesNotContain("paragraph 01", scrolled);
        Assert.Contains("paragraph 02", scrolled);

        app.Press(ConsoleKey.PageDown);
        Assert.DoesNotContain("paragraph 02", app.Screen());

        app.Press(ConsoleKey.PageUp).Up(5);
        Assert.Contains("paragraph 01", app.Screen());
    }

    [Fact]
    public void Short_notes_do_not_show_a_percentage()
    {
        Assert.DoesNotMatch(@"\d+%", Open("short").Screen());
    }

    [Fact]
    public void E_in_details_opens_the_edit_form_and_space_the_status_modal()
    {
        var app = Open("x").Press('e');
        Assert.Contains("Edit task #7", app.Screen());
        app.Esc();

        app.Press(' ');
        Assert.Contains("Not started", app.Screen());
    }
}
