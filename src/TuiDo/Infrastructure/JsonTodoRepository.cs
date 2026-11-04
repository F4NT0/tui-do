using System.Text.Json;
using TuiDo.Abstractions;
using TuiDo.Domain;

namespace TuiDo.Infrastructure;

internal sealed class JsonTodoRepository(string directory) : ITodoRepository
{
    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tui-do");

    public string ItemsPath { get; } = Path.Combine(directory, "tasks.json");

    public string ListsPath { get; } = Path.Combine(directory, "lists.json");

    public List<TodoItem> LoadItems() => Load<TodoItem>(ItemsPath);

    public List<CustomList> LoadLists() => Load<CustomList>(ListsPath);

    public void SaveItems(IReadOnlyCollection<TodoItem> items) => Save(ItemsPath, items);

    public void SaveLists(IReadOnlyCollection<CustomList> lists) => Save(ListsPath, lists);

    private static List<T> Load<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? [] : []; }
        catch (JsonException) { return []; }
        catch (IOException) { return []; }
    }

    private static void Save<T>(string path, IReadOnlyCollection<T> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(values));
    }
}
