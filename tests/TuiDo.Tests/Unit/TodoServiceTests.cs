using TuiDo.Domain;
using TuiDo.Services;
using TuiDo.Tests.Support;

namespace TuiDo.Tests.Unit;

public class TodoServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly InMemoryRepository _repo = new();
    private readonly NotificationLog _log;
    private readonly DateTime _today;

    public TodoServiceTests()
    {
        _log = new NotificationLog(_clock);
        _today = _clock.Now.Date;
    }

    private TodoService Service() => new(_repo, _clock, _log);

    private TodoItem Seed(string title, DateTime? date = null, Status status = Status.Pending, bool important = false, Guid? listId = null, int priority = 0, int code = 0)
    {
        var item = new TodoItem { Title = title, Date = date ?? _today, Status = status, Important = important, ListId = listId, Priority = priority, Code = code == 0 ? _repo.Items.Count + 1 : code };
        _repo.Items.Add(item);
        return item;
    }

    [Fact]
    public void Create_assigns_incrementing_codes_and_priorities_and_trims_the_title()
    {
        var service = Service();

        var first = service.CreateTask("  one  ", _today, "", null);
        var second = service.CreateTask("two", _today, "", null);

        Assert.Equal("one", first.Title);
        Assert.Equal([1, 2], new[] { first.Code, second.Code });
        Assert.True(second.Priority > first.Priority);
        Assert.Equal(2, _repo.Items.Count);
        Assert.Equal("Task #2 created", _log.All[^1].Message);
    }

    [Fact]
    public void Codes_continue_after_the_highest_existing_code_when_an_older_task_is_deleted()
    {
        var service = Service();
        var a = service.CreateTask("a", _today, "", null);
        service.CreateTask("b", _today, "", null);
        service.Delete(a);

        var c = service.CreateTask("c", _today, "", null);

        Assert.Equal(3, c.Code);
    }

    [Fact]
    public void Loading_tasks_without_a_code_numbers_them_after_the_highest_existing_code()
    {
        Seed("coded", code: 5);
        var legacy = Seed("legacy", code: 0);
        legacy.Code = 0;

        var service = Service();

        Assert.Equal(6, service.Items.Single(i => i.Title == "legacy").Code);
    }

    [Fact]
    public void My_Day_has_open_tasks_due_today_or_earlier_that_are_not_in_a_custom_list()
    {
        var work = new CustomList { Name = "Work" };
        _repo.Lists.Add(work);
        Seed("today");
        Seed("overdue", _today.AddDays(-3));
        Seed("future", _today.AddDays(1));
        Seed("done", status: Status.Done);
        Seed("in list", listId: work.Id);

        var titles = Service().Filter(BuiltInLists.MyDay).Select(i => i.Title);

        Assert.Equal(["today", "overdue"], titles);
    }

    [Fact]
    public void Tasks_has_open_future_tasks_outside_custom_lists()
    {
        Seed("today");
        Seed("future", _today.AddDays(1));

        Assert.Equal(["future"], Service().Filter(BuiltInLists.Tasks).Select(i => i.Title));
    }

    [Fact]
    public void Important_ignores_finished_tasks_but_keeps_custom_list_members()
    {
        var work = new CustomList { Name = "Work" };
        _repo.Lists.Add(work);
        Seed("vip", important: true, listId: work.Id);
        Seed("done vip", important: true, status: Status.Done);
        Seed("plain");

        Assert.Equal(["vip"], Service().Filter(BuiltInLists.Important).Select(i => i.Title));
    }

    [Fact]
    public void Completed_and_cancelled_have_their_own_lists()
    {
        Seed("a", status: Status.Done);
        Seed("b", status: Status.Cancelled);
        var service = Service();

        Assert.Equal(["a"], service.Filter(BuiltInLists.Completed).Select(i => i.Title));
        Assert.Equal(["b"], service.Filter(BuiltInLists.Cancelled).Select(i => i.Title));
    }

    [Fact]
    public void Custom_list_shows_all_of_its_tasks_whatever_their_status()
    {
        var work = new CustomList { Name = "Work" };
        _repo.Lists.Add(work);
        Seed("open", listId: work.Id);
        Seed("done", listId: work.Id, status: Status.Done);
        Seed("elsewhere");
        var service = Service();

        Assert.Equal(2, service.Filter(BuiltInLists.Count).Count());
        Assert.Equal(2, service.MenuCountOf(BuiltInLists.Count));
    }

    [Fact]
    public void Ranked_lists_sort_by_priority_with_unranked_tasks_last_then_by_date_and_code()
    {
        Seed("unranked late", _today.AddDays(-1));
        Seed("unranked early", _today.AddDays(-5));
        Seed("second", priority: 2);
        Seed("first", priority: 1);

        var titles = Service().Visible(BuiltInLists.MyDay, null).Select(i => i.Title);

        Assert.Equal(["first", "second", "unranked early", "unranked late"], titles);
    }

    [Fact]
    public void Lists_that_are_not_ranked_sort_by_date()
    {
        Seed("later", _today.AddDays(2), important: true, priority: 1);
        Seed("sooner", _today.AddDays(1), important: true, priority: 2);

        var service = Service();

        Assert.False(service.IsRanked(BuiltInLists.Important, null));
        Assert.Equal(["sooner", "later"], service.Visible(BuiltInLists.Important, null).Select(i => i.Title));
    }

    [Theory]
    [InlineData(BuiltInLists.MyDay, null, true)]
    [InlineData(BuiltInLists.MyDay, "x", false)]
    [InlineData(BuiltInLists.Tasks, null, false)]
    [InlineData(BuiltInLists.Completed, null, false)]
    [InlineData(BuiltInLists.Count, null, true)]
    public void Ranking_applies_to_My_Day_and_custom_lists_but_not_to_searches(int menu, string? query, bool ranked)
    {
        _repo.Lists.Add(new CustomList { Name = "Work" });

        Assert.Equal(ranked, Service().IsRanked(menu, query));
    }

    [Fact]
    public void Search_matches_title_case_insensitively_or_exact_code_with_optional_hash()
    {
        Seed("Buy MILK", code: 12);
        Seed("Other", code: 3);
        var service = Service();

        Assert.Equal(["Buy MILK"], service.Search("milk").Select(i => i.Title));
        Assert.Equal(["Buy MILK"], service.Search("#12").Select(i => i.Title));
        Assert.Equal(["Buy MILK"], service.Search("12").Select(i => i.Title));
        Assert.Equal(2, service.Search("").Count());
        Assert.Empty(service.Search("#1"));
    }

    [Fact]
    public void Reorder_moves_a_task_and_keeps_the_same_set_of_priority_slots()
    {
        Seed("a", priority: 10);
        Seed("b", priority: 20);
        Seed("c", priority: 30);
        var service = Service();
        var visible = service.Visible(BuiltInLists.MyDay, null);

        var index = service.Reorder(visible, 2, 0);

        Assert.Equal(0, index);
        Assert.Equal(["c", "a", "b"], service.Visible(BuiltInLists.MyDay, null).Select(i => i.Title));
        Assert.Equal([10, 20, 30], service.Items.Select(i => i.Priority).Order());
    }

    [Fact]
    public void Reorder_clamps_and_ignores_no_op_moves()
    {
        Seed("a", priority: 1);
        Seed("b", priority: 2);
        var service = Service();
        var visible = service.Visible(BuiltInLists.MyDay, null);
        var savesBefore = _repo.ItemSaves;

        Assert.Equal(0, service.Reorder(visible, 0, -5));
        Assert.Equal(savesBefore, _repo.ItemSaves);
        Assert.Equal(1, service.Reorder(visible, 0, 99));
        Assert.Equal(["b", "a"], service.Visible(BuiltInLists.MyDay, null).Select(i => i.Title));
    }

    [Fact]
    public void Reorder_gives_unique_priorities_to_tasks_that_had_none_or_duplicates()
    {
        Seed("a");
        Seed("b");
        Seed("c", priority: 4);
        Seed("d", priority: 4);
        var service = Service();

        service.Reorder(service.Visible(BuiltInLists.MyDay, null), 0, 3);

        var priorities = service.Items.Select(i => i.Priority).ToList();
        Assert.Equal(priorities.Count, priorities.Distinct().Count());
        Assert.All(priorities, p => Assert.True(p > 0));
    }

    [Fact]
    public void SetStatus_clears_the_blocker_and_notifies_with_the_status_name()
    {
        var item = Seed("a");
        item.Status = Status.Blocked;
        item.BlockedBy = 9;
        var service = Service();

        service.SetStatus(service.Items[0], Status.Waiting);

        Assert.Equal(Status.Waiting, item.Status);
        Assert.Null(item.BlockedBy);
        Assert.Equal("#1 status: Need more details", _log.All[^1].Message);
    }

    [Fact]
    public void Blocker_candidates_are_the_other_open_tasks_ordered_by_code()
    {
        Seed("self", code: 1);
        Seed("b", code: 3);
        Seed("a", code: 2);
        Seed("done", code: 4, status: Status.Done);
        Seed("dropped", code: 5, status: Status.Cancelled);
        var service = Service();

        var codes = service.BlockerCandidates(service.Items[0]).Select(i => i.Code);

        Assert.Equal([2, 3], codes);
    }

    [Fact]
    public void Block_records_the_blocker_code()
    {
        Seed("a", code: 1);
        Seed("b", code: 2);
        var service = Service();

        service.Block(service.Items[0], service.Items[1]);

        Assert.Equal(Status.Blocked, service.Items[0].Status);
        Assert.Equal(2, service.Items[0].BlockedBy);
        Assert.Equal("#1 blocked by #2", _log.All[^1].Message);
    }

    [Fact]
    public void Deleting_a_blocker_releases_the_tasks_it_was_blocking()
    {
        Seed("blocker", code: 1);
        var waiting = Seed("waiting", code: 2);
        waiting.Status = Status.Blocked;
        waiting.BlockedBy = 1;
        var other = Seed("blocked but doing", code: 3, status: Status.Doing);
        other.BlockedBy = 1;
        var service = Service();

        service.Delete(service.Items[0]);

        Assert.Equal(Status.Pending, waiting.Status);
        Assert.Null(waiting.BlockedBy);
        Assert.Equal(Status.Doing, other.Status);
        Assert.Null(other.BlockedBy);
    }

    [Fact]
    public void MoveToList_sends_the_task_to_the_end_of_the_target_list()
    {
        _repo.Lists.Add(new CustomList { Name = "Work" });
        Seed("a", priority: 1);
        Seed("b", priority: 7);
        var service = Service();

        service.MoveToList(service.Items[0], service.Lists[0].Id);

        Assert.Equal(service.Lists[0].Id, service.Items[0].ListId);
        Assert.Equal(8, service.Items[0].Priority);
        Assert.Equal("#1 moved", _log.All[^1].Message);
    }

    [Fact]
    public void AddList_parses_the_icon_trims_the_name_and_returns_its_index()
    {
        var service = Service();

        var first = service.AddList("  Work ", "f522");
        var second = service.AddList("Home", "");

        Assert.Equal(0, first);
        Assert.Equal(1, second);
        Assert.Equal("Work", service.Lists[0].Name);
        Assert.Equal("\uf522", service.Lists[0].Icon);
        Assert.Equal(IconParser.DefaultIcon, service.Lists[1].Icon);
        Assert.Equal(2, _repo.Lists.Count);
    }

    [Fact]
    public void DeleteList_returns_its_tasks_to_the_default_lists()
    {
        var service = Service();
        service.AddList("Work", "");
        var item = service.CreateTask("a", _today, "", service.Lists[0].Id);

        service.DeleteList(service.Lists[0]);

        Assert.Null(item.ListId);
        Assert.Empty(service.Lists);
        Assert.Empty(_repo.Lists);
        Assert.Equal("List 'Work' deleted", _log.All[^1].Message);
    }

    [Fact]
    public void Menu_helpers_cover_built_in_and_custom_entries()
    {
        var service = Service();
        service.AddList("Work", "");

        Assert.Equal(BuiltInLists.Count + 1, service.MenuCount);
        Assert.Equal("My Day", service.MenuName(0));
        Assert.Equal("Work", service.MenuName(BuiltInLists.Count));
        Assert.False(service.IsCustom(BuiltInLists.Cancelled));
        Assert.True(service.IsCustom(BuiltInLists.Count));
    }

    [Fact]
    public void ToggleImportant_flips_and_reports_both_ways()
    {
        Seed("a");
        var service = Service();

        service.ToggleImportant(service.Items[0]);
        Assert.Equal("#1 marked as important", _log.All[^1].Message);

        service.ToggleImportant(service.Items[0]);
        Assert.Equal("#1 unmarked as important", _log.All[^1].Message);
        Assert.False(service.Items[0].Important);
    }

    [Fact]
    public void Save_failures_become_error_notifications_instead_of_exceptions()
    {
        var service = Service();
        _repo.FailOnSave = true;

        service.CreateTask("a", _today, "", null);
        service.AddList("Work", "");

        Assert.Equal(["Failed to save tasks: disk is full", "Failed to save lists: disk is full"], _log.All.Select(n => n.Message));
        Assert.All(_log.All, n => Assert.Equal(NotificationKind.Error, n.Kind));
    }

    [Fact]
    public void OnDate_returns_the_tasks_of_that_day_across_lists_ordered_by_code()
    {
        Seed("b", code: 2);
        Seed("a", code: 1);
        Seed("other day", _today.AddDays(1), code: 3);

        Assert.Equal(["a", "b"], Service().OnDate(_today).Select(i => i.Title));
    }

    [Fact]
    public void Overdue_only_applies_to_pending_tasks_before_today()
    {
        Assert.True(new TodoItem { Date = _today.AddDays(-1) }.IsOverdue(_today));
        Assert.False(new TodoItem { Date = _today }.IsOverdue(_today));
        Assert.False(new TodoItem { Date = _today.AddDays(-1), Status = Status.Doing }.IsOverdue(_today));
        Assert.False(new TodoItem { Date = _today.AddDays(-1), Status = Status.Done }.IsOverdue(_today));
    }
}
