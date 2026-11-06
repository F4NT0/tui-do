using TuiDo.Domain;
using TuiDo.Infrastructure;
using TuiDo.Presentation;
using TuiDo.Presentation.Forms;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Unit;

public sealed class WorkspaceRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tuido-ws-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string Descriptions => Path.Combine(_dir, "descriptions");

    [Fact]
    public void Json_files_are_indented()
    {
        var repo = new JsonTodoRepository(_dir);

        repo.SaveItems([new TodoItem { Code = 1, Title = "a" }]);
        repo.SaveLists([new CustomList { Name = "Work" }]);

        Assert.Contains("\n  {\n    \"", File.ReadAllText(repo.ItemsPath).Replace("\r\n", "\n"));
        Assert.Contains("\n  {\n    \"", File.ReadAllText(repo.ListsPath).Replace("\r\n", "\n"));
    }

    [Fact]
    public void Initialize_creates_both_json_files_and_the_descriptions_folder()
    {
        new JsonTodoRepository(_dir, markdownDescriptions: true).Initialize();

        Assert.True(File.Exists(Path.Combine(_dir, "tasks.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "lists.json")));
        Assert.True(Directory.Exists(Descriptions));
    }

    [Fact]
    public void Notes_are_written_to_a_markdown_file_named_after_the_code_and_title()
    {
        var repo = new JsonTodoRepository(_dir, markdownDescriptions: true);

        repo.SaveItems([new TodoItem { Code = 12, Title = "Buy: milk?", Notes = "# milk" }]);

        Assert.Equal("# milk", File.ReadAllText(Path.Combine(Descriptions, "12-Buy milk.md")));
        Assert.DoesNotContain("Notes", File.ReadAllText(repo.ItemsPath));
    }

    [Fact]
    public void Notes_round_trip_through_the_markdown_files()
    {
        new JsonTodoRepository(_dir, markdownDescriptions: true).SaveItems([new TodoItem { Code = 3, Title = "x", Notes = "body" }]);

        var loaded = new JsonTodoRepository(_dir, markdownDescriptions: true).LoadItems().Single();

        Assert.Equal("body", loaded.Notes);
    }

    [Fact]
    public void Renaming_a_task_renames_its_markdown_file()
    {
        var repo = new JsonTodoRepository(_dir, markdownDescriptions: true);
        var item = new TodoItem { Code = 1, Title = "old", Notes = "n" };
        repo.SaveItems([item]);

        item.Title = "new";
        repo.SaveItems([item]);

        Assert.False(File.Exists(Path.Combine(Descriptions, "1-old.md")));
        Assert.True(File.Exists(Path.Combine(Descriptions, "1-new.md")));
    }

    [Fact]
    public void Clearing_the_notes_deletes_the_markdown_file()
    {
        var repo = new JsonTodoRepository(_dir, markdownDescriptions: true);
        var item = new TodoItem { Code = 1, Title = "t", Notes = "n" };
        repo.SaveItems([item]);

        item.Notes = "";
        repo.SaveItems([item]);

        Assert.Empty(Directory.GetFiles(Descriptions));
    }

    [Fact]
    public void The_descriptions_folder_is_only_created_when_a_description_exists()
    {
        var repo = new JsonTodoRepository(_dir, markdownDescriptions: true);

        repo.SaveItems([new TodoItem { Code = 1, Title = "t" }]);
        Assert.False(Directory.Exists(Descriptions));

        repo.SaveItems([new TodoItem { Code = 1, Title = "t", Notes = "n" }]);
        Assert.True(Directory.Exists(Descriptions));
    }

    [Fact]
    public void Missing_json_files_are_created_on_first_save()
    {
        Directory.CreateDirectory(_dir);
        var repo = new JsonTodoRepository(_dir, markdownDescriptions: true);

        repo.SaveLists([new CustomList { Name = "Work" }]);
        Assert.True(File.Exists(repo.ListsPath));
        Assert.False(File.Exists(repo.ItemsPath));

        repo.SaveItems([new TodoItem { Code = 1, Title = "t" }]);
        Assert.True(File.Exists(repo.ItemsPath));
    }

    [Fact]
    public void Unrelated_markdown_files_are_never_deleted()
    {
        Directory.CreateDirectory(Descriptions);
        var mine = Path.Combine(Descriptions, "readme.md");
        File.WriteAllText(mine, "keep");

        new JsonTodoRepository(_dir, markdownDescriptions: true).SaveItems([new TodoItem { Code = 1, Title = "t" }]);

        Assert.True(File.Exists(mine));
    }
}

public sealed class DirectoryBrowserTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tuido-br-" + Guid.NewGuid().ToString("N"));

    public DirectoryBrowserTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "alpha", "inner"));
        Directory.CreateDirectory(Path.Combine(_dir, "beta"));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Lists_sub_directories_in_alphabetical_order()
    {
        var browser = new DirectoryBrowser(_dir);

        Assert.Equal(["alpha", "beta"], browser.Entries.Select(e => e.Name));
    }

    [Fact]
    public void Selected_is_the_highlighted_directory_not_the_one_being_viewed()
    {
        var browser = new DirectoryBrowser(_dir);
        browser.Move(1);

        Assert.Equal(Path.Combine(_dir, "beta"), browser.Selected);
    }

    [Fact]
    public void Enter_goes_inside_and_up_returns_with_the_cursor_on_the_previous_folder()
    {
        var browser = new DirectoryBrowser(_dir);

        browser.Enter();
        Assert.Equal(["inner"], browser.Entries.Select(e => e.Name));

        browser.Up();
        Assert.Equal(Path.GetFullPath(_dir), browser.Current);
        Assert.Equal(Path.Combine(_dir, "alpha"), browser.Selected);
    }

    [Fact]
    public void A_directory_without_sub_directories_selects_itself()
    {
        var browser = new DirectoryBrowser(Path.Combine(_dir, "beta"));

        Assert.Empty(browser.Entries);
        Assert.Equal(Path.Combine(_dir, "beta"), browser.Selected);
    }

    [Fact]
    public void The_cursor_never_leaves_the_list()
    {
        var browser = new DirectoryBrowser(_dir);

        browser.Move(-5);
        Assert.Equal(0, browser.Cursor);

        browser.Move(50);
        Assert.Equal(1, browser.Cursor);
    }
}

