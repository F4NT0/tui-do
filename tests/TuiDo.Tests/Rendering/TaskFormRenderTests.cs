using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class TaskFormRenderTests
{
    [Fact]
    public void N_opens_a_floating_new_task_form_with_title_date_and_about_fields()
    {
        var app = new TestApp().Press('n');

        var screen = app.Screen();

        Assert.Contains("New task", screen);
        Assert.Contains("Title", screen);
        Assert.Contains("Date (dd/MM/yyyy)", screen);
        Assert.Contains("About (Markdown)", screen);
        Assert.Contains("03/11/2026", screen);
        Assert.Contains("Enter: save", screen);
        Assert.Contains("Ctrl+E: open About in editor", screen);
    }

    [Fact]
    public void Form_uses_a_single_line_rounded_border_instead_of_a_double_one()
    {
        var screen = new TestApp().Press('n').Screen();

        Assert.DoesNotContain("╔", screen);
        Assert.DoesNotContain("═", screen);
    }

    [Fact]
    public void Typed_text_appears_in_the_active_field_and_tab_moves_to_the_next_one()
    {
        var app = new TestApp().Press('n').Type("Buy milk");
        Assert.Contains("Buy milk", app.Screen());

        app.Tab();
        for (var i = 0; i < 4; i++) app.Press(ConsoleKey.Backspace);
        app.Type("2027");
        Assert.Contains("03/11/2027", app.Screen());

        app.Tab().Type("some notes");
        Assert.Contains("some notes", app.Screen());
    }

    [Fact]
    public void Enter_saves_the_task_closes_the_form_and_lists_the_new_task()
    {
        var app = new TestApp().Press('n').Type("Buy milk").Enter();

        var screen = app.Screen();

        Assert.DoesNotContain("New task", screen);
        Assert.Contains("#1 Buy milk", screen);
        Assert.Contains("Task #1 created", screen);
        Assert.Single(app.Repository.Items);
    }

    [Fact]
    public void Empty_title_keeps_the_form_open_and_reports_the_error_inline_and_in_the_top_bar()
    {
        var app = new TestApp().Press('n').Enter();

        var screen = app.Screen();

        Assert.Contains("New task", screen);
        Assert.Equal(2, CountOccurrences(screen, "Title is required"));
        Assert.Contains(Glyph.Cancelled, screen);
        Assert.Empty(app.Repository.Items);
    }

    [Fact]
    public void Invalid_date_is_rejected_with_the_expected_format()
    {
        var app = new TestApp().Press('n').Type("x").Tab();
        for (var i = 0; i < 10; i++) app.Press(ConsoleKey.Backspace);
        app.Type("31-31-2026").Enter();

        var screen = app.Screen();

        Assert.Contains("Invalid date (dd/MM/yyyy)", screen);
        Assert.Contains("Invalid date", screen);
        Assert.Empty(app.Repository.Items);
    }

    [Fact]
    public void Edit_form_shows_the_task_code_and_current_values()
    {
        var app = TestApp.With(a => a.AddTask("Existing", date: new DateTime(2026, 12, 25), notes: "old notes", code: 4));
        app.Down().FocusTasks().Press('e');

        var screen = app.Screen();

        Assert.Contains("Edit task #4", screen);
        Assert.Contains("Existing", screen);
        Assert.Contains("25/12/2026", screen);
        Assert.Contains("old notes", screen);
    }

    [Fact]
    public void Editing_updates_the_task_and_notifies()
    {
        var app = TestApp.With(a => a.AddTask("Existing", code: 4));
        app.FocusTasks().Press('e').Type(" v2").Enter();

        var screen = app.Screen();

        Assert.Contains("Existing v2", screen);
        Assert.Contains("Task #4 updated", screen);
    }

    [Fact]
    public void Esc_cancels_without_saving()
    {
        var app = new TestApp().Press('n').Type("draft").Esc();

        Assert.DoesNotContain("New task", app.Screen());
        Assert.Empty(app.Repository.Items);
    }

    [Fact]
    public void New_task_started_inside_a_custom_list_belongs_to_that_list()
    {
        var app = TestApp.With(a => a.AddList("Work"));
        app.Down(5).Press('n').Type("report").Enter();

        var screen = app.Screen();

        Assert.Contains("report", screen);
        Assert.Contains("Work (1)", screen);
        Assert.Contains("My Day (0)", screen);
    }

    [Fact]
    public void Ctrl_V_pastes_the_clipboard_keeping_line_breaks_in_about()
    {
        var app = new TestApp();
        app.Clipboard.Text = "# Heading\nsecond line";
        app.Press('n').Tab().Tab().Ctrl('v');

        var screen = app.Screen();

        Assert.Contains("# Heading", screen);
        Assert.Contains("second line", screen);
    }

    [Fact]
    public void Ctrl_V_in_a_single_line_field_flattens_line_breaks()
    {
        var app = new TestApp();
        app.Clipboard.Text = "one\ntwo";
        app.Press('n').Ctrl('v');

        Assert.Contains("one two", app.Screen());
    }

    [Fact]
    public void Shift_Enter_adds_a_new_line_in_about_instead_of_saving()
    {
        var app = new TestApp().Press('n').Type("t").Tab().Tab().Type("line1").Press(ConsoleKey.Enter, ConsoleModifiers.Shift).Type("line2");

        var screen = app.Screen();

        Assert.Contains("New task", screen);
        Assert.Contains("line1", screen);
        Assert.Contains("line2", screen);
        Assert.Empty(app.Repository.Items);
    }

    [Fact]
    public void Enter_that_arrives_while_more_keys_are_queued_is_pasted_text_not_a_save()
    {
        var app = new TestApp().Press('n').Type("t").Tab().Tab().Type("pasted");
        app.Keys.Push(new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false));

        app.Enter().Type("more");

        var screen = app.Screen();
        Assert.Contains("New task", screen);
        Assert.Contains("pasted", screen);
        Assert.Contains("more", screen);
        Assert.Empty(app.Repository.Items);
    }

    [Fact]
    public void Ctrl_E_in_about_opens_the_editor_chooser_but_not_in_other_fields()
    {
        var app = new TestApp().Press('n').Ctrl('e');
        Assert.DoesNotContain("Edit About in", app.Screen());

        app.Tab().Tab().Ctrl('e');

        Assert.Contains("Edit About in", app.Screen());
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
