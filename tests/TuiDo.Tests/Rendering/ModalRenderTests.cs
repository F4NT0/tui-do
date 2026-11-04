using TuiDo.Domain;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class StatusPickerRenderTests
{
    private static TestApp OneTask() => TestApp.With(a => a.AddTask("subject", code: 1));

    [Fact]
    public void Space_opens_the_status_modal_with_the_six_statuses_and_their_icons()
    {
        var app = OneTask().FocusTasks().Press(' ');

        var screen = app.Screen();

        Assert.Contains("Status", screen);
        foreach (var name in new[] { "Not started", "Doing", "Need more details", "Blocked", "Completed", "Cancelled" })
            Assert.Contains(name, screen);
        foreach (var glyph in new[] { Glyph.NotStarted, Glyph.Doing, Glyph.NeedMoreDetails, Glyph.Blocked, Glyph.Completed, Glyph.Cancelled })
            Assert.Contains(glyph, screen);
        Assert.Contains("↑↓: select | Enter: confirm | Esc: cancel", screen);
    }

    [Fact]
    public void Current_status_is_preselected()
    {
        var app = TestApp.With(a => a.AddTask("subject", status: Status.Doing));
        app.FocusTasks().Press(' ');

        Assert.Matches(@">\s+\S+\s+Doing", app.Screen());
    }

    [Fact]
    public void Arrow_keys_move_the_selection()
    {
        var app = OneTask().FocusTasks().Press(' ').Down(2);

        Assert.Matches(@">\s+\S+\s+Need more details", app.Screen());
    }

    [Fact]
    public void Choosing_Completed_moves_the_task_to_the_Completed_list_and_notifies()
    {
        var app = OneTask().FocusTasks().Press(' ').Down(4).Enter();

        var screen = app.Screen();

        Assert.Contains("#1 status: Completed", screen);
        Assert.Contains("Completed (1)", screen);
        Assert.Contains("My Day (0)", screen);
        Assert.DoesNotContain("Status", screen.Split('\n')[3]);
    }

    [Fact]
    public void Choosing_Cancelled_moves_the_task_to_the_Cancelled_list()
    {
        var app = OneTask().FocusTasks().Press(' ').Down(5).Enter();

        Assert.Contains("Cancelled (1)", app.Screen());
        Assert.Contains("My Day (0)", app.Screen());
    }

    [Fact]
    public void Doing_and_Need_more_details_keep_the_task_in_My_Day()
    {
        var app = OneTask().FocusTasks().Press(' ').Down().Enter();
        Assert.Contains("My Day (1)", app.Screen());
        Assert.Contains(Glyph.Doing, app.Screen());

        app.Press(' ').Down().Enter();
        Assert.Contains("My Day (1)", app.Screen());
        Assert.Contains(Glyph.NeedMoreDetails, app.Screen());
    }

    [Fact]
    public void Esc_closes_the_modal_without_changing_the_status()
    {
        var app = OneTask().FocusTasks().Press(' ').Down(4).Esc();

        Assert.DoesNotContain("Need more details", app.Screen());
        Assert.Contains("My Day (1)", app.Screen());
    }
}

public class BlockedByRenderTests
{
    private static TestApp TwoTasks() => TestApp.With(a =>
    {
        a.AddTask("blocked one", code: 1);
        a.AddTask("blocker candidate", code: 2);
        a.AddTask("already done", code: 3, status: Status.Done);
    });

    [Fact]
    public void Choosing_Blocked_opens_a_second_modal_listing_the_other_open_tasks_only()
    {
        var app = TwoTasks().FocusTasks().Press(' ').Down(3).Enter();

        var screen = app.Screen();

        Assert.Contains("Blocked by", screen);
        Assert.Contains("#2 blocker candidate", screen);
        Assert.DoesNotContain("#1 blocked one", screen.Split("Blocked by")[1]);
        Assert.DoesNotContain("#3 already done", screen);
    }

