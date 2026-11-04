using TuiDo.Domain;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class NotificationRenderTests
{
    private static string TopBar(string screen) => string.Join('\n', screen.Split('\n').Take(3));

    [Fact]
    public void Starts_empty()
    {
        var top = TopBar(new TestApp().Screen());

        Assert.DoesNotContain(Glyph.Completed, top);
        Assert.DoesNotContain(Glyph.Cancelled, top);
    }

    [Fact]
    public void Success_uses_the_completed_check_icon()
    {
        var app = new TestApp().Press('n').Type("x").Enter();

        var top = TopBar(app.Screen());

        Assert.Contains($"{Glyph.Completed} Task #1 created", top);
    }

    [Fact]
    public void Errors_use_the_cancelled_x_icon_and_show_the_reason()
    {
        var app = new TestApp();
        app.Repository.FailOnSave = true;
        app.Press('n').Type("x").Enter();

        var top = TopBar(app.Screen());

        Assert.Contains($"{Glyph.Cancelled} Failed to save tasks: disk is full", top);
    }

    [Fact]
    public void Warnings_use_the_yellow_overdue_icon()
    {
        var app = new TestApp();
        app.Editors.Installed.Clear();
        app.Press('n').Type("t").Tab().Tab().Ctrl('e').Enter();

        Assert.Contains($"{Glyph.Overdue} Neovim is not installed on this computer", TopBar(app.Screen()));
    }

    [Fact]
    public void Message_disappears_after_two_seconds()
    {
        var app = new TestApp().Press('n').Type("x").Enter();

        app.Clock.Advance(TimeSpan.FromMilliseconds(1900));
        Assert.Contains("Task #1 created", app.Screen());

        app.Clock.Advance(TimeSpan.FromMilliseconds(200));
        Assert.DoesNotContain("Task #1 created", app.Screen());
    }

    [Fact]
    public void Only_the_newest_message_is_shown()
    {
        var app = new TestApp().Press('n').Type("x").Enter().Press('n').Type("y").Enter();

        var top = TopBar(app.Screen());

        Assert.Contains("Task #2 created", top);
        Assert.DoesNotContain("Task #1 created", top);
    }

    [Fact]
    public void Signature_changes_when_a_message_appears_and_when_it_expires_so_the_host_redraws()
    {
        var app = new TestApp();
        var idle = app.App.Notifications.Signature;

        app.Press('n').Type("x").Enter();
        var shown = app.App.Notifications.Signature;
        app.Clock.Advance(TimeSpan.FromSeconds(3));
        var expired = app.App.Notifications.Signature;

        Assert.NotEqual(idle, shown);
        Assert.NotEqual(shown, expired);
        Assert.Equal(idle, expired);
    }

    [Theory]
    [InlineData('i', "#1 marked as important")]
    [InlineData('d', "Task #1 deleted")]
    public void Every_modification_is_reported(char key, string message)
    {
        var app = TestApp.With(a => a.AddTask("subject", code: 1));
        app.FocusTasks().Press(key);

        Assert.Contains(message, TopBar(app.Screen()));
    }

    [Fact]
    public void Very_long_messages_are_cut_to_fit_one_line()
    {
        var app = new TestApp();
        app.Repository.FailOnSave = true;
        app.Press('n').Type("x").Enter();

        var top = TopBar(app.Screen(width: 60));

        Assert.Equal(3, top.Split('\n').Length);
        Assert.Contains("…", top);
    }

    [Fact]
    public void Toggling_important_twice_reports_unmarking()
    {
        var app = TestApp.With(a => a.AddTask("subject", code: 1));
        app.FocusTasks().Press('i').Press('i');

        Assert.Contains("#1 unmarked as important", TopBar(app.Screen()));
    }

    [Fact]
    public void History_keeps_every_modification_even_after_it_leaves_the_screen()
    {
        var app = new TestApp().Press('n').Type("x").Enter().Press('l').Type("Work").Enter();
        app.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(["Task #1 created", "List 'Work' created"], app.App.Notifications.All.Select(n => n.Message));
    }

    [Fact]
    public void Status_change_is_reported_with_the_new_status_name()
    {
        var app = TestApp.With(a => a.AddTask("subject", code: 1, status: Status.Pending));
        app.FocusTasks().Press(' ').Down().Enter();

        Assert.Contains("#1 status: Doing", TopBar(app.Screen()));
    }
}
