using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using TuiDo.Abstractions;
using TuiDo.Domain;

namespace TuiDo.Infrastructure;

/// <summary>
/// Stores tasks and lists as indented JSON. With <paramref name="markdownDescriptions"/> the task notes are kept out of
/// tasks.json and written to <c>descriptions/{code}-{title}.md</c> instead.
/// </summary>
internal sealed partial class JsonTodoRepository(string directory, bool markdownDescriptions = false) : ITodoRepository
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static readonly JsonSerializerOptions IndentedWithoutNotes = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { type => { if (type.Type == typeof(TodoItem)) type.Properties.Remove(type.Properties.First(p => p.Name == nameof(TodoItem.Notes))); } },
        },
    };

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tui-do");

    public string ItemsPath { get; } = Path.Combine(directory, "tasks.json");

    public string ListsPath { get; } = Path.Combine(directory, "lists.json");

    public string DescriptionsPath { get; } = Path.Combine(directory, "descriptions");

    public static bool LooksLikeWorkspace(string directory) =>
        File.Exists(Path.Combine(directory, "tasks.json")) || File.Exists(Path.Combine(directory, "lists.json")) || Directory.Exists(Path.Combine(directory, "descriptions"));

    /// <summary>Creates the folder structure of an empty workspace.</summary>
    public void Initialize()
    {
        Directory.CreateDirectory(DescriptionsPath);
        if (!File.Exists(ItemsPath)) SaveItems([]);
        if (!File.Exists(ListsPath)) SaveLists([]);
    }

    public List<TodoItem> LoadItems()
    {
        var items = Load<TodoItem>(ItemsPath);
        if (!markdownDescriptions || !Directory.Exists(DescriptionsPath)) return items;
        foreach (var item in items.Where(i => i.Code > 0))
        {
            var file = Directory.EnumerateFiles(DescriptionsPath, "*.md").FirstOrDefault(f =>
                Path.GetFileName(f) is var n && (n == $"{item.Code}.md" || n.StartsWith($"{item.Code}-", StringComparison.Ordinal)));
            if (file != null) item.Notes = File.ReadAllText(file);
        }
        return items;
    }

    public List<CustomList> LoadLists() => Load<CustomList>(ListsPath);

    public void SaveItems(IReadOnlyCollection<TodoItem> items)
    {
        Save(ItemsPath, items, markdownDescriptions ? IndentedWithoutNotes : Indented);
        if (markdownDescriptions) SaveDescriptions(items);
    }

    public void SaveLists(IReadOnlyCollection<CustomList> lists) => Save(ListsPath, lists, Indented);

    private void SaveDescriptions(IReadOnlyCollection<TodoItem> items)
    {
        var expected = items.Where(i => i.Code > 0 && !string.IsNullOrWhiteSpace(i.Notes)).ToDictionary(DescriptionFileName, i => i.Notes);

        if (Directory.Exists(DescriptionsPath))
            foreach (var file in Directory.EnumerateFiles(DescriptionsPath, "*.md"))
            {
                var name = Path.GetFileName(file);
                if (MarkdownFileName().IsMatch(name) && !expected.ContainsKey(name)) File.Delete(file);
            }

        if (expected.Count > 0) Directory.CreateDirectory(DescriptionsPath);

        foreach (var (name, notes) in expected)
        {
            var path = Path.Combine(DescriptionsPath, name);
            if (!File.Exists(path) || File.ReadAllText(path) != notes) File.WriteAllText(path, notes);
        }
    }

    internal static string DescriptionFileName(TodoItem item)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var title = new string(item.Title.Select(c => invalid.Contains(c) ? ' ' : c).ToArray());
        title = WhiteSpace().Replace(title, " ").Trim().TrimEnd('.');
        if (title.Length > 80) title = title[..80].TrimEnd();
        return title.Length == 0 ? $"{item.Code}.md" : $"{item.Code}-{title}.md";
    }

    [GeneratedRegex(@"^\d+(-.*)?\.md$")]
    private static partial Regex MarkdownFileName();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    private static List<T> Load<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? [] : []; }
        catch (JsonException) { return []; }
        catch (IOException) { return []; }
    }

    private static void Save<T>(string path, IReadOnlyCollection<T> values, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(values, options));
    }
}
