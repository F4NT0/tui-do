using TuiDo.Abstractions;
using TuiDo.Domain;

namespace TuiDo.Services;

/// <summary>Owns the tasks and custom lists, applies every business rule and persists the result.</summary>
internal sealed class TodoService
{
    private ITodoRepository _repository;
    private readonly IClock _clock;
    private readonly NotificationLog _log;
    private readonly List<TodoItem> _items;
    private readonly List<CustomList> _lists;

    public TodoService(ITodoRepository repository, IClock clock, NotificationLog log)
    {
        _repository = repository;
        _clock = clock;
        _log = log;
        _items = [];
        _lists = [];
        Load();
    }

    /// <summary>Replaces the current data with the contents of another repository (another directory).</summary>
    public void Switch(ITodoRepository repository)
    {
        _repository = repository;
        Load();
    }

    public string DataDirectory => Path.GetDirectoryName(ItemsPath) ?? ItemsPath;

    private void Load()
    {
        _items.Clear();
        _items.AddRange(_repository.LoadItems());
        _lists.Clear();
        _lists.AddRange(_repository.LoadLists());
        var next = NextCode();
        foreach (var item in _items.Where(i => i.Code == 0)) item.Code = next++;
    }

    public IReadOnlyList<TodoItem> Items => _items;

    public IReadOnlyList<CustomList> Lists => _lists;

    public string ItemsPath => _repository.ItemsPath;

    public string ListsPath => _repository.ListsPath;

    // ---- menu (built-in lists followed by custom lists) ----

    public int MenuCount => BuiltInLists.Count + _lists.Count;

    public bool IsCustom(int menu) => menu >= BuiltInLists.Count;

    public CustomList CustomAt(int menu) => _lists[menu - BuiltInLists.Count];

    public string MenuName(int menu) => IsCustom(menu) ? CustomAt(menu).Name : BuiltInLists.Names[menu];

    public int MenuCountOf(int menu) => Filter(menu).Count();

    /// <summary>Lists whose order is decided by the user (priority) instead of by date.</summary>
    public bool IsRanked(int menu, string? query) => query == null && (menu == BuiltInLists.MyDay || IsCustom(menu));

    public IEnumerable<TodoItem> Filter(int menu)
    {
        var today = _clock.Today;
        return menu switch
        {
            BuiltInLists.MyDay => _items.Where(i => i.IsOpen() && i.Date.Date <= today),
            BuiltInLists.Tasks => _items.Where(i => i.ListId == null && i.IsOpen() && i.Date.Date > today),
            BuiltInLists.Important => _items.Where(i => i.IsOpen() && i.Important),
            BuiltInLists.Completed => _items.Where(i => i.Status == Status.Done),
            BuiltInLists.Cancelled => _items.Where(i => i.Status == Status.Cancelled),
            _ => _items.Where(i => i.ListId == CustomAt(menu).Id),
        };
    }

    public List<TodoItem> Visible(int menu, string? query)
    {
        var source = query != null ? Search(query) : Filter(menu);
        return IsRanked(menu, query)
            ? source.OrderBy(i => i.Priority == 0 ? int.MaxValue : i.Priority).ThenBy(i => i.Date).ThenBy(i => i.Time ?? TimeSpan.Zero).ThenBy(i => i.Code).ToList()
            : source.OrderBy(i => i.Date).ThenBy(i => i.Time ?? TimeSpan.Zero).ThenBy(i => i.Code).ToList();
    }

