using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class MainLayoutRenderTests
{
    [Fact]
    public void Shows_clock_with_date_and_time_in_the_top_bar()
    {
        var app = new TestApp();

        Assert.Contains("03/11/2026 18:51:45", app.Screen());
    }

    [Fact]
    public void Clock_follows_the_clock_service()
    {
        var app = new TestApp();
        app.Clock.Advance(TimeSpan.FromSeconds(75));

        Assert.Contains("03/11/2026 18:53:00", app.Screen());
    }

    [Fact]
    public void Shows_both_panels_and_the_built_in_lists()
    {
        var screen = new TestApp().Screen();

        foreach (var text in new[] { "Lists", "My Lists", "My Day", "Tasks", "Important", "Completed", "Cancelled" })
            Assert.Contains(text, screen);
        Assert.Contains(Glyph.MyDay, screen);
        Assert.Contains(Glyph.Tasks, screen);
        Assert.Contains(Glyph.StarOn, screen);
        Assert.Contains(Glyph.Completed, screen);
        Assert.Contains(Glyph.Cancelled, screen);
    }

    [Fact]
    public void Counts_tasks_in_every_built_in_list()
    {
        var app = TestApp.With(a =>
        {
            a.AddTask("today");
            a.AddTask("late", date: a.Today.AddDays(-2));
            a.AddTask("future", date: a.Today.AddDays(3));
            a.AddTask("starred", important: true);
            a.AddTask("done", status: TuiDo.Domain.Status.Done);
            a.AddTask("dropped", status: TuiDo.Domain.Status.Cancelled);
        });

        var screen = app.Screen();

        Assert.Contains("My Day (3)", screen);
        Assert.Contains("Tasks (1)", screen);
        Assert.Contains("Important (1)", screen);
        Assert.Contains("Completed (1)", screen);
        Assert.Contains("Cancelled (1)", screen);
    }

    [Fact]
    public void Custom_lists_area_invites_the_user_to_create_a_list_when_empty()
    {
        Assert.Contains("Press l to create a list", new TestApp().Screen());
    }

    [Fact]
    public void Custom_lists_show_their_icon_name_and_count()
    {
        var app = TestApp.With(a =>
        {
            var work = a.AddList("Work", "\uf4a5");
            a.AddList("Home", "\uf015");
            a.AddTask("report", listId: work.Id);
        });

        var screen = app.Screen();

        Assert.Contains("Work (1)", screen);
        Assert.Contains("Home (0)", screen);
        Assert.Contains("\uf4a5", screen);
        Assert.Contains("\uf015", screen);
        Assert.DoesNotContain("Press l to create a list", screen);
    }

    [Fact]
    public void Marks_the_selected_list_with_an_arrow_and_follows_the_cursor()
    {
        var app = new TestApp();
        Assert.Matches(@">\s+\S*\s*My Day", app.Screen());

        app.Down();

        Assert.Matches(@">\s+\S*\s*Tasks", app.Screen());
        Assert.Contains("Tasks", RightPanelTitle(app.Screen()));
    }

    [Fact]
    public void Right_panel_title_is_the_selected_list_name()
    {
        var app = new TestApp();

        Assert.Contains("My Day", RightPanelTitle(app.Screen()));
        app.Down(3);
        Assert.Contains("Completed", RightPanelTitle(app.Screen()));
    }

    [Fact]
    public void Layout_fits_the_requested_terminal_height()
    {
        var screen = new TestApp().Screen(width: 120, height: 30);

        Assert.Equal(30 - 1, screen.TrimEnd((char)10).Split((char)10).Length);
    }

    /// <summary>The line carrying both panel titles: the first row below the 3-line top bar.</summary>
    private static string RightPanelTitle(string screen) => screen.Split((char)10)[3];
}