    [Fact]
    public void Confirming_records_the_blocker_and_shows_it_in_list_and_detail()
    {
        var app = TwoTasks().FocusTasks().Press(' ').Down(3).Enter().Enter();

        var list = app.Screen();
        Assert.Contains("blocked by #2", list);
        Assert.Contains("#1 blocked by #2", list);
        Assert.Contains(Glyph.Blocked, list);

        app.Enter();
        Assert.Contains("waiting for #2", app.Screen());
    }

    [Fact]
    public void Arrow_keys_pick_among_several_candidates()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("subject", code: 1);
            a.AddTask("second", code: 2);
            a.AddTask("third", code: 3);
        });
        app.FocusTasks().Press(' ').Down(3).Enter().Down().Enter();

        Assert.Contains("blocked by #3", app.Screen());
    }

    [Fact]
    public void Without_candidates_no_modal_opens_and_the_status_is_unchanged()
    {
        var app = TestApp.With(a => a.AddTask("alone", code: 1));
        app.FocusTasks().Press(' ').Down(3).Enter();

        var screen = app.Screen();

        Assert.DoesNotContain("Blocked by", screen);
        Assert.DoesNotContain("blocked by", screen);
    }

    [Fact]
    public void Deleting_the_blocker_unblocks_the_waiting_task()
    {
        var app = TwoTasks().FocusTasks().Press(' ').Down(3).Enter().Enter();
        app.Down().Press('d');

        var screen = app.Screen();

        Assert.DoesNotContain("blocked by", screen);
        Assert.Contains("Task #2 deleted", screen);
    }
}

public class MoveToListRenderTests
{
    [Fact]
    public void M_opens_the_move_modal_with_None_and_every_custom_list()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("subject");
            a.AddList("Work", "\uf4a5");
            a.AddList("Home", "\uf015");
        });
        app.FocusTasks().Press('m');

        var screen = app.Screen();

        Assert.Contains("Move to list", screen);
        Assert.Contains("None (My Day / Tasks)", screen);
        Assert.Contains("Work", screen);
        Assert.Contains("Home", screen);
        Assert.Contains("\uf4a5", screen);
    }

    [Fact]
    public void Confirming_moves_the_task_out_of_My_Day_into_the_chosen_list()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("subject", code: 1);
            a.AddList("Work");
        });

        app.FocusTasks().Press('m').Down().Enter();

        var screen = app.Screen();
        Assert.Contains("My Day (0)", screen);
        Assert.Contains("Work (1)", screen);
        Assert.Contains("#1 moved", screen);
    }

    [Fact]
    public void Moving_back_to_None_returns_the_task_to_My_Day()
    {
        var app = TestApp.With(a =>
        {
            var work = a.AddList("Work");
            a.AddTask("subject", listId: work.Id);
        });
        app.Down(5).FocusTasks().Press('m').Up().Enter();

        var screen = app.Screen();
        Assert.Contains("My Day (1)", screen);
        Assert.Contains("Work (0)", screen);
    }

    [Fact]
    public void Move_also_works_from_the_detail_view_and_closes_it()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("subject");
            a.AddList("Work");
        });
        app.FocusTasks().Enter().Press('m').Down().Enter();

        var screen = app.Screen();
        Assert.Contains("Work (1)", screen);
        Assert.Contains("↑↓: select | Enter: open", screen);
    }
}

public class PriorityRenderTests
{
    private static TestApp Three() => TestApp.With(a =>
    {
        a.AddTask("alpha", priority: 1);
        a.AddTask("beta", priority: 2);
        a.AddTask("gamma", priority: 3);
    });

    /// <summary>Titles in the order the task rows appear (rows read "#code title"; notifications never use that shape).</summary>
    private static string[] Order(string screen) =>
        System.Text.RegularExpressions.Regex.Matches(screen, @"#\d+ (alpha|beta|gamma)").Select(m => m.Groups[1].Value).ToArray();

