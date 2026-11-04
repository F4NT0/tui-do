using TuiDo.Domain;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Rendering;

public class SearchRenderTests
{
    private static TestApp Seeded() => TestApp.With(a =>
    {
        a.AddTask("Buy milk", code: 1);
        a.AddTask("Write report", code: 2, date: a.Today.AddDays(9));
        a.AddTask("Old milk bill", code: 3, status: Status.Done);
    });

    [Fact]
    public void Slash_starts_a_search_and_shows_the_query_in_footer_and_title()
    {
        var app = Seeded().Press('/').Type("mi");

        var screen = app.Screen();

        Assert.Contains("/mi▏", screen);
        Assert.Contains("Enter: keep results | Esc: cancel", screen);
        Assert.Contains("Search: mi", screen);
    }

    [Fact]
    public void Search_matches_titles_across_every_list_and_status()
    {
        var screen = Seeded().Press('/').Type("milk").Screen();

        Assert.Contains("#1 Buy milk", screen);
        Assert.Contains("#3 Old milk bill", screen);
        Assert.DoesNotContain("Write report", screen);
    }

    [Fact]
    public void Search_by_code_accepts_the_hash_prefix()
    {
        var withHash = Seeded().Press('/').Type("#2").Screen();
        var without = Seeded().Press('/').Type("2").Screen();

        Assert.Contains("#2 Write report", withHash);
        Assert.DoesNotContain("Buy milk", withHash);
        Assert.Contains("#2 Write report", without);
    }

    [Fact]
    public void Backspace_widens_the_search_again()
    {
        var app = Seeded().Press('/').Type("report");
        Assert.DoesNotContain("Buy milk", app.Screen());

        for (var i = 0; i < 6; i++) app.Press(ConsoleKey.Backspace);

        Assert.Contains("Buy milk", app.Screen());
    }

    [Fact]
    public void Enter_keeps_the_results_so_they_can_be_browsed()
    {
        var app = Seeded().Press('/').Type("milk").Enter();

        var screen = app.Screen();

        Assert.Contains("Search: milk", screen);
        Assert.Contains("↑↓: select | Enter: open", screen);
        Assert.Matches(@">\s+ ?\S*\s+#1 Buy milk", screen.Replace("\u2800", ""));
    }

    [Fact]
    public void Esc_while_typing_cancels_the_search()
    {
        var app = Seeded().Press('/').Type("milk").Esc();

        var screen = app.Screen();

        Assert.DoesNotContain("Search:", screen);
        Assert.Contains("My Day", screen.Split('\n')[3]);
    }

    [Fact]
    public void Esc_after_keeping_results_clears_the_search_filter()
    {
        var app = Seeded().Press('/').Type("milk").Enter().Esc();

        Assert.DoesNotContain("Search:", app.Screen());
        Assert.Contains("#1 Buy milk", app.Screen());
    }

    [Fact]
    public void Search_results_are_not_ranked_and_do_not_show_a_list_selection_arrow()
    {
        var app = Seeded().Press('/').Type("milk");

        var line = app.Screen().Split('\n').First(l => l.Contains("Buy milk"));
        Assert.DoesNotMatch(@" 1 \S+\s+#1", line);
        Assert.DoesNotMatch(@">\s+\S*\s*My Day", app.Screen());
    }
}