public sealed class DirectorySwitchRenderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tuido-sw-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private TestApp App() => new() { RepositoryFactory = path => new JsonTodoRepository(path, markdownDescriptions: true) };

    [Fact]
    public void D_does_nothing_when_directory_switching_is_not_available()
    {
        var app = new TestApp().Press('d');

        Assert.DoesNotContain("Open directory", app.Screen());
    }

    [Fact]
    public void D_opens_the_directory_picker_from_the_lists_panel()
    {
        var app = App();
        app.Restart();
        app.Press('d');

        Assert.Contains("Open directory", app.Screen());
        Assert.Contains("Enter: open directory", app.Screen());
    }

    [Fact]
    public void D_still_deletes_a_task_from_the_tasks_panel()
    {
        var app = App();
        app.AddTask("doomed");
        app.Restart();

        app.FocusTasks().Press('d');

        Assert.DoesNotContain("Open directory", app.Screen());
        Assert.DoesNotContain("doomed", app.Screen());
    }

    [Fact]
    public void Enter_opens_the_highlighted_directory_and_loads_its_tasks()
    {
        var target = Path.Combine(_dir, "other");
        new JsonTodoRepository(target, markdownDescriptions: true).SaveItems([new TodoItem { Code = 1, Title = "from other dir", Date = new FakeClock().Now.Date }]);
        var app = App();
        app.Restart();
        app.App.State.DirPick = new DirectoryBrowser(_dir);

        app.Enter();

        var screen = app.Screen();
        Assert.Contains("Opened other", screen);
        Assert.Contains("from other dir", screen);
        Assert.Contains("other", screen);
    }

    [Fact]
    public void Opening_a_directory_without_any_data_files_is_allowed()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "empty"));
        var app = App();
        app.Restart();
        app.App.State.DirPick = new DirectoryBrowser(_dir);

        app.Enter();

        Assert.Contains("Opened empty", app.Screen());
        Assert.Empty(app.App.Service.Items);
    }
}

public class ListIconPickerRenderTests
{
    [Fact]
    public void I_in_the_icon_field_opens_the_icon_picker()
    {
        var app = new TestApp().Press('l').Tab().Press('i');

        var screen = app.Screen();

        Assert.Contains("Choose icon", screen);
        Assert.Contains("\uf510", screen);
        Assert.Contains("\udb84\udda1", screen);
    }

    [Fact]
    public void I_in_the_name_field_is_just_a_letter()
    {
        var app = new TestApp().Press('l').Press('i');

        Assert.DoesNotContain("Choose icon", app.Screen());
        Assert.Equal("i", app.App.State.ListForm!.Name);
    }

    [Fact]
    public void Arrows_move_and_enter_chooses_the_icon()
    {
        var app = new TestApp().Press('l').Tab().Press('i').Press(ConsoleKey.RightArrow).Press(ConsoleKey.RightArrow).Enter();

        var form = app.App.State.ListForm!;
        Assert.Null(form.IconPick);
        Assert.Equal("\uf513", form.Icon);
        Assert.DoesNotContain("Choose icon", app.Screen());
    }

    [Fact]
    public void Down_moves_one_row()
    {
        var app = new TestApp().Press('l').Tab().Press('i').Down().Enter();

        Assert.Equal(ListForm.Icons[ListForm.IconColumns], app.App.State.ListForm!.Icon);
    }

    [Fact]
    public void Esc_closes_only_the_picker()
    {
        var app = new TestApp().Press('l').Tab().Press('i').Esc();

        var screen = app.Screen();
        Assert.DoesNotContain("Choose icon", screen);
        Assert.Contains("New list", screen);
    }

    [Fact]
    public void A_chosen_icon_creates_the_list_with_that_glyph()
    {
        var app = new TestApp().Press('l').Type("Work").Tab().Press('i').Enter().Enter();

        Assert.Equal("\uf510", app.Repository.Lists.Single().Icon);
    }
}