    public IEnumerable<TodoItem> Search(string query)
    {
        var text = query.Trim();
        var code = text.TrimStart('#');
        return _items.Where(i => code.Length == 0 || i.Code.ToString() == code || i.Title.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<TodoItem> OnDate(DateTime date) => _items.Where(i => i.Date.Date == date.Date).OrderBy(i => i.Time ?? TimeSpan.Zero).ThenBy(i => i.Code);

    // ---- tasks ----

    public TodoItem CreateTask(string title, DateTime date, string notes, Guid? listId, TimeSpan? time = null)
    {
        var item = new TodoItem
        {
            Title = title.Trim(),
            Date = date,
            Time = time,
            Notes = notes,
            ListId = listId,
            Code = NextCode(),
            Priority = NextPriority(),
        };
        _items.Add(item);
        SaveItems($"Task #{item.Code} created");
        return item;
    }

    public void UpdateTask(TodoItem item, string title, DateTime date, string notes, TimeSpan? time = null)
    {
        item.Title = title.Trim();
        item.Date = date;
        item.Time = time;
        item.Notes = notes;
        SaveItems($"Task #{item.Code} updated");
    }

    public void SetStatus(TodoItem item, Status status)
    {
        item.Status = status;
        item.BlockedBy = null;
        SaveItems($"#{item.Code} status: {StatusCatalog.Name(status)}");
    }

    public IReadOnlyList<TodoItem> BlockerCandidates(TodoItem item) =>
        _items.Where(i => i != item && i.IsOpen()).OrderBy(i => i.Code).ToList();

    public void Block(TodoItem item, TodoItem blocker)
    {
        item.Status = Status.Blocked;
        item.BlockedBy = blocker.Code;
        SaveItems($"#{item.Code} blocked by #{blocker.Code}");
    }

    public void ToggleImportant(TodoItem item)
    {
        item.Important = !item.Important;
        SaveItems($"#{item.Code} {(item.Important ? "marked" : "unmarked")} as important");
    }

    public void Delete(TodoItem item)
    {
        _items.Remove(item);
        foreach (var other in _items.Where(o => o.BlockedBy == item.Code))
        {
            other.BlockedBy = null;
            if (other.Status == Status.Blocked) other.Status = Status.Pending;
        }

        SaveItems($"Task #{item.Code} deleted");
    }

    public void MoveToList(TodoItem item, Guid? listId)
    {
        item.ListId = listId;
        item.Priority = NextPriority();
        SaveItems($"#{item.Code} moved");
    }

    /// <summary>Moves <paramref name="from"/> to position <paramref name="to"/> inside the visible list; returns the final index.</summary>
    public int Reorder(List<TodoItem> visible, int from, int to)
    {
        to = Math.Clamp(to, 0, visible.Count - 1);
        if (to == from) return from;

        NormalizePriorities(visible);
        var slots = visible.Select(i => i.Priority).Order().ToList();
        var moved = visible[from];
        visible.RemoveAt(from);
        visible.Insert(to, moved);
        for (var n = 0; n < visible.Count; n++) visible[n].Priority = slots[n];
        SaveItems($"'{moved.Title}' moved to position {to + 1}");
        return to;
    }

    // ---- custom lists ----

    /// <summary>Creates a list and returns its index among the custom lists.</summary>
    public int AddList(string name, string iconText)
    {
        var list = new CustomList { Name = name.Trim(), Icon = IconParser.Parse(iconText) };
        _lists.Add(list);
        SaveLists($"List '{list.Name}' created");
        return _lists.Count - 1;
    }

    public void DeleteList(CustomList list)
    {
        foreach (var item in _items.Where(i => i.ListId == list.Id)) item.ListId = null;
        _lists.Remove(list);
        SaveLists();
        SaveItems($"List '{list.Name}' deleted");
    }

    // ---- helpers ----

    public int NextCode() => _items.Select(i => i.Code).DefaultIfEmpty(0).Max() + 1;

    public int NextPriority() => _items.Select(i => i.Priority).DefaultIfEmpty(0).Max() + 1;

    private void NormalizePriorities(IEnumerable<TodoItem> visible)
    {
        var seen = new HashSet<int>();
        foreach (var item in visible)
        {
            if (item.Priority != 0 && seen.Add(item.Priority)) continue;
            item.Priority = NextPriority();
            seen.Add(item.Priority);
        }
    }

    private void SaveItems(string success)
    {
        try { _repository.SaveItems(_items); _log.Success(success); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _log.Error($"Failed to save tasks: {e.Message}"); }
    }

    private void SaveLists(string? success = null)
    {
        try { _repository.SaveLists(_lists); if (success != null) _log.Success(success); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _log.Error($"Failed to save lists: {e.Message}"); }
    }
}
