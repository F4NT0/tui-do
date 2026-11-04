using TuiDo.Domain;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class TaskListRenderTests
{
    [Fact]
    public void Empty_list_explains_how_to_create_a_task()
    {
        Assert.Contains("No tasks. Press n to create one.", new TestApp().Screen());
    }

    [Fact]
    public void Shows_code_title_and_date_of_each_task()
    {
        var app = TestApp.With(a => a.AddTask("Write report", code: 7));

        var screen = app.Screen();

        Assert.Contains("#7", screen);
        Assert.Contains("Write report", screen);
        Assert.Contains("03/11/2026", screen);
    }

    [Fact]
    public void My_Day_shows_the_priority_rank_before_the_status_icon()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("first", priority: 1);
            a.AddTask("second", priority: 2);
        });

        var lines = app.Screen().Split('\n');

        Assert.Matches(@" 1 \S+\s+#1 first", lines.First(l => l.Contains("first")));
        Assert.Matches(@" 2 \S+\s+#2 second", lines.First(l => l.Contains("second")));
    }

    [Fact]
    public void Custom_lists_show_the_priority_rank_too()
    {
        var app = TestApp.With(a =>
        {
            var work = a.AddList("Work");
            a.AddTask("alpha", listId: work.Id, priority: 1);
            a.AddTask("beta", listId: work.Id, priority: 2);
        });
        app.Down(5);

        var lines = app.Screen().Split('\n');

        Assert.Matches(@" 1 \S+\s+#1 alpha", lines.First(l => l.Contains("alpha")));
        Assert.Matches(@" 2 \S+\s+#2 beta", lines.First(l => l.Contains("beta")));
    }

    [Fact]
    public void Orders_by_priority_and_puts_unranked_tasks_last()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("unranked");
            a.AddTask("second", priority: 2);
            a.AddTask("first", priority: 1);
        });

        var screen = app.Screen();

        Assert.True(screen.IndexOf("first", StringComparison.Ordinal) < screen.IndexOf("second", StringComparison.Ordinal));
        Assert.True(screen.IndexOf("second", StringComparison.Ordinal) < screen.IndexOf("unranked", StringComparison.Ordinal));
    }

    [Fact]
    public void Lists_without_manual_ranking_do_not_show_rank_numbers()
    {
        var app = TestApp.With(a => a.AddTask("starred", important: true, priority: 1));
        app.Down(2);

        var line = app.Screen().Split('\n').First(l => l.Contains("starred"));

        Assert.DoesNotMatch(@" 1 \S+\s+#1 starred", line);
        Assert.Contains("#1 starred", line);
    }

    [Fact]
    public void Overdue_pending_tasks_get_the_yellow_overdue_icon()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("late", date: a.Today.AddDays(-1));
            a.AddTask("on time");
        });

        var lines = app.Screen().Split('\n');

        Assert.Contains(Glyph.Overdue, lines.First(l => l.Contains("late")));
        Assert.DoesNotContain(Glyph.Overdue, lines.First(l => l.Contains("on time")));
    }

    [Theory]
    [InlineData(0, Glyph.NotStarted)]
    [InlineData(6, Glyph.Doing)]
    [InlineData(4, Glyph.NeedMoreDetails)]
    [InlineData(5, Glyph.Blocked)]
    public void Open_statuses_show_their_own_icon(int status, string glyph)
    {
        var app = TestApp.With(a =>
        {
            var work = a.AddList("Work");
            a.AddTask("subject", status: (Status)status, listId: work.Id);
        });
        app.Down(5);

        Assert.Contains(glyph, app.Screen().Split('\n').First(l => l.Contains("subject")));
    }

    [Fact]
    public void Completed_and_cancelled_tasks_live_in_their_own_lists_with_their_icons()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("finished", status: Status.Done);
            a.AddTask("dropped", status: Status.Cancelled);
        });

        app.Down(3);
        var completed = app.Screen();
        app.Down();
        var cancelled = app.Screen();

        Assert.Contains(Glyph.Completed, completed.Split('\n').First(l => l.Contains("finished")));
        Assert.DoesNotContain("dropped", completed);
        Assert.Contains(Glyph.Cancelled, cancelled.Split('\n').First(l => l.Contains("dropped")));
        Assert.DoesNotContain("finished", cancelled);
    }

    [Fact]
    public void Important_tasks_show_a_filled_orange_star_and_the_others_a_grey_one()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("vip", important: true);
            a.AddTask("plain");
        });

        var lines = app.Screen().Split('\n');

        Assert.Contains(Glyph.StarOn, lines.First(l => l.Contains("vip")));
        Assert.Contains(Glyph.StarOff, lines.First(l => l.Contains("plain")));
    }

    [Fact]
    public void Tasks_with_notes_show_a_notes_marker_and_blocked_ones_name_their_blocker()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("blocker", code: 1);
            var blocked = a.AddTask("waiting", notes: "# hi", code: 2);
            blocked.Status = Status.Blocked;
            blocked.BlockedBy = 1;
        });

        var screen = app.Screen();

        Assert.Contains("notes", screen);
        Assert.Contains("blocked by #1", screen);
    }

    [Fact]
    public void Selection_marker_only_shows_when_the_task_panel_has_focus()
    {
        var app = TestApp.With(a => a.AddTask("subject"));

        Assert.DoesNotMatch(@">\s+ 1 .*subject", app.Screen());

        app.FocusTasks();

        Assert.Matches(@">\s+ 1 .*subject", app.Screen());
    }

    [Fact]
    public void Future_tasks_appear_in_Tasks_and_not_in_My_Day()
    {
        var app = TestApp.With(a => a.AddTask("later", date: a.Today.AddDays(5)));

        Assert.DoesNotContain("later", app.Screen());
        app.Down();

        Assert.Contains("later", app.Screen());
        Assert.Contains("08/11/2026", app.Screen());
    }

    [Fact]
    public void Completed_tasks_are_struck_through_but_still_readable()
    {
        var app = TestApp.With(a => a.AddTask("finished", status: Status.Done));
        app.Down(3);

        Assert.Contains("finished", app.Screen());
    }
}
