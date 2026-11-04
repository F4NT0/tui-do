using TuiDo.Abstractions;
using TuiDo.Domain;
using TuiDo.Infrastructure;
using TuiDo.Services;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Unit;

public class IconParserTests
{
    [Theory]
    [InlineData("f522", "\uf522")]
    [InlineData("F522", "\uf522")]
    [InlineData("0xf522", "\uf522")]
    [InlineData("\\uf522", "\uf522")]
    [InlineData("\\udb81\\udcce", "\udb81\udcce")]
    [InlineData("  \\uf522  ", "\uf522")]
    [InlineData("\uf522", "\uf522")]
    [InlineData("F04CE", "\udb81\udcce")]
    public void Accepts_hex_codes_escapes_and_glyphs(string typed, string expected) =>
        Assert.Equal(expected, IconParser.Parse(typed));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_input_falls_back_to_the_default_icon(string? typed) =>
        Assert.Equal(IconParser.DefaultIcon, IconParser.Parse(typed));

    [Theory]
    [InlineData("hello")]
    [InlineData("xyz1")]
    public void Unrecognised_text_is_kept_as_typed(string typed) =>
        Assert.Equal(typed, IconParser.Parse(typed));

    [Fact]
    public void Surrogate_code_points_are_not_treated_as_hex()
    {
        Assert.Equal("D800", IconParser.Parse("D800"));
    }
}

public class NotificationLogTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Active_is_the_newest_entry_while_younger_than_two_seconds()
    {
        var log = new NotificationLog(_clock);
        Assert.Null(log.Active);

        log.Success("a");
        _clock.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.Equal("a", log.Active!.Message);

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Null(log.Active);
    }

    [Fact]
    public void Newer_entries_replace_older_ones_on_screen_but_not_in_history()
    {
        var log = new NotificationLog(_clock);
        log.Success("a");
        log.Error("b");
        log.Warning("c");

        Assert.Equal("c", log.Active!.Message);
        Assert.Equal([NotificationKind.Success, NotificationKind.Error, NotificationKind.Warning], log.All.Select(n => n.Kind));
    }

    [Fact]
    public void History_is_capped()
    {
        var log = new NotificationLog(_clock);
        for (var i = 0; i < 250; i++) log.Success($"n{i}");

        Assert.Equal(200, log.All.Count);
        Assert.Equal("n50", log.All[0].Message);
    }

    [Fact]
    public void Signature_is_zero_when_nothing_is_shown_and_changes_with_every_new_entry()
    {
        var log = new NotificationLog(_clock);
        Assert.Equal(0, log.Signature);

        log.Success("a");
        var first = log.Signature;
        log.Success("b");

        Assert.NotEqual(0, first);
        Assert.NotEqual(first, log.Signature);
    }
}

public sealed class JsonTodoRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tuido-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_files_load_as_empty_lists()
    {
        var repo = new JsonTodoRepository(_dir);

        Assert.Empty(repo.LoadItems());
        Assert.Empty(repo.LoadLists());
    }

    [Fact]
    public void Paths_are_tasks_json_and_lists_json_inside_the_directory()
    {
        var repo = new JsonTodoRepository(_dir);

        Assert.Equal(Path.Combine(_dir, "tasks.json"), repo.ItemsPath);
        Assert.Equal(Path.Combine(_dir, "lists.json"), repo.ListsPath);
    }

    [Fact]
    public void Items_and_lists_round_trip_with_every_field()
    {
        var repo = new JsonTodoRepository(_dir);
        var list = new CustomList { Name = "Work", Icon = "\udb81\udcce" };
        var item = new TodoItem
        {
            Code = 4, Title = "Ship it", Date = new DateTime(2026, 12, 1), Notes = "# notes", Status = Status.Blocked,
            Important = true, BlockedBy = 2, ListId = list.Id, Priority = 3,
        };

        repo.SaveItems([item]);
        repo.SaveLists([list]);
        var loaded = new JsonTodoRepository(_dir).LoadItems().Single();
        var loadedList = new JsonTodoRepository(_dir).LoadLists().Single();

        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal(4, loaded.Code);
        Assert.Equal("Ship it", loaded.Title);
        Assert.Equal(new DateTime(2026, 12, 1), loaded.Date);
        Assert.Equal("# notes", loaded.Notes);
        Assert.Equal(Status.Blocked, loaded.Status);
        Assert.True(loaded.Important);
        Assert.Equal(2, loaded.BlockedBy);
        Assert.Equal(list.Id, loaded.ListId);
        Assert.Equal(3, loaded.Priority);
        Assert.Equal("Work", loadedList.Name);
        Assert.Equal("\udb81\udcce", loadedList.Icon);
    }

    [Fact]
    public void Corrupt_json_loads_as_empty_instead_of_crashing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "tasks.json"), "{ not json");
        File.WriteAllText(Path.Combine(_dir, "lists.json"), "[1, 2");
        var repo = new JsonTodoRepository(_dir);

        Assert.Empty(repo.LoadItems());
        Assert.Empty(repo.LoadLists());
    }

    [Fact]
    public void Files_written_by_older_versions_without_the_new_fields_still_load()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "tasks.json"), """[{"Title":"legacy","Date":"2026-11-03T00:00:00","Status":1}]""");

        var item = new JsonTodoRepository(_dir).LoadItems().Single();

        Assert.Equal("legacy", item.Title);
        Assert.Equal(0, item.Code);
        Assert.Equal(0, item.Priority);
        Assert.Null(item.ListId);
    }

    [Fact]
    public void Saving_creates_the_directory()
    {
        var repo = new JsonTodoRepository(Path.Combine(_dir, "nested", "deeper"));

        repo.SaveItems([]);

        Assert.True(File.Exists(repo.ItemsPath));
    }
}
