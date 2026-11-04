using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using TuiDo.Presentation.Documents;

namespace TuiDo.Tests.Unit;

public class MarkdownRendererTests
{
    private static string Render(string markdown, int width = 80)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Write(MarkdownRenderer.Render(markdown));
        return console.Output;
    }

    [Fact]
    public void Plain_paragraphs_join_their_lines_and_are_separated_by_a_blank_line()
    {
        var output = Render("first line\nsecond line\n\nnext paragraph");

        Assert.Contains("first line second line", output);
        Assert.Contains("next paragraph", output);
        Assert.Contains("\n\n", output.Replace(" \n", "\n"));
    }

    [Fact]
    public void Windows_and_old_mac_line_endings_are_normalised()
    {
        Assert.Contains("a b", Render("a\r\nb"));
        Assert.Contains("a b", Render("a\rb"));
    }

    [Theory]
    [InlineData("# One", "One")]
    [InlineData("## Two", "Two")]
    [InlineData("###### Six", "SIX")]
    public void Headings_lose_their_marks(string markdown, string expected)
    {
        var output = Render(markdown);

        Assert.Contains(expected, output);
        Assert.DoesNotContain("#", output);
    }

    [Fact]
    public void Square_brackets_in_user_text_never_break_rendering()
    {
        var output = Render("array[0] and [red]not markup[/] and **[bold]**");

        Assert.Contains("array[0]", output);
        Assert.Contains("[red]not markup[/]", output);
    }

    [Fact]
    public void Links_show_only_their_text()
    {
        var output = Render("see [the docs](https://example.com) now");

        Assert.Contains("see the docs now", output);
        Assert.DoesNotContain("https://example.com", output);
    }

    [Fact]
    public void Nested_list_items_are_indented()
    {
        var output = Render("- parent\n  - child");

        Assert.Contains("• parent", output);
        Assert.Matches(@"\s{2,}• child", output);
    }

    [Fact]
    public void Tables_pad_short_rows_and_keep_every_column()
    {
        var output = Render("| A | B | C |\n|---|---|---|\n| 1 | 2 |");

        Assert.Contains("A", output);
        Assert.Contains("C", output);
        Assert.Contains("1", output);
    }

    [Fact]
    public void Table_cells_support_inline_formatting()
    {
        var output = Render("| Name |\n|---|\n| **bold** `code` |");

        Assert.Contains("bold", output);
        Assert.DoesNotContain("**", output);
    }

    [Fact]
    public void Code_blocks_without_a_language_have_no_label_and_empty_blocks_still_render()
    {
        var output = Render("```\nplain\n```\n\n```text\n```");

        Assert.Contains("1  plain", output);
        Assert.DoesNotContain("```", output);
    }

    [Fact]
    public void Package_manager_commands_are_recognised_in_code_blocks()
    {
        var output = Render("```powershell\nwinget install Foo\n```");

        Assert.Contains("1  winget install Foo", output);
    }

    [Fact]
    public void Unterminated_code_fences_swallow_the_rest_without_crashing()
    {
        var output = Render("```bash\necho hi");

        Assert.Contains("echo hi", output);
    }

    [Fact]
    public void Math_between_dollars_loses_carets_too()
    {
        Assert.Contains("n2", Render("$n^2$"));
    }

    [Fact]
    public void Empty_input_renders_nothing_but_does_not_throw()
    {
        Assert.Equal("", Render("").Trim());
    }
}

public class ScrollViewTests
{
    private static IRenderable Lines(int count) => new Rows(Enumerable.Range(1, count).Select(i => (IRenderable)new Text($"line {i:00}")));

    private static string Draw(ScrollView view)
    {
        var console = new TestConsole();
        console.Profile.Width = 40;
        console.Write(view);
        return console.Output;
    }

    [Fact]
    public void Shows_only_the_window_starting_at_the_scroll_offset()
    {
        var output = Draw(new ScrollView(Lines(20), visible: 5, scroll: 3));

        Assert.Contains("line 04", output);
        Assert.Contains("line 08", output);
        Assert.DoesNotContain("line 03", output);
        Assert.DoesNotContain("line 09", output);
    }

    [Fact]
    public void Scroll_is_clamped_to_the_last_page()
    {
        var view = new ScrollView(Lines(20), visible: 5, scroll: 999);

        var output = Draw(view);

        Assert.Contains("line 20", output);
        Assert.Contains("line 16", output);
        Assert.DoesNotContain("line 15", output);
        Assert.Equal(20, view.Total);
        Assert.Equal(15, view.Offset);
    }

    [Fact]
    public void Prepare_measures_the_content_before_drawing()
    {
        var view = new ScrollView(Lines(12), visible: 5, scroll: 0);
        Assert.Equal(0, view.Total);

        view.Prepare(new TestConsole(), 40);

        Assert.Equal(12, view.Total);
    }

    [Fact]
    public void Short_content_is_shown_completely()
    {
        var view = new ScrollView(Lines(3), visible: 10, scroll: 5);

        var output = Draw(view);

        Assert.Contains("line 01", output);
        Assert.Contains("line 03", output);
        Assert.Equal(0, view.Offset);
    }
}
