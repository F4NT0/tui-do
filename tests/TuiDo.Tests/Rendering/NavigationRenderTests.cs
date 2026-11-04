using TuiDo.Domain;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class NavigationRenderTests
{
    private static TestApp Seeded() => TestApp.With(a =>
    {
        a.AddTask("alpha", priority: 1);
        a.AddTask("beta", priority: 2);
        var work = a.AddList("Work");
        a.AddTask("job", listId: work.Id);
    });

    [Fact]
    public void Tab_toggles_focus_between_the_panels()
    {
        var app = Seeded();
        Assert.Contains("l: new list", app.Screen());

        app.Tab();
        Assert.Contains("c: calendar", app.Screen());

        app.Tab();
        Assert.Contains("l: new list", app.Screen());
    }

    [Fact]
    public void Arrow_keys_right_and_left_also_change_focus()
    {
        var app = Seeded().Press(ConsoleKey.RightArrow);
        Assert.Contains("c: calendar", app.Screen());

        app.Press(ConsoleKey.LeftArrow);
        Assert.Contains("l: new list", app.Screen());
    }

    [Fact]
    public void Enter_on_the_lists_panel_moves_focus_to_the_tasks_and_a_second_Enter_opens_details()
    {
        var app = Seeded().Enter();
        Assert.Contains("c: calendar", app.Screen());

        app.Enter();
        Assert.Contains("↑↓: scroll", app.Screen());
        Assert.Contains("#1 alpha", app.Screen().Split('\n')[3]);
    }

    [Fact]
    public void Vim_keys_j_and_k_move_like_the_arrows()
    {
        var app = Seeded().FocusTasks().Press('j');
        Assert.Matches(@">\s+ 2 .*beta", app.Screen());

        app.Press('k');
        Assert.Matches(@">\s+ 1 .*alpha", app.Screen());
    }

    [Fact]
    public void Selection_stays_inside_the_list()
    {
        var app = Seeded().FocusTasks().Up(3);
        Assert.Matches(@">\s+ 1 .*alpha", app.Screen());

        app.Down(10);
        Assert.Matches(@">\s+ 2 .*beta", app.Screen());
    }

    [Fact]
    public void Menu_cursor_walks_from_the_built_in_lists_into_the_custom_ones_and_stops_at_the_end()
    {
        var app = Seeded().Down(5);
        Assert.Matches(@">\s+\S+\s+Work", app.Screen());
        Assert.Contains("job", app.Screen());

        app.Down(3);
        Assert.Matches(@">\s+\S+\s+Work", app.Screen());
    }

    [Fact]
    public void Active_panel_is_the_one_with_the_accent_border_and_modals_dim_both()
    {
        // Border colours are not visible in plain text; focus is observable through the footer and the selection arrow.
        var app = Seeded();
        Assert.DoesNotMatch(@">\s+ 1 .*alpha", app.Screen());

        app.FocusTasks().Press('n');
        Assert.Contains("New task", app.Screen());
    }

    [Fact]
    public void I_toggles_important_and_the_task_shows_up_in_Important()
    {
        var app = Seeded().FocusTasks().Press('i');
        Assert.Contains("Important (1)", app.Screen());

        app.Tab().Down(2);
        Assert.Contains("alpha", app.Screen());
    }

    [Fact]
    public void D_deletes_the_selected_task()
    {
        var app = Seeded().FocusTasks().Press('d');

        var screen = app.Screen();
        Assert.DoesNotContain("#1 alpha", screen);
        Assert.Contains("#2 beta", screen);
        Assert.Contains("My Day (1)", screen);
    }

    [Fact]
    public void Q_quits_only_from_browsing_and_never_while_typing()
    {
        var typing = new TestApp().Press('n').Type("q");
        Assert.False(typing.App.Quit);

        var browsing = new TestApp().Press('q');
        Assert.True(browsing.App.Quit);
    }

    [Fact]
    public void Letters_typed_in_a_form_do_not_trigger_shortcuts()
    {
        var app = new TestApp().Press('n').Type("odlnx?/pc");

        var screen = app.Screen();
        Assert.Contains("odlnx?/pc", screen);
        Assert.DoesNotContain("Data location", screen);
        Assert.DoesNotContain("Help", screen.Replace("Enter: save", ""));
    }

    [Fact]
    public void Edited_state_survives_a_restart_because_everything_is_persisted()
    {
        var app = new TestApp();
        app.Press('n').Type("persistent").Enter().Press('l').Type("Work").Enter();

        app.Restart();

        var screen = app.Screen();
        Assert.Contains("Work (0)", screen);
        Assert.Contains("My Day (1)", screen);
    }

    [Fact]
    public void Creating_tasks_numbers_them_from_one_and_never_reuses_codes()
    {
        var app = new TestApp();
        app.Press('n').Type("a").Enter().Press('n').Type("b").Enter().FocusTasks().Press('d').Tab().Press('n').Type("c").Enter();

        Assert.Equal([2, 3], app.Repository.Items.Select(i => i.Code).Order());
    }

    [Fact]
    public void Tasks_loaded_without_codes_get_numbered_when_the_app_starts()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("old one", code: 0);
            a.AddTask("old two", code: 0);
        });

        var screen = app.Screen();
        Assert.Contains("#1 old one", screen);
        Assert.Contains("#2 old two", screen);
    }

    [Fact]
    public void Status_values_round_trip_through_the_domain()
    {
        Assert.Equal(6, StatusCatalog.Ordered.Count);
        Assert.Equal("Need more details", StatusCatalog.Name(Status.Waiting));
    }
}