    [Fact]
    public void Lower_p_enters_move_mode_with_a_vertical_arrows_marker_and_footer()
    {
        var app = Three().FocusTasks().Press('p');

        var screen = app.Screen();

        Assert.Contains("↕", screen);
        Assert.Contains("↑↓: move task | Enter/p: done", screen);
    }

    [Fact]
    public void Arrows_move_the_selected_task_down_and_up_the_list()
    {
        var app = Three().FocusTasks().Press('p').Down();
        Assert.Equal(["beta", "alpha", "gamma"], Order(app.Screen()));

        app.Down().Up().Up();
        Assert.Equal(["alpha", "beta", "gamma"], Order(app.Screen()));
    }

    [Fact]
    public void Leaving_move_mode_keeps_the_new_order_and_restores_the_normal_marker_and_footer()
    {
        var app = Three().FocusTasks().Press('p').Down().Enter();

        var screen = app.Screen();

        Assert.Equal(["beta", "alpha", "gamma"], Order(screen));
        Assert.DoesNotContain("↕", screen);
        Assert.Contains("c: calendar", screen);
    }

    [Fact]
    public void Move_notifies_the_new_position()
    {
        var app = Three().FocusTasks().Press('p').Down();

        Assert.Contains("'alpha' moved to position 2", app.Screen());
    }

    [Fact]
    public void Upper_P_asks_for_a_position_and_moves_the_task_there()
    {
        var app = Three().FocusTasks().Down(2).Press('P');
        var modal = app.Screen();
        Assert.Contains("Priority", modal);
        Assert.Contains("Move to position (1-3):", modal);

        app.Type("1").Enter();

        Assert.Equal(["gamma", "alpha", "beta"], Order(app.Screen()));
    }

    [Fact]
    public void Position_beyond_the_list_is_clamped_and_non_digits_are_ignored()
    {
        var app = Three().FocusTasks().Press('P').Type("a9").Enter();

        Assert.Equal(["beta", "gamma", "alpha"], Order(app.Screen()));
    }

    [Fact]
    public void Position_prompt_supports_backspace_and_esc()
    {
        var app = Three().FocusTasks().Press('P').Type("12").Press(ConsoleKey.Backspace);
        Assert.Contains("Move to position (1-3): 1", app.Screen());

        app.Esc();

        Assert.DoesNotContain("Move to position", app.Screen());
        Assert.Equal(["alpha", "beta", "gamma"], Order(app.Screen()));
    }

    [Fact]
    public void Reordering_is_only_available_in_ranked_lists()
    {
        var app = TestApp.With(a => a.AddTask("alpha", important: true));
        app.Down(2).FocusTasks().Press('p');

        Assert.DoesNotContain("↕", app.Screen());
    }

    [Fact]
    public void Reordering_assigns_ranks_to_tasks_that_had_none_yet()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("alpha");
            a.AddTask("beta");
        });
        app.FocusTasks().Press('p').Down();

        Assert.Equal(["beta", "alpha"], Order(app.Screen()));
        Assert.All(app.Repository.Items, i => Assert.True(i.Priority > 0));
    }
}

public class NewListRenderTests
{
    [Fact]
    public void L_opens_the_new_list_form_with_name_icon_and_a_live_preview()
    {
        var app = new TestApp().Press('l').Type("Work").Tab().Type("f522");

        var screen = app.Screen();

        Assert.Contains("New list", screen);
        Assert.Contains("Name", screen);
        Assert.Contains("Icon (e.g. f522, \\uf522 or paste the glyph)", screen);
        Assert.Contains($"Preview: {Glyph.MyDay}  Work", screen);
        Assert.Contains("Enter: create", screen);
    }

    [Fact]
    public void Enter_creates_the_list_selects_it_and_notifies()
    {
        var app = new TestApp().Press('l').Type("Work").Tab().Type("\\udb81\\udcce").Enter();

        var screen = app.Screen();

        Assert.Contains("List 'Work' created", screen);
        Assert.Contains("Work (0)", screen);
        Assert.Contains(Glyph.StarOn, screen);
        Assert.Matches(@">\s+\S+\s+Work", screen);
        Assert.Single(app.Repository.Lists);
    }

    [Fact]
    public void Name_is_required()
    {
        var app = new TestApp().Press('l').Enter();

        var screen = app.Screen();

        Assert.Contains("New list", screen);
        Assert.Equal(2, screen.Split("Name is required").Length - 1 + (screen.Contains("List name is required") ? 1 : 0));
        Assert.Empty(app.Repository.Lists);
    }

    [Fact]
    public void Ctrl_V_pastes_the_icon_glyph()
    {
        var app = new TestApp();
        app.Clipboard.Text = "\uf015";
        app.Press('l').Type("Home").Tab().Ctrl('v');

        Assert.Contains("Preview: \uf015  Home", app.Screen());
    }

    [Fact]
    public void X_on_a_custom_list_deletes_it_and_sends_its_tasks_back_to_My_Day()
    {
        var app = TestApp.With(a =>
        {
            var work = a.AddList("Work");
            a.AddTask("report", listId: work.Id);
        });
        app.Down(5).Press('x');

        var screen = app.Screen();

        Assert.Contains("List 'Work' deleted", screen);
        Assert.Contains("My Day (1)", screen);
        Assert.Contains("Press l to create a list", screen);
    }

    [Fact]
    public void X_does_nothing_on_built_in_lists()
    {
        var app = new TestApp().Press('x');

        Assert.DoesNotContain("deleted", app.Screen());
    }
}

public class EditorChooserRenderTests
{
    private static TestApp AboutFocused(TestApp app) => app.Press('n').Type("t").Tab().Tab().Type("draft").Ctrl('e');

    [Fact]
    public void Lists_neovim_vim_and_vscode_with_their_icons_first()
    {
        var screen = AboutFocused(new TestApp()).Screen();

        Assert.Contains("Edit About in", screen);
        Assert.Matches($@">\s+{Glyph.Neovim}\s+Neovim", screen);
        Assert.Matches($@"{Glyph.Vim}\s+Vim", screen);
        Assert.Matches($@"{Glyph.VsCode}\s+VS Code", screen);
    }

    [Fact]
    public void Installed_editor_receives_the_notes_and_its_result_replaces_them()
    {
        var app = new TestApp();
        app.Editors.EditedText = "# edited in vim";

        AboutFocused(app).Enter();

        var screen = app.Screen();
        Assert.Equal("draft", app.Editors.ReceivedText);
        Assert.Equal("Neovim", app.Editors.LaunchedWith!.Name);
        Assert.Contains("# edited in vim", screen);
        Assert.Contains("About updated from Neovim", screen);
        Assert.Contains("New task", screen);
    }

    [Fact]
    public void Arrow_keys_choose_a_different_editor()
    {
        var app = AboutFocused(new TestApp()).Down(2).Enter();

        Assert.Equal("VS Code", app.Editors.LaunchedWith!.Name);
    }

    [Fact]
    public void Missing_editor_shows_a_warning_instead_of_launching()
    {
        var app = new TestApp();
        app.Editors.Installed.Remove("nvim");

        AboutFocused(app).Enter();

        var screen = app.Screen();
        Assert.Contains("Neovim is not installed on this computer", screen);
        Assert.Contains(Glyph.Overdue, screen);
        Assert.Null(app.Editors.LaunchedWith);
        Assert.Contains("draft", screen);
    }

    [Fact]
    public void Esc_closes_the_chooser()
    {
        var app = AboutFocused(new TestApp()).Esc();

        Assert.DoesNotContain("Edit About in", app.Screen());
        Assert.Contains("New task", app.Screen());
    }
}
